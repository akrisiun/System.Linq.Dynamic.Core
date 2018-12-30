// Copyright (c) Microsoft Corporation.  All rights reserved.
// This source code is made available under the terms of the Microsoft Public License (MS-PL)
// Matt Warren : https://github.com/mattwar/iqtoolkit

#if (!NETSTANDARD_20) && !NOMSSQL

namespace IQToolkit.Data.SqlClient
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.Common;
    using System.Data.SqlClient;
    using System.IO;
    using System.Linq;
    using System.Linq.Expressions;
    using IQToolkit.Data.Common;

    public class SqlQueryProvider : DbEntityProvider, IQueryProvider
    {
        bool? allowMulitpleActiveResultSets;

        public SqlQueryProvider(SqlConnection connection, QueryMapping mapping, QueryPolicy policy)
            : base(connection, TSqlLanguage.Default, mapping, policy)
        {
        }

        public SqlQueryProvider(IDbConnection connection, Type type)
          : base(connection, type) // TSqlLanguage.Default, null, QueryPolicy.Default) 
        { }


        public override DbEntityProvider New(IDbConnection connection, QueryMapping mapping, QueryPolicy policy)
        {
            return new SqlQueryProvider((SqlConnection)connection, mapping, policy);
        }

        //  Constructs an System.Linq.IQueryable object that can evaluate the query represented by expression
        //  expression: tree that represents a LINQ query.
        public override IQueryable CreateQuery(Expression expression) {
            var wrap = base.CreateQuery(expression);
            return wrap;
        }

        public static string GetConnectionStringExpress(string databaseFile)
        {
            if (databaseFile.EndsWith(".mdf"))
            {
                databaseFile = Path.GetFullPath(databaseFile);
            }

            return string.Format(@"Data Source=.\SQLEXPRESS;Integrated Security=True;Connect Timeout=30;User Instance=True;MultipleActiveResultSets=true;AttachDbFilename='{0}'", databaseFile);
        }

        public bool AllowsMultipleActiveResultSets
        {
            get
            {
                if (this.allowMulitpleActiveResultSets == null)
                {
                    var builder = new SqlConnectionStringBuilder(this.Connection.ConnectionString);
                    var result = builder["MultipleActiveResultSets"];
                    this.allowMulitpleActiveResultSets = (result != null && result.GetType() == typeof(bool) && (bool)result);
                }
                return (bool)this.allowMulitpleActiveResultSets;
            }
        }

        protected override QueryExecutor CreateExecutor()
        {
            return new SqlExecutor(this);
        }

        public new Tuple<SqlCommand, SqlDataReader> ExecuteSqlRead(string commandText)
        {
            var conn = this.Connection as IDbConnection;
            var cmd = ((SqlConnection)conn)?.CreateCommand();
            SqlDataReader obj = null;

            if (cmd != null)
            {
                cmd.CommandText = commandText;
                cmd.Connection = conn as SqlConnection;
                //  cmd.CommandType 

                 obj = cmd.ExecuteReader() as SqlDataReader;
            }
            
            return new Tuple<SqlCommand, SqlDataReader>(cmd, obj);
        }

        public class SqlExecutor : DbEntityProvider.Executor {
            SqlQueryProvider provider;

            public SqlExecutor(SqlQueryProvider provider)
                : base(provider)
            {
                this.provider = provider;
            }

            public override IEnumerable<T> ExecuteDeferred<T>(QueryCommand query, Func<FieldReader, T> fnProjector, MappingEntity entity, object[] paramValues) 
            {
                this.CommandText = paramValues[0] as string;
                // var wrap = base.ExecuteDeferred<T>(query, fnProjector, entity, paramValues);

                this.LogCommand(query, paramValues);
                this.StartUsingConnection();
                try
                {
                    DbCommand cmd = this.GetCommand(query, paramValues);
                    var reader = this.ExecuteReader(cmd);
                    var freader = new DbFieldReader(this, reader);
                    try
                    {
                        while (reader.Read())
                        {
                            yield return fnProjector(freader);
                        }
                    }
                    finally
                    {
                        reader.Close();
                    }
                }
                finally
                {
                    this.StopUsingConnection();
                }
            }


            public override IEnumerable<T> Execute<T>(QueryCommand command, Func<FieldReader, T> fnProjector, MappingEntity entity, IQuery query, object[] paramValues) 
            {
                if (query.ParamValues != null) {
                    this.Query = query;
                    CommandText = query.ParamValues[0] as string;
                } else {
                    CommandText = command.CommandText;
                }

                return base.Execute<T>(command, fnProjector, entity, paramValues);
            }

            public IEnumerable<T> Execute2<T>(QueryCommand command, Func<FieldReader, T> fnProjector, MappingEntity entity, IQuery query, object[] paramValues) {
                return base.Execute<T>(command, fnProjector, entity, paramValues);
            }

            public string CommandText { get; set; }

            protected override DbCommand GetCommand(QueryCommand query, object[] paramValues) 
            {
                var wrap = base.GetCommand(query, paramValues);

                var real = new SqlCommand { CommandText = CommandText, CommandType = CommandType.Text };
                real.Connection = wrap.Connection as SqlConnection;
                return real;
            }

            protected override bool BufferResultRows
            {
                get { return !this.provider.AllowsMultipleActiveResultSets; }
            }

            protected override void AddParameter(DbCommand command, QueryParameter parameter, object value)
            {
                DbQueryType sqlType = (DbQueryType)parameter.QueryType;
                if (sqlType == null)
                {
                    sqlType = (DbQueryType)this.Provider.Language.TypeSystem.GetColumnType(parameter.Type);
                }

                int len = sqlType.Length;
                if (len == 0 && DbTypeSystem.IsVariableLength(sqlType.SqlDbType))
                {
                    len = Int32.MaxValue;
                }

                var p = ((SqlCommand)command).Parameters.Add("@" + parameter.Name, sqlType.SqlDbType, len);
                if (sqlType.Precision != 0)
                {
                    p.Precision = (byte)sqlType.Precision;
                }

                if (sqlType.Scale != 0)
                {
                    p.Scale = (byte)sqlType.Scale;
                }

                p.Value = value ?? DBNull.Value;
            }

            public override IEnumerable<int> ExecuteBatch(QueryCommand query,
                IEnumerable<object[]> paramSets, int batchSize, bool stream)
            {

                this.StartUsingConnection();
                try
                {
                    // IEnumerable<int> result = null;
                    throw new NotImplementedException();

                    //var result = this.ExecuteBatch(query, paramSets, batchSize);
                    //if (!stream || this.ActionOpenedConnection)
                    //{
                    //    return result.ToList();
                    //}
                    //else
                    //{
                    //    return new Key.EnumerateOnce<int>(result);
                    //}
                }
                finally
                {
                    this.StopUsingConnection();
                }
            }

            //private IEnumerable<int> ExecuteBatch(QueryCommand query, IEnumerable<object[]> paramSets, int batchSize)
            //{
            //    SqlCommand cmd = (SqlCommand)this.GetCommand(query, null);
            //    DataTable dataTable = new DataTable();

            //    for (int i = 0, n = query.Parameters.Count; i < n; i++)
            //    {
            //        var qp = query.Parameters[i];
            //        cmd.Parameters[i].SourceColumn = qp.Name;
            //        dataTable.Columns.Add(qp.Name, TypeHelper.GetNonNullableType(qp.Type));
            //    }

            //    SqlDataAdapter dataAdapter = new SqlDataAdapter();
            //    dataAdapter.InsertCommand = cmd;
            //    dataAdapter.InsertCommand.UpdatedRowSource = UpdateRowSource.None;
            //    dataAdapter.UpdateBatchSize = batchSize;

            //    this.LogMessage("-- Start SQL Batching --");
            //    this.LogMessage("");
            //    this.LogCommand(query, null);

            //    IEnumerator<object[]> en = paramSets.GetEnumerator();
            //    using (en)
            //    {
            //        bool hasNext = true;
            //        while (hasNext)
            //        {
            //            int count = 0;
            //            for (; count < dataAdapter.UpdateBatchSize && (hasNext = en.MoveNext()); count++)
            //            {
            //                var paramValues = en.Current;
            //                dataTable.Rows.Add(paramValues);
            //                this.LogParameters(query, paramValues);
            //                this.LogMessage("");
            //            }

            //            if (count > 0)
            //            {
            //                int n = dataAdapter.Update(dataTable);
            //                for (int i = 0; i < count; i++)
            //                {
            //                    yield return (i < n) ? 1 : 0;
            //                }

            //                dataTable.Rows.Clear();
            //            }
            //        }
            //    }

            //    this.LogMessage(string.Format("-- End SQL Batching --"));
            //    this.LogMessage("");
            //}
        }
    }
}

#endif