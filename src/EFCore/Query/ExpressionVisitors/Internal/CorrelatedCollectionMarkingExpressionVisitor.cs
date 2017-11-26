// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Remotion.Linq.Clauses.Expressions;
using Remotion.Linq.Parsing;
using Remotion.Linq;
using Remotion.Linq.Clauses;
using System.Collections.ObjectModel;

namespace Microsoft.EntityFrameworkCore.Query.ExpressionVisitors.Internal
{
    /// <summary>
    ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public class CorrelatedCollectionFindingExpressionVisitor : RelinqExpressionVisitor
    {
        private EntityQueryModelVisitor _queryModelVisitor;

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public CorrelatedCollectionFindingExpressionVisitor(EntityQueryModelVisitor queryModelVisitor)
        {
            _queryModelVisitor = queryModelVisitor;
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.Name.StartsWith(nameof(IQueryBuffer.IncludeCollection), StringComparison.Ordinal))
            {
                return node;
            }

            return base.VisitMethodCall(node);
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        protected override Expression VisitSubQuery(SubQueryExpression expression)
        {
            // TODO: add property for this?
            if (_queryModelVisitor.QueryCompilationContext.LinqOperatorProvider.Select.ReturnType.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>))
            {
                return expression;
            }

            var subQueryModel = expression.QueryModel;

            // TODO: uncomment this to enable nested correlated collections (currently buggy for edge case scenarios)
            subQueryModel.SelectClause.TransformExpressions(Visit);

            var validator = new CorrelatedSubqueryOptimizationValidator();
            if (validator.CanTryOptimizeCorreltedSubquery(subQueryModel))
            { 

            //if (subQueryModel.ResultOperators.Count == 0)
            //{
            //    var querySourceReferenceFindingExpressionTreeVisitor
            //        = new QuerySourceReferenceFindingExpressionTreeVisitor2(subQueryModel.MainFromClause);

            //    querySourceReferenceFindingExpressionTreeVisitor.Visit(subQueryModel.SelectClause.Selector);

            //    if (querySourceReferenceFindingExpressionTreeVisitor.QuerySourceFound)
                {
                    var newExpression = _queryModelVisitor.BindNavigationPathPropertyExpression(
                        subQueryModel.MainFromClause.FromExpression,
                        (properties, querySource) =>
                        {
                            var collectionNavigation = properties.OfType<INavigation>().SingleOrDefault(n => n.IsCollection());

                            if (collectionNavigation != null)
                            {
                                _queryModelVisitor.QueryCompilationContext.CorrelatedSubqueryMetadataMap[subQueryModel] = new QueryCompilationContext.CorrelatedSubqueryMetadata
                                {
                                    FirstNavigation = properties.OfType<INavigation>().First(),
                                    CollectionNavigation = collectionNavigation,
                                    ParentQuerySource = querySource
                                };

                                return expression;
                            }

                            return default;
                        });

                    if (newExpression != null)
                    {
                        return newExpression;
                    }
                }
            }

            return base.VisitSubQuery(expression);
        }

        private class CorrelatedSubqueryOptimizationValidator
        {
            public bool CanTryOptimizeCorreltedSubquery(QueryModel queryModel)
            {
                if (queryModel.ResultOperators.Any())
                {
                    return false;
                }

                var querySourceReferenceFindingExpressionTreeVisitor
                    = new QuerySourceReferenceFindingExpressionTreeVisitor2(queryModel.MainFromClause);

                querySourceReferenceFindingExpressionTreeVisitor.Visit(queryModel.SelectClause.Selector);

                if (!querySourceReferenceFindingExpressionTreeVisitor.QuerySourceFound)
                {
                    return false;
                }

                // first pass finds all the query sources declared in this scope (i.e. from clauses)
                var foo = new Foo();
                foo.VisitQueryModel(queryModel);

                // second pass makes sure that all qsres reference only query sources that were discovered in the first step, i.e. nothing from the outside
                var bar = new Barr(queryModel.MainFromClause, foo.QuerySources);
                bar.VisitQueryModel(queryModel);

                return bar.AllQuerySourceReferencesInScope;
            }

            private class Foo : QueryModelVisitorBase
            {
                public ISet<IQuerySource> QuerySources { get; } = new HashSet<IQuerySource>();

                public override void VisitQueryModel(QueryModel queryModel)
                {
                    queryModel.TransformExpressions(new TransformingQueryModelExpressionVisitor<Foo>(this).Visit);

                    base.VisitQueryModel(queryModel);
                }

                public override void VisitMainFromClause(MainFromClause fromClause, QueryModel queryModel)
                {
                    QuerySources.Add(fromClause);

                    base.VisitMainFromClause(fromClause, queryModel);
                }

                public override void VisitAdditionalFromClause(AdditionalFromClause fromClause, QueryModel queryModel, int index)
                {
                    QuerySources.Add(fromClause);

                    base.VisitAdditionalFromClause(fromClause, queryModel, index);
                }
            }

            private class Barr : QueryModelVisitorBase
            {
                private class InnerVisitor : TransformingQueryModelExpressionVisitor<Barr>
                {
                    private ISet<IQuerySource> _querySourcesInScope;

                    public InnerVisitor(ISet<IQuerySource> querySourcesInScope, Barr transformingQueryModelVisitor)
                        : base(transformingQueryModelVisitor)
                    {
                        _querySourcesInScope = querySourcesInScope;
                    }

                    public bool AllQuerySourceReferencesInScope { get; private set; } = true;

                    protected override Expression VisitQuerySourceReference(QuerySourceReferenceExpression expression)
                    {
                        if (!_querySourcesInScope.Contains(expression.ReferencedQuerySource))
                        {
                            AllQuerySourceReferencesInScope = false;
                        }

                        return base.VisitQuerySourceReference(expression);
                    }
                }

                // query source that can reference something outside the scope, e.g. main from clause that contains the correlated navigation
                private IQuerySource _exemptQuerySource;
                private InnerVisitor _innerVisitor;

                public Barr(IQuerySource exemptQuerySource, ISet<IQuerySource> querySourcesInScope)
                {
                    _exemptQuerySource = exemptQuerySource;
                    _innerVisitor = new InnerVisitor(querySourcesInScope, this);
                }

                public bool AllQuerySourceReferencesInScope => _innerVisitor.AllQuerySourceReferencesInScope;

                public override void VisitMainFromClause(MainFromClause fromClause, QueryModel queryModel)
                {
                    if (fromClause != _exemptQuerySource)
                    {
                        fromClause.TransformExpressions(_innerVisitor.Visit);
                    }
                }

                protected override void VisitBodyClauses(ObservableCollection<IBodyClause> bodyClauses, QueryModel queryModel)
                {
                    foreach (var bodyClause in bodyClauses)
                    {
                        if (bodyClause != _exemptQuerySource)
                        {
                            bodyClause.TransformExpressions(_innerVisitor.Visit);
                        }
                    }
                }

                public override void VisitSelectClause(SelectClause selectClause, QueryModel queryModel)
                {
                    selectClause.TransformExpressions(_innerVisitor.Visit);
                }

                public override void VisitResultOperator(ResultOperatorBase resultOperator, QueryModel queryModel, int index)
                {
                    // in theory, it is not necessary to visit result ops at the moment, since we don't optimize subqueries that contain any result ops
                    // however, we might support some result ops in the future
                    resultOperator.TransformExpressions(_innerVisitor.Visit);
                }
            }
        }
    }
}
