// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query.Internal;
using JetBrains.Annotations;
using Remotion.Linq.Clauses.Expressions;
using Remotion.Linq.Parsing;
using Remotion.Linq;
using Remotion.Linq.Clauses;

namespace Microsoft.EntityFrameworkCore.Query.ExpressionVisitors.Internal
{
    /// <summary>
    ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public class CorrelatedCollectionFindingExpressionVisitor : RelinqExpressionVisitor
    {
        private EntityQueryModelVisitor _queryModelVisitor;
        private CorrelatedSubqueryOptimizationValidator _validator;

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public CorrelatedCollectionFindingExpressionVisitor([NotNull] EntityQueryModelVisitor queryModelVisitor)
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
            if (_queryModelVisitor.QueryCompilationContext.IsAsyncQuery)
            {
                return expression;
            }

            var subQueryModel = expression.QueryModel;

            subQueryModel.SelectClause.TransformExpressions(Visit);

            if (_validator == null)
            {
                _validator = new CorrelatedSubqueryOptimizationValidator();
            }

            if (_validator.CanTryOptimizeCorreltedSubquery(subQueryModel))
            {
                // if the query passes validation it becomes a candidate for future optimization
                // optimiation can't always be performed, e.g. when client-eval is needed
                // but we need to collect metadata (i.e. INavigations) before nav rewrite converts them into joins
                var newExpression = _queryModelVisitor.BindNavigationPathPropertyExpression(
                    subQueryModel.MainFromClause.FromExpression,
                    (properties, querySource) =>
                    {
                        var collectionNavigation = properties.OfType<INavigation>().SingleOrDefault(n => n.IsCollection());

                        if (collectionNavigation != null)
                        {
                            _queryModelVisitor.QueryCompilationContext.CorrelatedSubqueryMetadataMap[subQueryModel.MainFromClause] = new CorrelatedSubqueryMetadata
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

                // first pass finds all the query sources defined in this scope (i.e. from clauses)
                var declaredQuerySourcesFinder = new DefinedQuerySourcesFindingVisitor();
                declaredQuerySourcesFinder.VisitQueryModel(queryModel);

                // second pass makes sure that all qsres reference only query sources that were discovered in the first step, i.e. nothing from the outside
                var qsreScopeValidator = new ReferencedQuerySourcesScopeValidatingVisitor(queryModel.MainFromClause, declaredQuerySourcesFinder.QuerySources);
                qsreScopeValidator.VisitQueryModel(queryModel);

                return qsreScopeValidator.AllQuerySourceReferencesInScope;
            }

            private class DefinedQuerySourcesFindingVisitor : QueryModelVisitorBase
            {
                public ISet<IQuerySource> QuerySources { get; } = new HashSet<IQuerySource>();

                public override void VisitQueryModel(QueryModel queryModel)
                {
                    queryModel.TransformExpressions(new TransformingQueryModelExpressionVisitor<DefinedQuerySourcesFindingVisitor>(this).Visit);

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

            private class ReferencedQuerySourcesScopeValidatingVisitor : QueryModelVisitorBase
            {
                private class InnerVisitor : TransformingQueryModelExpressionVisitor<ReferencedQuerySourcesScopeValidatingVisitor>
                {
                    private ISet<IQuerySource> _querySourcesInScope;

                    public InnerVisitor(ISet<IQuerySource> querySourcesInScope, ReferencedQuerySourcesScopeValidatingVisitor transformingQueryModelVisitor)
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

                public ReferencedQuerySourcesScopeValidatingVisitor(IQuerySource exemptQuerySource, ISet<IQuerySource> querySourcesInScope)
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
                    // it is not necessary to visit result ops at the moment, since we don't optimize subqueries that contain any result ops
                    // however, we might support some result ops in the future
                    resultOperator.TransformExpressions(_innerVisitor.Visit);
                }
            }
        }
    }
}
