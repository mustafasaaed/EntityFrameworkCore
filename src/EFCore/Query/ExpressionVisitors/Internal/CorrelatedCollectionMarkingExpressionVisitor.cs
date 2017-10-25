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

            subQueryModel.SelectClause.TransformExpressions(Visit);

            if (subQueryModel.ResultOperators.Count == 0)
            {
                var querySourceReferenceFindingExpressionTreeVisitor
                    = new QuerySourceReferenceFindingExpressionTreeVisitor2(subQueryModel.MainFromClause);

                querySourceReferenceFindingExpressionTreeVisitor.Visit(subQueryModel.SelectClause.Selector);

                if (querySourceReferenceFindingExpressionTreeVisitor.QuerySourceFound)
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
    }
}
