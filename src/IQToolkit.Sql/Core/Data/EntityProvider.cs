// Copyright (c) Microsoft Corporation.  All rights reserved.
// This source code is made available under the terms of the Microsoft Public License (MS-PL)

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace IQToolkit.Data
{
    using Common;
    using IQToolkit.Data.SqlClient;
    // using IQToolkit.Session;
    using Mapping;
    using System.Collections;

    /// <summary>
    /// A LINQ IQueryable query provider that executes database queries over a DbConnection
    /// </summary>
    public abstract class EntityProvider : QueryProvider, IEntityProvider, ICreateExecutor
    {
        protected QueryPolicy policy;
        protected Dictionary<MappingEntity, IEntityTable> tables;
        protected QueryLanguage language;
        protected QueryMapping mapping;
        protected TextWriter log;
        // protected QueryCache cache;

        public EntityProvider(Type type, QueryLanguage language = null) 
        {
            this.language = language ?? TSqlLanguage.Default;
            this.mapping = new AttributeMapping(type);
            this.policy = QueryPolicy.Default;

            this.tables = new Dictionary<MappingEntity, IEntityTable>();
        }

        public EntityProvider(QueryLanguage language, QueryMapping mapping, QueryPolicy policy)
        {
            this.language = language ?? throw new InvalidOperationException("Language not specified");
            this.mapping = mapping ?? throw new InvalidOperationException("Mapping not specified");
            this.policy = policy ?? throw new InvalidOperationException("Policy not specified");

            this.tables = new Dictionary<MappingEntity, IEntityTable>();
        }

        #region Properties 

        public QueryMapping Mapping
        {
            get { return this.mapping; }
        }

        public QueryLanguage Language
        {
            get { return this.language; }
        }

        public QueryPolicy Policy
        {
            get { return this.policy; }

            set
            {
                if (value == null)
                {
                    this.policy = QueryPolicy.Default;
                }
                else
                {
                    this.policy = value;
                }
            }
        }

        public TextWriter Log
        {
            get { return this.log; }
            set { this.log = value; }
        }

#if SESSION
        public QueryCache Cache
        {
            get { return this.cache; }
            set { this.cache = value; }
        }
#endif

        #endregion


        #region Get Table

        public IEntityTable GetTable(MappingEntity entity)
        {
            IEntityTable table = null;
            if (this.tables.Count == 0 || !this.tables.TryGetValue(entity, out table))
            {
                table = this.CreateTable(entity);
                this.tables.Add(entity, table);
            }
            return table;
        }

        public virtual IEntityTable DOCreateTable(MappingEntity entity)
               => this.CreateTable(entity);

        protected virtual IEntityTable CreateTable(MappingEntity entity)
        {
            return (IEntityTable) Activator.CreateInstance(
                typeof(EntityTable<>).MakeGenericType(entity.ElementType), 
                new object[] { this, entity }
                );
        }

        public virtual IEntityTable<T> TableExec<T>(IDbConnection conn, string query) {
            
        var mapping = new AttributeMapping(typeof(T));
            MappingEntity entity = mapping.GetEntity(typeof(T), ""); // typeof(T).Name);
            IEntityTable<T> table = null;
            if (entity != null) {
                table = CreateTable(entity) as IEntityTable<T>;
            }

            var table2 = ExecClass<T>(conn, query, table);
            
            return table2 ?? table;
        }

        public virtual IEntityTable<S> ExecClass<S>(IDbConnection conn, string query, IEntityTable<S> table) //  where S : class
        { 
            var tor = this.CreateExecutor() as SqlQueryProvider.SqlExecutor;
            var queryCmd = new QueryCommand(query, Enumerable.Empty<QueryParameter>());
        
            Func<FieldReader, S> fn = (r) => {
                S obj = (S)Activator.CreateInstance(typeof(S));

                FieldReader rr = r;

                return (S)obj;
            };

            MappingEntity me = null;

            var parm = new object[] { query, queryCmd, table };
            (table as Query<S>).ParamValues = parm;

            var enum1 = tor.ExecuteDeferred<S>(queryCmd, fn, me, parm);
            // table.Except

            // TODO: parse results

            return table;
        }

        public virtual IEntityTable<T> GetTable<T>()
        {
            return GetTable<T>(null);
        }

        public virtual IEntityTable<T> GetTable<T>(string tableId)
        {
            return (IEntityTable<T>)this.GetTable(typeof(T), tableId ?? typeof(T).Name);
        }

        public virtual IEntityTable GetTable(Type type)
        {
            return GetTable(type, null);
        }

        public virtual IEntityTable GetTable(Type type, string tableId)
        {
            return this.GetTable(this.Mapping.GetEntity(type, tableId));
        }

        public bool CanBeEvaluatedLocally(Expression expression)
        {
            return this.Mapping.CanBeEvaluatedLocally(expression);
        }

        public virtual bool CanBeParameter(Expression expression)
        {
            Type type = TypeHelper.GetNonNullableType(expression.Type);
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Object:
                    if (expression.Type == typeof(Byte[]) ||
                        expression.Type == typeof(Char[]))
                        return true;
                    return false;
                default:
                    return true;
            }
        }

        protected abstract QueryExecutor CreateExecutor();

        QueryExecutor ICreateExecutor.CreateExecutor()
        {
            return this.CreateExecutor();
        }

        public class EntityTable<T> : Query<T>, IEntityTable<T>, IHaveMappingEntity
        {
            MappingEntity entity;
            EntityProvider provider;

            public EntityTable(EntityProvider provider, MappingEntity entity)
                : base(provider, typeof(IEntityTable<T>))
            {
                this.provider = provider;
                this.entity = entity;
            }

            public MappingEntity Entity
            {
                get { return this.entity; }
            }

            new public IEntityProvider Provider
            {
                get { return this.provider; }
            }

            public string TableId
            {
                get { return this.entity.TableId; }
            }

            public Type EntityType
            {
                get { return this.entity.EntityType; }
            }

            public T GetById(object id)
            {
                var dbProvider = this.Provider;
                if (dbProvider != null)
                {
                    IEnumerable<object> keys = id as IEnumerable<object>;
                    if (keys == null)
                        keys = new object[] { id };
                    Expression query = ((EntityProvider)dbProvider).Mapping.GetPrimaryKeyQuery(this.entity, this.Expression, keys.Select(v => Expression.Constant(v)).ToArray());
                    return this.Provider.Execute<T>(query);
                }
                return default(T);
            }

            object IEntityTable.GetById(object id)
            {
                return this.GetById(id);
            }

#if SESSION
            public int Insert(T instance)
            {
                return UpdatableCall.Insert(this, instance);
            }

            int IEntityTable.Insert(object instance)
            {
                return this.Insert((T)instance);
            }

            public int Delete(T instance)
            {
                return UpdatableCall.Delete(this, instance);
            }

            int IEntityTable.Delete(object instance)
            {
                return this.Delete((T)instance);
            }

            public int Update(T instance)
            {
                return UpdatableCall.Update(this, instance);
            }

            int IEntityTable.Update(object instance)
            {
                return this.Update((T)instance);
            }

            public int InsertOrUpdate(T instance)
            {
                return UpdatableCall.InsertOrUpdate(this, instance);
            }

            int IEntityTable.InsertOrUpdate(object instance)
            {
                return this.InsertOrUpdate((T)instance);
            }
#endif
        }

        #endregion

        public override string GetQueryText(Expression expression)
        {
            Expression plan = this.GetExecutionPlan<object>(expression, null);
            var commands = CommandGatherer.Gather(plan).Select(c => c.CommandText).ToArray();
            return string.Join("\n\n", commands);
        }

        class CommandGatherer : DbExpressionVisitor
        {
            List<QueryCommand> commands = new List<QueryCommand>();

            public static ReadOnlyCollection<QueryCommand> Gather(Expression expression)
            {
                var gatherer = new CommandGatherer();
                gatherer.Visit(expression);
                return gatherer.commands.AsReadOnly();
            }

            protected override Expression VisitConstant(ConstantExpression c)
            {
                QueryCommand qc = c.Value as QueryCommand;
                if (qc != null)
                {
                    this.commands.Add(qc);
                }
                return c;
            }
        }

        #region Execute

        public string GetQueryPlan(Expression expression)
        {
            Expression plan = this.GetExecutionPlan<object>(expression, null);
            return DbExpressionWriter.WriteToString(this.Language, plan);
        }

        protected virtual IQueryTranslator CreateTranslator()
        {
            return new QueryTranslator(this.language, this.mapping, this.policy);
        }

        public abstract void DoTransacted(Action action);
        public abstract void DoConnected(Action action);
        public abstract int ExecuteCommand(string commandText);

        public virtual IEnumerable<T> ExecuteExprQuery<T>(Expression expression, Query<T> caller) 
        { 
            var lambda = expression as LambdaExpression;
#if SESSION
            if (lambda == null && this.cache != null && expression.NodeType != ExpressionType.Constant) {
                return this.cache.Execute(expression) as IEnumerable<T>;
            }
#endif

            Expression plan = this.GetExecutionPlan<T>(expression, caller);
            if (lambda != null) {
                
                // compile & return the execution plan so it can be used multiple times
                LambdaExpression fn = Expression.Lambda(lambda.Type, plan, lambda.Parameters);
                return fn.Compile() as IEnumerable<T>; 

            } else {

                // compile the execution plan and invoke it
                Expression<Func<object>> efn = Expression.Lambda<Func<object>>(Expression.Convert(plan, typeof(object)));
                Func<object> fn = efn.Compile();

                //{() => Convert(Invoke(executor => Convert(Sequence(new [] {
                // Assign(executor, Convert(*Record).Provider, ICreateExecutor).CreateExecutor()), 
                // executor.Execute(value(IQToolkit.Data.Common.QueryCommand), 
                // r0 => new *Record() {}, 
                // value(IQToolkit.Data.Mapping.AttributeMapping+AttributeMappingEntity), new [] {})}), IEnumerable`1), null), Object)
                //}

                var wrap = fn() as IEnumerable<T>;
                
                return wrap;
            }
        }

        /// <summary>
        /// Execute the query expression (does translation, etc.)
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        public override object ExecuteExpr(Expression expression)
        {
            LambdaExpression lambda = expression as LambdaExpression;

#if SESSION
            if (lambda == null && this.cache != null && expression.NodeType != ExpressionType.Constant)
            {
                return this.cache.Execute(expression);
            }
#endif

            Expression plan = this.GetExecutionPlan<object>(expression, null);

            if (lambda != null)
            {
                // compile & return the execution plan so it can be used multiple times
                LambdaExpression fn = Expression.Lambda(lambda.Type, plan, lambda.Parameters);
#if NOREFEMIT
                    return ExpressionEvaluator.CreateDelegate(fn);
#else
                return fn.Compile();
#endif
            }
            else
            {
                // compile the execution plan and invoke it
                Expression<Func<object>> efn = Expression.Lambda<Func<object>>(Expression.Convert(plan, typeof(object)));
#if NOREFEMIT
                    return ExpressionEvaluator.Eval(efn, new object[] { });
#else
                Func<object> fn = efn.Compile();
                return fn();
#endif
            }
        }

        /// <summary>
        /// Convert the query expression into an execution plan
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        public virtual Expression GetExecutionPlan<S>(Expression expression, Query<S> caller = null)
        {
            // strip off lambda for now
            var lambda = expression as LambdaExpression;
            if (lambda != null)
                expression = lambda.Body;

            //  QueryTranslator 
            var translator = this.CreateTranslator();
            //  assing caller IQuerable<S> -> IQuery
            translator.Query = caller as IQuery;

            // translate query into client & server parts
            Expression translation = translator.Translate(expression);

            var parameters = lambda != null ? lambda.Parameters : null;
            Expression provider = this.Find(expression, parameters, typeof(EntityProvider));

            Expression rootQueryable = null;
            if (provider == null) {
                rootQueryable = this.Find(expression, parameters, typeof(IQueryable));
                if (rootQueryable != null || translation.NodeType != ExpressionType.Constant) {
                    provider = Expression.Property(rootQueryable, typeof(IQueryable).GetProperty("Provider"));
                } 
            }

            return translator.Police.BuildExecutionPlan(translation, provider, rootQueryable);
        }

        private Expression Find(Expression expression, IList<ParameterExpression> parameters, Type type)
        {
            if (parameters != null)
            {
                Expression found = parameters.FirstOrDefault(p => type.IsAssignableFrom(p.Type));
                if (found != null)
                    return found;
            }

            return TypedSubtreeFinder.Find(expression, type);
        }

        public static AttributeMapping GetMappingType<T>()
            => new AttributeMapping(typeof(T));
           
        public static QueryMapping GetMapping(string mappingId)
        {
            if (mappingId != null)
            {
                Type type = FindLoadedType(mappingId);
                if (type != null)
                {
                    return new AttributeMapping(type);
                }

                if (File.Exists(mappingId))
                {
                    return XmlMapping.FromXml(File.ReadAllText(mappingId));
                }
            }

            return new ImplicitMapping();
        }

        #endregion

        #region Types, Assemblies 

        public static Type GetProviderType(string providerName)
        {
            if (!string.IsNullOrEmpty(providerName))
            {
                var type = FindInstancesIn(typeof(EntityProvider), providerName).FirstOrDefault();
                if (type != null)
                    return type;
            }
            return null;
        }

        private static Type FindLoadedType(string typeName)
        {
            foreach (var assem in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assem.GetType(typeName, false, true);
                if (type != null)
                    return type;
            }
            return null;
        }

        private static IEnumerable<Type> FindInstancesIn(Type type, string assemblyName)
        {
            Assembly assembly = GetAssemblyForNamespace(assemblyName);
            if (assembly != null)
            {
                foreach (var atype in assembly.GetTypes())
                {
                    if (string.Compare(atype.Namespace, assemblyName, true) == 0
                        && type.IsAssignableFrom(atype))
                    {
                        yield return atype;
                    }
                }
            }
        }

        private static Assembly GetAssemblyForNamespace(string nspace)
        {
            foreach (var assem in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assem.FullName.Contains(nspace))
                {
                    return assem;
                }
            }

            return Load(nspace + ".dll");
        }

        private static Assembly Load(string name)
        {
            // try to load it.
            try
            {
                var fullName = Path.GetFullPath(name);
                return Assembly.LoadFrom(fullName);
            }
            catch
            {
            }
            return null;
        }

        #endregion
    }

    public static class UpdatableCall
    {
        private static MethodInfo GetCurrentMethodOf<T>(Expression<Func<T>> expression)
        {
            var body = (MethodCallExpression)expression.Body;
            return body.Method;
        }

#if SESSION
        // TODO: GetCurrentMethod
        public static object Insert(IUpdatable collection, object instance, LambdaExpression resultSelector)
        {
            MethodInfo method = (MethodInfo)MethodInfo.GetCurrentMethod();

            var callMyself = Expression.Call(
                null,
                method,
                collection.Expression,
                Expression.Constant(instance),
                resultSelector != null ? (Expression)Expression.Quote(resultSelector)
                    : Expression.Constant(null, typeof(LambdaExpression))
                );

            return collection.Provider.Execute(callMyself);
        }

        // Insert an copy of the instance into the updatable collection and produce a result if the insert succeeds.
        // <returns>The value of the result if the insert succeed, otherwise null.</returns>
        public static S Insert<T, S>(this IUpdatable<T> collection, T instance, Expression<Func<T, S>> resultSelector)
        {
            var callMyself = Expression.Call(
                null,
                ((MethodInfo)MethodInfo.GetCurrentMethod()).MakeGenericMethod(typeof(T), typeof(S)),
                collection.Expression,
                Expression.Constant(instance),
                resultSelector != null ? (Expression)Expression.Quote(resultSelector) : Expression.Constant(null, typeof(Expression<Func<T,S>>))
                );
            return (S)collection.Provider.Execute(callMyself);
        }

          public static int Insert<T>(this IUpdatable<T> collection, T instance)
        {
            return Insert<T, int>(collection, instance, null);
        }

        public static object Update(IUpdatable collection, object instance, LambdaExpression updateCheck, LambdaExpression resultSelector)
        {
            var callMyself = Expression.Call(
                null,
                (MethodInfo)MethodInfo.GetCurrentMethod(),
                collection.Expression,
                Expression.Constant(instance),
                updateCheck != null ? (Expression)Expression.Quote(updateCheck) : Expression.Constant(null, typeof(LambdaExpression)),
                resultSelector != null ? (Expression)Expression.Quote(resultSelector) : Expression.Constant(null, typeof(LambdaExpression))
                );
            return collection.Provider.Execute(callMyself);
        }

        // Update the object in the updatable collection with the values in this instance only if the update check passes and produce
         // <returns>The value of the result function if the update succeeds, otherwise null.</returns>
        public static S Update<T, S>(this IUpdatable<T> collection, T instance, Expression<Func<T, bool>> updateCheck, Expression<Func<T, S>> resultSelector)
        {
            var callMyself = Expression.Call(
                null,
                ((MethodInfo)MethodInfo.GetCurrentMethod()).MakeGenericMethod(typeof(T), typeof(S)),
                collection.Expression,
                Expression.Constant(instance),
                updateCheck != null ? (Expression)Expression.Quote(updateCheck) : Expression.Constant(null, typeof(Expression<Func<T, bool>>)),
                resultSelector != null ? (Expression)Expression.Quote(resultSelector) : Expression.Constant(null, typeof(Expression<Func<T, S>>))
                );
            return (S)collection.Provider.Execute(callMyself);
        }

         // Update the object in the updatable collection with the values in this instance only if the update check passes.
         // <returns>The value 1 if the update succeeds, otherwise 0.</returns>
        public static int Update<T>(this IUpdatable<T> collection, T instance, Expression<Func<T, bool>> updateCheck)
        {
            return Update<T, int>(collection, instance, updateCheck, null);
        }

        // Update the object in the updatable collection with the values in this instance.
         // <returns>The value 1 if the update succeeds, otherwise 0.</returns>
        public static int Update<T>(this IUpdatable<T> collection, T instance)
        {
            return Update<T, int>(collection, instance, null, null);
        }

        public static object InsertOrUpdate(IUpdatable collection, object instance, LambdaExpression updateCheck, LambdaExpression resultSelector)
        {
            var callMyself = Expression.Call(
                null,
                (MethodInfo)MethodInfo.GetCurrentMethod(),
                collection.Expression,
                Expression.Constant(instance),
                updateCheck != null ? (Expression)Expression.Quote(updateCheck) : Expression.Constant(null, typeof(LambdaExpression)),
                resultSelector != null ? (Expression)Expression.Quote(resultSelector) : Expression.Constant(null, typeof(LambdaExpression))
                );
            return collection.Provider.Execute(callMyself);
        }

        // Insert a copy of the instance if it does not exist in the collection or update the object in the collection with the values in this instance. 
         // <returns>The value of the result if the insert or update succeeds, otherwise null.</returns>
        public static S InsertOrUpdate<T, S>(this IUpdatable<T> collection, T instance, Expression<Func<T, bool>> updateCheck, Expression<Func<T, S>> resultSelector)
        {
            var callMyself = Expression.Call(
                null,
                ((MethodInfo)MethodInfo.GetCurrentMethod()).MakeGenericMethod(typeof(T), typeof(S)),
                collection.Expression,
                Expression.Constant(instance),
                updateCheck != null ? (Expression)Expression.Quote(updateCheck) : Expression.Constant(null, typeof(Expression<Func<T, bool>>)),
                resultSelector != null ? (Expression)Expression.Quote(resultSelector) : Expression.Constant(null, typeof(Expression<Func<T, S>>))
                );
            return (S)collection.Provider.Execute(callMyself);
        }

        // Insert a copy of the instance if it does not exist in the collection or update the object in the collection with the values in this instance. 
         // <returns>The value 1 if the insert or update succeeds, otherwise 0.</returns>
        public static int InsertOrUpdate<T>(this IUpdatable<T> collection, T instance, Expression<Func<T, bool>> updateCheck)
        {
            return InsertOrUpdate<T, int>(collection, instance, updateCheck, null);
        }

         // Insert a copy of the instance if it does not exist in the collection or update the object in the collection with the values in this instance. 
         // <returns>The value 1 if the insert or update succeeds, otherwise 0.</returns>
        public static int InsertOrUpdate<T>(this IUpdatable<T> collection, T instance)
        {
            return InsertOrUpdate<T, int>(collection, instance, null, null);
        }

        public static object Delete(IUpdatable collection, object instance, LambdaExpression deleteCheck)
        {
            var callMyself = Expression.Call(
                null,
                (MethodInfo)MethodInfo.GetCurrentMethod(),
                collection.Expression,
                Expression.Constant(instance),
                deleteCheck != null ? (Expression)Expression.Quote(deleteCheck) : Expression.Constant(null, typeof(LambdaExpression))
                );
            return collection.Provider.Execute(callMyself);
        }

         // Delete the object in the collection that matches the instance only if the delete check passes.
         // <returns>The value 1 if the delete succeeds, otherwise 0.</returns>
        public static int Delete<T>(this IUpdatable<T> collection, T instance, Expression<Func<T, bool>> deleteCheck)
        {
            var callMyself = Expression.Call(
                null,
                ((MethodInfo)MethodInfo.GetCurrentMethod()).MakeGenericMethod(typeof(T)),
                collection.Expression,
                Expression.Constant(instance),
                deleteCheck != null ? (Expression)Expression.Quote(deleteCheck) : Expression.Constant(null, typeof(Expression<Func<T, bool>>))
                );
            return (int)collection.Provider.Execute(callMyself);
        }

        // <returns>The value 1 if the Delete succeeds, otherwise 0.</returns>
        public static int Delete<T>(this IUpdatable<T> collection, T instance)
        {
            return Delete<T>(collection, instance, null);
        }

        public static int Delete(IUpdatable collection, LambdaExpression predicate)
        {
            var callMyself = Expression.Call(
                null,
                ((MethodInfo)MethodInfo.GetCurrentMethod()),
                collection.Expression,
                predicate != null ? (Expression)Expression.Quote(predicate) : Expression.Constant(null, typeof(LambdaExpression))
                );
            return (int)collection.Provider.Execute(callMyself);
        }

            public static int Delete<T>(this IUpdatable<T> collection, Expression<Func<T, bool>> predicate)
        {
            var callMyself = Expression.Call(
                null,
                ((MethodInfo)MethodInfo.GetCurrentMethod()).MakeGenericMethod(typeof(T)),
                collection.Expression,
                predicate != null ? (Expression)Expression.Quote(predicate) : Expression.Constant(null, typeof(Expression<Func<T, bool>>))
                );
            return (int)collection.Provider.Execute(callMyself);
        }

        public static IEnumerable Batch(IUpdatable collection, IEnumerable items, LambdaExpression fnOperation, int batchSize, bool stream)
        {
            var callMyself = Expression.Call(
                null,
                ((MethodInfo)MethodInfo.GetCurrentMethod()),
                collection.Expression,
                Expression.Constant(items),
                fnOperation != null ? (Expression)Expression.Quote(fnOperation) : Expression.Constant(null, typeof(LambdaExpression)),
                Expression.Constant(batchSize),
                Expression.Constant(stream)
                );

            return (IEnumerable)collection.Provider.Execute(callMyself);
        }
        
        // <returns>A sequence of results cooresponding to each invocation.</returns>
        public static IEnumerable<S> Batch<U,T,S>(this IUpdatable<U> collection, IEnumerable<T> instances,
               Expression<Func<IUpdatable<U>, T, S>> fnOperation, int batchSize, bool stream)
        {
            var callMyself = Expression.Call(
                null,
                ((MethodInfo)MethodInfo.GetCurrentMethod()).MakeGenericMethod(typeof(U), typeof(T), typeof(S)),
                collection.Expression,
                Expression.Constant(instances),
                fnOperation != null ? (Expression)Expression.Quote(fnOperation) : Expression.Constant(null, typeof(Expression<Func<IUpdatable<U>, T, S>>)),
                Expression.Constant(batchSize),
                Expression.Constant(stream)
                );
            return (IEnumerable<S>)collection.Provider.Execute(callMyself);
        }

        public static IEnumerable<S> Batch<U, T, S>(this IUpdatable<U> collection, IEnumerable<T> instances, Expression<Func<IUpdatable<U>, T, S>> fnOperation)
        {
            return Batch<U, T, S>(collection, instances, fnOperation, 50, false);
        }
#endif
    }

}
