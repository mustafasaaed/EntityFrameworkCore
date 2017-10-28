//// Copyright (c) .NET Foundation. All rights reserved.
//// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

//using System;
//using System.Linq.Expressions;
//using Microsoft.EntityFrameworkCore.Metadata;
//using Remotion.Linq.Clauses;
//using Remotion.Linq.Clauses.Expressions;

//namespace Microsoft.EntityFrameworkCore.Query.Expressions.Internal
//{

//    /// <summary>
//    ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
//    ///     directly from your code. This API may change or be removed in future releases.
//    /// </summary>
//    public class CorrelatedCollectionMarkingExpression : Expression
//    {
//        private readonly Type _type;

//        /// <summary>
//        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
//        ///     directly from your code. This API may change or be removed in future releases.
//        /// </summary>
//        public CorrelatedCollectionMarkingExpression(
//            SubQueryExpression operand,
//            IQuerySource parentQuerySource,
//            INavigation firstNavigation,
//            INavigation lastNavigation)
//        {
//            Operand = operand;
//            ParentQuerySource = parentQuerySource;
//            FirstNavigation = firstNavigation;
//            LastNavigation = lastNavigation;
//            _type = operand.Type;
//        }

//        /// <summary>
//        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
//        ///     directly from your code. This API may change or be removed in future releases.
//        /// </summary>
//        public virtual SubQueryExpression Operand { get; }

//        /// <summary>
//        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
//        ///     directly from your code. This API may change or be removed in future releases.
//        /// </summary>
//        public virtual IQuerySource ParentQuerySource { get; set; }

//        /// <summary>
//        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
//        ///     directly from your code. This API may change or be removed in future releases.
//        /// </summary>
//        public virtual INavigation FirstNavigation { get; }

//        /// <summary>
//        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
//        ///     directly from your code. This API may change or be removed in future releases.
//        /// </summary>
//        public virtual INavigation LastNavigation { get; }

//        /// <summary>
//        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
//        ///     directly from your code. This API may change or be removed in future releases.
//        /// </summary>
//        public override bool CanReduce => true;

//        /// <summary>
//        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
//        ///     directly from your code. This API may change or be removed in future releases.
//        /// </summary>
//        public override Type Type => _type;

//        /// <summary>
//        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
//        ///     directly from your code. This API may change or be removed in future releases.
//        /// </summary>
//        public override ExpressionType NodeType => ExpressionType.Extension;

//        /// <summary>
//        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
//        ///     directly from your code. This API may change or be removed in future releases.
//        /// </summary>
//        public override Expression Reduce()
//            => Operand;

//        /// <summary>
//        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
//        ///     directly from your code. This API may change or be removed in future releases.
//        /// </summary>
//        protected override Expression VisitChildren(ExpressionVisitor visitor)
//            => this;

//        /// <summary>
//        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
//        ///     directly from your code. This API may change or be removed in future releases.
//        /// </summary>
//        public override string ToString()
//            => $"{nameof(CorrelatedCollectionMarkingExpression)}({Operand})";
//    }
//}
