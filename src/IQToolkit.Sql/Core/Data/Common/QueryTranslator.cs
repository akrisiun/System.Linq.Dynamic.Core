// Copyright (c) Microsoft Corporation.  All rights reserved.
// This source code is made available under the terms of the Microsoft Public License (MS-PL)

using System;
using System.Linq;
using System.Linq.Expressions;

namespace IQToolkit.Data.Common
{
    public interface IQueryTranslator
    {
        QueryLinguist Linguist { get; }
        QueryMapper Mapper { get; }
        QueryPolice Police { get; }
        IQuery Query { get; set; }

        Expression Translate(Expression expression);
    }

    /// <summary>
    /// Defines query execution and materialization policies. 
    /// </summary>
    public class QueryTranslator : IQueryTranslator
    {
        QueryLinguist linguist;
        QueryMapper mapper;
        QueryPolice police;

        public QueryTranslator(QueryLanguage language, QueryMapping mapping, QueryPolicy policy)
        {
            this.linguist = language.CreateLinguist(this);
            this.mapper = mapping.CreateMapper(this);
            this.police = policy.CreatePolice(this);
        }

        public IQuery Query { get; set; }

        public QueryLinguist Linguist
        {
            get { return this.linguist; }
        }

        public QueryMapper Mapper
        {
            get { return this.mapper; }
        }

        public QueryPolice Police
        {
            get { return this.police; }
        }

        public virtual Expression Translate(Expression expression)
        {
            // pre-evaluate local sub-trees
            expression = PartialEvaluator.Eval(expression, this.mapper.Mapping.CanBeEvaluatedLocally);

            // apply mapping (binds LINQ operators too)
            expression = this.mapper.Translate(expression);

            // any policy specific translations or validations
            expression = this.police.Translate(expression);

            // any language specific translations or validations
            expression = this.linguist.Translate(expression);

            if (expression is ProjectionExpression) {
                // var rootQueryable = this.Find(expression, parameters, typeof(IQueryable));
                // var queryConst = Expression.Constant(this.Query);
                // var proj = new ProjectionExpression(null, expression, this.Query);
                // proj.QueryText = proj.Projector.ToString();
                (expression as ProjectionExpression).Query = this.Query;
            }

            return expression;
        }
    }
}