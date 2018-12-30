// Copyright (c) Microsoft Corporation.  All rights reserved.
// This source code is made available under the terms of the Microsoft Public License (MS-PL)

using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace IQToolkit
{
    /// <summary>
    /// A basic abstract LINQ query provider
    /// </summary>
    public abstract class QueryProvider : IQueryProvider, IQueryText
    {
        protected QueryProvider()
        {
        }

        /// <summary>
        /// IQueryProvider.CreateQuery<S>
        /// </summary>
        /// <typeparam name="S"></typeparam>
        /// <param name="expression"></param>
        /// <returns></returns>
        public virtual IQueryable<S> CreateQuery<S>(Expression expression)
        {
            return new Query<S>(this, expression);
        }

        /// <summary>
        /// IQueryProvider.CreateQuery
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        public virtual IQueryable CreateQuery(Expression expression)
        {
            if (expression == null) {
                return null;    // or ???? throw new ArgumentNullException()
            }

            Type elementType = TypeHelper.GetElementType(expression.Type);
            try
            {
                return (IQueryable)Activator.CreateInstance(typeof(Query<>)
                      .MakeGenericType(elementType), new object[] { this, expression });
            }
            catch (TargetInvocationException tie)
            {
                throw tie.InnerException;
            }
        }

        /// <summary>
        /// IQueryProvider.Execute
        /// </summary>
        /// <typeparam name="S"></typeparam>
        /// <param name="expression"></param>
        /// <returns></returns>
        public virtual S Execute<S>(Expression expression)
        {
            return (S)this.ExecuteExpr(expression);
        }

        /// <summary>
        /// IQueryProvider.
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        public virtual object Execute(Expression expression)
        {
            return this.ExecuteExpr(expression);
        }

        public abstract string GetQueryText(Expression expression);

        public abstract object ExecuteExpr(Expression expression);
    }
}
