// Copyright (c) Microsoft Corporation.  All rights reserved.
// This source code is made available under the terms of the Microsoft Public License (MS-PL)

using IQToolkit.Data.SqlClient;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace IQToolkit
{
    /// <summary>
    /// IQToolkit interface IQueryText - optional interface for <see cref="IQueryProvider"/> to implement <see cref="Query{T}.QueryText"/> property.
    /// </summary>
    public interface IQueryText
    {
        string GetQueryText(Expression expression);
        object ExecuteExpr(Expression expression);
        IQueryable CreateQuery(Expression expression);
    }

    public interface IQuery
    { 
        object[] ParamValues { get; set; }

        Expression Expression  { get; }

        Type ElementType { get; }

        IQueryProvider Provider { get ;  }
        
    }

    /// <summary>
    /// A default implementation of IQueryable for use with QueryProvider
    /// </summary>
    public class Query<T> : IQuery, IQueryable<T>, IQueryable, IEnumerable<T>, IEnumerable, IOrderedQueryable<T>, IOrderedQueryable {
        IQueryProvider provider;
        Expression expression;

        public static IQueryProvider Default { get; set; }
        public static IQueryProvider SqlProvider(SqlConnection connection) => new SqlQueryProvider(connection, typeof(T));
        public static IQueryProvider DbProvider(IDbConnection connection) {
            SqlConnection mssql = connection as SqlConnection ?? throw new NotImplementedException();
            return SqlProvider(mssql);
        }

        public Query(IQueryProvider provider = null)
            : this(provider ?? Default, null) {
        }

        public Query(IDbConnection connection)
             : this(DbProvider(connection), typeof(T)) {
        }

        public Query(IQueryProvider provider, Type staticType) {
            if (provider == null) {
                if (Default == null) {
                    throw new ArgumentNullException("Provider");
                }

                provider = Default;
            }

            this.provider = provider;
            this.expression = staticType != null ? Expression.Constant(this, staticType) : Expression.Constant(this);
        }

        public Query(QueryProvider provider, Expression expression) {
            this.provider = provider ?? throw new ArgumentNullException("Provider");
            this.expression = expression ?? throw new ArgumentNullException("expression");
            if (typeof(IQueryable<T>).GetTypeInfo().IsAssignableFrom(expression.Type)) {
                // OK...
            } else {
                // throw new ArgumentOutOfRangeException("expression");
                if (Debugger.IsAttached) {
                    Console.WriteLine("$ArgumentOutOfRangeException {expression}");
                }
            }
        }

        public object[] ParamValues { get; set; }

        public Expression Expression  { get => expression; }

        public Type ElementType { get => typeof(T); }

        public IQueryProvider Provider { get => provider;  }

        public IEnumerator<T> GetEnumerator()
            =>
            // Executes the query represented by a specified expression tree.
            ((this.provider as SqlQueryProvider)?.ExecuteExprQuery<T>(this.expression, this)
             ?? ((IEnumerable<T>)this.provider.Execute(this.expression))
            )
                  .GetEnumerator();
        
        IEnumerator IEnumerable.GetEnumerator()
            =>
            // Executes the query represented by a specified expression tree.
            ((IEnumerable)this.provider.Execute(this.expression))
                  .GetEnumerator();

        public override string ToString()
        {
            if (this.expression.NodeType == ExpressionType.Constant &&
                ((ConstantExpression)this.expression).Value == this) {
                return "Query(" + typeof(T) + ")";
            } else {
                return this.expression.ToString();
            }
        }

        public string QueryText
        {
            get 
            {
                var iqt = this.provider as IQueryText;
                if (iqt != null) {
                    return iqt.GetQueryText(this.expression);
                }
                return string.Empty;
            }
        }
    }
}
