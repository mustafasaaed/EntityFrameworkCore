// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq.Expressions;
using Remotion.Linq.Clauses.Expressions;
using Remotion.Linq;

namespace Microsoft.EntityFrameworkCore.Query.ExpressionVisitors.Internal
{
    /// <summary>
    ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public class SubqueryUniquefyingExpressionVisitor : ExpressionVisitorBase
    {
        private List<QueryModel> _visitedSubqueries = new List<QueryModel>();

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        protected override Expression VisitSubQuery(SubQueryExpression expression)
        {
            if (_visitedSubqueries.Contains(expression.QueryModel))
            {
                var clonedQueryModel = expression.QueryModel.Clone();

                return base.VisitSubQuery(new SubQueryExpression(clonedQueryModel));
            }
            else
            {
                _visitedSubqueries.Add(expression.QueryModel);
            }

            return base.VisitSubQuery(expression);
        }
    }
}
