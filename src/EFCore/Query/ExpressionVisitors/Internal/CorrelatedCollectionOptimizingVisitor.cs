// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Extensions.Internal;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Query.Expressions.Internal;
using Microsoft.EntityFrameworkCore.Query.Internal;
using JetBrains.Annotations;
using Remotion.Linq;
using Remotion.Linq.Clauses;
using Remotion.Linq.Clauses.Expressions;
using Remotion.Linq.Clauses.ExpressionVisitors;
using System.Diagnostics;

namespace Microsoft.EntityFrameworkCore.Query.ExpressionVisitors.Internal
{
    /// <summary>
    ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public class CorrelatedCollectionOptimizingVisitor : ExpressionVisitorBase
    {
        private readonly EntityQueryModelVisitor _queryModelVisitor;
        private readonly QueryCompilationContext _queryCompilationContext;
        private readonly QueryModel _parentQueryModel;

        private static readonly ExpressionEqualityComparer _expressionEqualityComparer
            = new ExpressionEqualityComparer();

        private static readonly MethodInfo _toListMethodInfo
            = typeof(Enumerable).GetTypeInfo().GetDeclaredMethod(nameof(Enumerable.ToList));

        private static readonly MethodInfo _toArrayMethodInfo
            = typeof(Enumerable).GetTypeInfo().GetDeclaredMethod(nameof(Enumerable.ToArray));

        private static MethodInfo _correlateSubqueryMethodInfo
            = typeof(IQueryBuffer).GetMethod(nameof(IQueryBuffer.CorrelateSubquery));

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public CorrelatedCollectionOptimizingVisitor(
            [NotNull] EntityQueryModelVisitor queryModelVisitor,
            [NotNull] QueryModel parentQueryModel)
        {
            _queryModelVisitor = queryModelVisitor;
            _queryCompilationContext = queryModelVisitor.QueryCompilationContext;
            _parentQueryModel = parentQueryModel;
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public virtual List<Ordering> ParentOrderings { get; } = new List<Ordering>();

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        protected override Expression VisitSubQuery(SubQueryExpression subQueryExpression) => subQueryExpression;

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            SubQueryExpression subQueryExpression = null;
            if ((node.Method.MethodIsClosedFormOf(_toListMethodInfo) || node.Method.MethodIsClosedFormOf(_toArrayMethodInfo))
                && node.Arguments[0] is SubQueryExpression)
            {
                subQueryExpression = (SubQueryExpression)node.Arguments[0];
            }

            if (node.Method.MethodIsClosedFormOf(CollectionNavigationSubqueryInjector.MaterializeCollectionNavigationMethodInfo)
                && node.Arguments[1] is SubQueryExpression)
            {
                subQueryExpression = (SubQueryExpression)node.Arguments[1];
            }

            if (subQueryExpression != null
                && _queryCompilationContext.CorrelatedSubqueryMetadataMap.TryGetValue(subQueryExpression.QueryModel, out var correlatedSubqueryMetadata))
            {
                var parentQsre = new QuerySourceReferenceExpression(correlatedSubqueryMetadata.ParentQuerySource);
                var correlatedCollectionIndex = _queryCompilationContext.CorrelatedSubqueryMetadataMap.Keys.IndexOf(subQueryExpression.QueryModel);

                var result = Rewrite(correlatedCollectionIndex, subQueryExpression.QueryModel, correlatedSubqueryMetadata.CollectionNavigation, parentQsre);

                return node.Method.MethodIsClosedFormOf(CollectionNavigationSubqueryInjector.MaterializeCollectionNavigationMethodInfo)
                    ? node.Update(node.Object, new[] { node.Arguments[0], result })
                    : node.Update(node.Object, new[] { result });
            }

            return base.VisitMethodCall(node);
        }

        ///// <summary>
        /////     This API supports the Entity Framework Core infrastructure and is not intended to be used
        /////     directly from your code. This API may change or be removed in future releases.
        ///// </summary>
        //protected override Expression VisitSubQuery(SubQueryExpression subQueryExpression)
        //{
        //    if (_queryCompilationContext.CorrelatedSubqueryMetadataMap.TryGetValue(subQueryExpression.QueryModel, out var correlatedSubqueryMetadata))
        //    {
        //        var parentQsre = new QuerySourceReferenceExpression(correlatedSubqueryMetadata.ParentQuerySource);
        //        var correlatedCollectionIndex = _queryCompilationContext.CorrelatedSubqueryMetadataMap.Keys.IndexOf(subQueryExpression.QueryModel);
        //        var result = Rewrite(correlatedCollectionIndex, subQueryExpression.QueryModel, correlatedSubqueryMetadata.CollectionNavigation, parentQsre);

        //        result = Expression.Call(_toListMethodInfo.MakeGenericMethod(result.Type.GetGenericArguments()[0]), result);

        //        if (subQueryExpression.Type.IsGenericType
        //            && subQueryExpression.Type.GetGenericTypeDefinition() == typeof(IOrderedEnumerable<>))
        //        {
        //            return
        //                Expression.Call(
        //                    _queryCompilationContext.LinqOperatorProvider.ToOrdered
        //                        .MakeGenericMethod(result.Type.GetGenericArguments()[0]),
        //                    result);
        //        }

        //        return result;
        //    }

        //    return base.VisitSubQuery(subQueryExpression);
        //}

        private Expression Rewrite(int correlatedCollectionIndex, QueryModel collectionQueryModel, INavigation navigation, QuerySourceReferenceExpression originQuerySource)
        {
            var querySourceReferenceFindingExpressionTreeVisitor
                = new QuerySourceReferenceFindingExpressionTreeVisitor();

            var originalCorrelationPredicate = collectionQueryModel.BodyClauses.OfType<WhereClause>().Single(c => c.Predicate is NullConditionalEqualExpression);
            collectionQueryModel.BodyClauses.Remove(originalCorrelationPredicate);

            originalCorrelationPredicate.TransformExpressions(querySourceReferenceFindingExpressionTreeVisitor.Visit);
            var parentQuerySourceReferenceExpression = querySourceReferenceFindingExpressionTreeVisitor.QuerySourceReferenceExpression;

            querySourceReferenceFindingExpressionTreeVisitor = new QuerySourceReferenceFindingExpressionTreeVisitor();
            querySourceReferenceFindingExpressionTreeVisitor.Visit(((NullConditionalEqualExpression)originalCorrelationPredicate.Predicate).InnerKey);

            var currentKey = BuildKeyAccess(navigation.ForeignKey.Properties, querySourceReferenceFindingExpressionTreeVisitor.QuerySourceReferenceExpression);

            // PK of the parent qsre
            var originKey = BuildKeyAccess(_queryCompilationContext.Model.FindEntityType(originQuerySource.Type).FindPrimaryKey().Properties, originQuerySource);

            // principal side of the FK relationship between parent and this collection
            var outerKey = BuildKeyAccess(navigation.ForeignKey.PrincipalKey.Properties, parentQuerySourceReferenceExpression);

            var parentQuerySource = parentQuerySourceReferenceExpression.ReferencedQuerySource;

            // ordering priority for parent:
            // - customer specified orderings
            // - parent PK
            // - principal side of the FK between parent and child

            // ordering priority for child:
            // - customer specified orderings on parent (from join)
            // - parent PK (from join)
            // - dependent side of the FK between parent and child
            // - customer specified orderings on child

            var parentOrderings = new List<Ordering>();
            var exisingParentOrderByClause = _parentQueryModel.BodyClauses.OfType<OrderByClause>().LastOrDefault();
            if (exisingParentOrderByClause != null)
            {
                parentOrderings.AddRange(exisingParentOrderByClause.Orderings);
            }

            var originEntityType = _queryCompilationContext.Model.FindEntityType(originQuerySource.Type);
            foreach (var property in originEntityType.FindPrimaryKey().Properties)
            {
                TryAddPropertyToOrderings(property, originQuerySource, parentOrderings);
            }

            foreach (var property in navigation.ForeignKey.PrincipalKey.Properties)
            {
                TryAddPropertyToOrderings(property, parentQuerySourceReferenceExpression, parentOrderings);
            }

            // TODO: except the existing ones?
            ParentOrderings.AddRange(parentOrderings);

            var querySourceMapping = new QuerySourceMapping();
            var clonedParentQueryModel = _parentQueryModel.Clone(querySourceMapping);
            _queryCompilationContext.UpdateMapping(querySourceMapping);
            _queryCompilationContext.CloneAnnotations(querySourceMapping, clonedParentQueryModel);

            var clonedParentQuerySourceReferenceExpression
                = (QuerySourceReferenceExpression)querySourceMapping.GetExpression(parentQuerySource);

            var clonedParentQuerySource
                = clonedParentQuerySourceReferenceExpression.ReferencedQuerySource;

            var parentItemName
                = parentQuerySource.HasGeneratedItemName()
                    ? navigation.DeclaringEntityType.DisplayName()[0].ToString().ToLowerInvariant()
                    : parentQuerySource.ItemName;

            collectionQueryModel.MainFromClause.ItemName = $"{parentItemName}.{navigation.Name}";

            var collectionQuerySourceReferenceExpression
                = new QuerySourceReferenceExpression(collectionQueryModel.MainFromClause);

            var subQueryProjection = new List<Expression>();
            subQueryProjection.AddRange(parentOrderings.Select(o => CloningExpressionVisitor.AdjustExpressionAfterCloning(o.Expression, querySourceMapping)));

            var joinQuerySourceReferenceExpression
                = CreateJoinToParentQuery(
                    clonedParentQueryModel,
                    clonedParentQuerySourceReferenceExpression,
                    collectionQuerySourceReferenceExpression,
                    navigation.ForeignKey,
                    collectionQueryModel,
                    subQueryProjection);

            ApplyParentOrderings(
                parentOrderings,
                clonedParentQueryModel,
                querySourceMapping);

            LiftOrderBy(
                clonedParentQuerySource,
                joinQuerySourceReferenceExpression,
                clonedParentQueryModel,
                collectionQueryModel,
                subQueryProjection);

            clonedParentQueryModel.SelectClause.Selector
                = Expression.New(
                    MaterializedAnonymousObject.AnonymousObjectCtor,
                    Expression.NewArrayInit(
                        typeof(object),
                        subQueryProjection.Select(e => Expression.Convert(e, typeof(object)))));

            clonedParentQueryModel.ResultTypeOverride = typeof(IQueryable<>).MakeGenericType(clonedParentQueryModel.SelectClause.Selector.Type);

            var newOriginKey = CloningExpressionVisitor
                    .AdjustExpressionAfterCloning(originKey, querySourceMapping);

            var newOriginKeyElements = ((NewArrayExpression)(((NewExpression)newOriginKey).Arguments[0])).Expressions;
            var remappedOriginKeyElements = RemapOriginKeyExpressions(newOriginKeyElements, joinQuerySourceReferenceExpression, subQueryProjection);

            var tupleCtor = typeof(Tuple<,,>).MakeGenericType(
                collectionQueryModel.SelectClause.Selector.Type,
                typeof(MaterializedAnonymousObject),
                typeof(MaterializedAnonymousObject)).GetConstructors().FirstOrDefault();

            var correlateSubqueryMethod = _correlateSubqueryMethodInfo.MakeGenericMethod(collectionQueryModel.SelectClause.Selector.Type);

            collectionQueryModel.SelectClause.Selector
                = Expression.New(
                    tupleCtor,
                    new Expression[]
                    {
                        collectionQueryModel.SelectClause.Selector,
                        currentKey,
                        Expression.New(
                            MaterializedAnonymousObject.AnonymousObjectCtor,
                            Expression.NewArrayInit(
                                typeof(object),
                                remappedOriginKeyElements))
                    });

            // Enumerable or OrderedEnumerable
            collectionQueryModel.ResultTypeOverride = collectionQueryModel.BodyClauses.OfType<OrderByClause>().Any()
                ? typeof(IOrderedEnumerable<>).MakeGenericType(collectionQueryModel.SelectClause.Selector.Type)
                : typeof(IEnumerable<>).MakeGenericType(collectionQueryModel.SelectClause.Selector.Type);

            // since we cloned QM, we need to check if it's query sources require materialization (e.g. TypeIs operation for InMemory)
            _queryCompilationContext.FindQuerySourcesRequiringMaterialization(_queryModelVisitor, collectionQueryModel);

            var correlationPredicate = CreateCorrelationPredicate(navigation);

            var arguments = new List<Expression>
                    {
                        Expression.Constant(correlatedCollectionIndex),
                        Expression.Constant(navigation),
                        outerKey,
                        Expression.Lambda(new SubQueryExpression(collectionQueryModel)),
                        correlationPredicate
                    };

            var result = Expression.Call(
                Expression.Property(
                    EntityQueryModelVisitor.QueryContextParameter,
                    nameof(QueryContext.QueryBuffer)),
                correlateSubqueryMethod,
                arguments);

            if (collectionQueryModel.ResultTypeOverride.GetGenericTypeDefinition() == typeof(IOrderedEnumerable<>))
            {
                return
                    Expression.Call(
                        _queryCompilationContext.LinqOperatorProvider.ToOrdered
                            .MakeGenericMethod(result.Type.GetSequenceType()),
                        result);
            }

            return result;
        }

        private static Expression BuildKeyAccess(IEnumerable<IProperty> keyProperties, Expression qsre)
        {
            var keyAccessExpressions = keyProperties.Select(p => new NullConditionalExpression(qsre, qsre.CreateEFPropertyExpression(p))).ToArray();

            return Expression.New(
                MaterializedAnonymousObject.AnonymousObjectCtor,
                Expression.NewArrayInit(
                    typeof(object),
                    keyAccessExpressions.Select(k => Expression.Convert(k, typeof(object)))));
        }

        private static Expression CreateCorrelationPredicate(INavigation navigation)
        {
            var foreignKey = navigation.ForeignKey;
            var primaryKeyProperties = foreignKey.PrincipalKey.Properties;
            var foreignKeyProperties = foreignKey.Properties;

            var outerKeyParameter = Expression.Parameter(typeof(MaterializedAnonymousObject), "o");
            var innerKeyParameter = Expression.Parameter(typeof(MaterializedAnonymousObject), "i");

            return Expression.Lambda(
                primaryKeyProperties
                    .Select((pk, i) => new { pk, i })
                    .Zip(
                        foreignKeyProperties,
                        (outer, inner) =>
                        {
                            var outerKeyAccess =
                                Expression.Call(
                                    outerKeyParameter,
                                    MaterializedAnonymousObject.GetValueMethodInfo,
                                    Expression.Constant(outer.i));

                            var typedOuterKeyAccess =
                                Expression.Convert(
                                    outerKeyAccess,
                                    primaryKeyProperties[outer.i].ClrType);

                            var innerKeyAccess =
                                Expression.Call(
                                    innerKeyParameter,
                                    MaterializedAnonymousObject.GetValueMethodInfo,
                                    Expression.Constant(outer.i));

                            var typedInnerKeyAccess =
                                Expression.Convert(
                                    innerKeyAccess,
                                    foreignKeyProperties[outer.i].ClrType);

                            Expression equalityExpression;
                            if (typedOuterKeyAccess.Type != typedInnerKeyAccess.Type)
                            {
                                if (typedOuterKeyAccess.Type.IsNullableType())
                                {
                                    typedInnerKeyAccess = Expression.Convert(typedInnerKeyAccess, typedOuterKeyAccess.Type);
                                }
                                else
                                {
                                    typedOuterKeyAccess = Expression.Convert(typedOuterKeyAccess, typedInnerKeyAccess.Type);
                                }
                            }

                            equalityExpression = Expression.Equal(typedOuterKeyAccess, typedInnerKeyAccess);

                            return 
                                (Expression)Expression.Condition(
                                    Expression.OrElse(
                                        Expression.Equal(innerKeyAccess, Expression.Default(innerKeyAccess.Type)),
                                        Expression.Equal(outerKeyAccess, Expression.Default(outerKeyAccess.Type))),
                                    Expression.Constant(false),
                                    equalityExpression);
                        })
                    .Aggregate((e1, e2) => Expression.AndAlso(e1, e2)),
                outerKeyParameter,
                innerKeyParameter);
        }

        private void TryAddPropertyToOrderings(
            IProperty property, 
            QuerySourceReferenceExpression propertyQsre, 
            ICollection<Ordering> orderings)
        {
            var propertyExpression = propertyQsre.CreateEFPropertyExpression(property);

            var orderingExpression = Expression.Convert(
                new NullConditionalExpression(
                    propertyQsre,
                    propertyExpression),
                propertyExpression.Type);

            if (!orderings.Any(
                o => _expressionEqualityComparer.Equals(o.Expression, orderingExpression)
                     || (o.Expression.RemoveConvert() is MemberExpression memberExpression1
                         && propertyExpression is MethodCallExpression methodCallExpression
                         && MatchEfPropertyToMemberExpression(memberExpression1, methodCallExpression))
                     || (o.Expression.RemoveConvert() is NullConditionalExpression nullConditionalExpression
                         && nullConditionalExpression.AccessOperation is MemberExpression memberExpression
                         && propertyExpression is MethodCallExpression methodCallExpression1
                         && MatchEfPropertyToMemberExpression(memberExpression, methodCallExpression1))))
            {
                orderings.Add(new Ordering(orderingExpression, OrderingDirection.Asc));
            }
        }

        private static bool MatchEfPropertyToMemberExpression(MemberExpression memberExpression, MethodCallExpression methodCallExpression)
        {
            if (methodCallExpression.IsEFProperty())
            {
                var propertyName = (string)((ConstantExpression)methodCallExpression.Arguments[1]).Value;

                return memberExpression.Member.Name.Equals(propertyName)
                       && _expressionEqualityComparer.Equals(memberExpression.Expression, methodCallExpression.Arguments[0]);
            }

            return false;
        }

        private QuerySourceReferenceExpression CreateJoinToParentQuery(
            QueryModel parentQueryModel,
            QuerySourceReferenceExpression parentQuerySourceReferenceExpression,
            Expression outerTargetExpression,
            IForeignKey foreignKey,
            QueryModel targetQueryModel,
            List<Expression> subQueryProjection)
        {
            var subQueryExpression = new SubQueryExpression(parentQueryModel);
            var parentQuerySource = parentQuerySourceReferenceExpression.ReferencedQuerySource;

            var joinClause
                = new JoinClause(
                    "_" + parentQuerySource.ItemName,
                    typeof(MaterializedAnonymousObject),
                    subQueryExpression,
                    outerTargetExpression.CreateKeyAccessExpression(foreignKey.Properties),
                    Expression.Constant(null));

            var joinQuerySourceReferenceExpression = new QuerySourceReferenceExpression(joinClause);
            var innerKeyExpressions = new List<Expression>();

            foreach (var principalKeyProperty in foreignKey.PrincipalKey.Properties)
            {
                var index = subQueryProjection.FindIndex(
                    e =>
                    {
                        var expressionWithoutConvert = e.RemoveConvert();
                        var projectionExpression = (expressionWithoutConvert as NullConditionalExpression)?.AccessOperation
                                                   ?? expressionWithoutConvert;

                        if (projectionExpression is MethodCallExpression methodCall
                            && methodCall.Method.IsEFPropertyMethod())
                        {
                            var propertyQsre = (QuerySourceReferenceExpression)methodCall.Arguments[0];
                            var propertyName = (string)((ConstantExpression)methodCall.Arguments[1]).Value;
                            var propertyQsreEntityType = _queryCompilationContext.FindEntityType(propertyQsre.ReferencedQuerySource)
                                ?? _queryCompilationContext.Model.FindEntityType(propertyQsre.Type);

                            return propertyQsreEntityType.RootType() == principalKeyProperty.DeclaringEntityType.RootType() 
                                && propertyName == principalKeyProperty.Name;
                        }

                        if (projectionExpression is MemberExpression projectionMemberExpression)
                        {
                            var projectionMemberQsre = (QuerySourceReferenceExpression)projectionMemberExpression.Expression;
                            var projectionMemberQsreEntityType = _queryCompilationContext.FindEntityType(projectionMemberQsre.ReferencedQuerySource)
                                ?? _queryCompilationContext.Model.FindEntityType(projectionMemberQsre.Type);

                            return projectionMemberQsreEntityType.RootType() == principalKeyProperty.DeclaringEntityType.RootType()
                                && projectionMemberExpression.Member.Name == principalKeyProperty.Name;
                        }

                        return false;
                    });

                Debug.Assert(index != -1);

                innerKeyExpressions.Add(
                    Expression.Convert(
                        Expression.Call(
                            joinQuerySourceReferenceExpression,
                            MaterializedAnonymousObject.GetValueMethodInfo,
                            Expression.Constant(index)),
                        principalKeyProperty.ClrType.MakeNullable()));

                var propertyExpression
                    = parentQuerySourceReferenceExpression.CreateEFPropertyExpression(principalKeyProperty);
            }

            joinClause.InnerKeySelector
                = innerKeyExpressions.Count == 1
                    ? innerKeyExpressions[0]
                    : Expression.New(
                        AnonymousObject.AnonymousObjectCtor,
                        Expression.NewArrayInit(
                            typeof(object),
                            innerKeyExpressions.Select(e => Expression.Convert(e, typeof(object)))));

            targetQueryModel.BodyClauses.Add(joinClause);

            return joinQuerySourceReferenceExpression;
        }

        private static void ApplyParentOrderings(
            IEnumerable<Ordering> parentOrderings,
            QueryModel queryModel,
            QuerySourceMapping querySourceMapping)
        {
            var orderByClause = queryModel.BodyClauses.OfType<OrderByClause>().LastOrDefault();

            if (orderByClause == null)
            {
                queryModel.BodyClauses.Add(orderByClause = new OrderByClause());
            }

            // all exisiting order by clauses are guaranteed to be present in the parent ordering list,
            // so we can safely remove them from the original order by clause
            orderByClause.Orderings.Clear();

            foreach (var ordering in parentOrderings)
            {
                var newExpression
                    = CloningExpressionVisitor
                        .AdjustExpressionAfterCloning(ordering.Expression, querySourceMapping);

                if (newExpression is MethodCallExpression methodCallExpression
                    && methodCallExpression.Method.IsEFPropertyMethod())
                {
                    newExpression
                        = new NullConditionalExpression(
                            methodCallExpression.Arguments[0],
                            methodCallExpression);
                }

                orderByClause.Orderings
                    .Add(new Ordering(newExpression, ordering.OrderingDirection));
            }
        }
     
        private static void LiftOrderBy(
            IQuerySource querySource,
            Expression targetExpression,
            QueryModel fromQueryModel,
            QueryModel toQueryModel,
            List<Expression> subQueryProjection)
        {
            foreach (var orderByClause
                in fromQueryModel.BodyClauses.OfType<OrderByClause>().ToArray())
            {
                var outerOrderByClause = new OrderByClause();
                for (var i = 0; i < orderByClause.Orderings.Count; i++)
                {
                    var newExpression
                        = Expression.Call(
                            targetExpression,
                            MaterializedAnonymousObject.GetValueMethodInfo,
                            Expression.Constant(i));

                    outerOrderByClause.Orderings
                        .Add(new Ordering(newExpression, orderByClause.Orderings[i].OrderingDirection));
                }

                // after we lifted the orderings, we need to append the orderings that were applied to the query originally
                // they should come after the ones that were lifted - we want to order by lifted properties first
                var toQueryModelPreviousOrderByClause = toQueryModel.BodyClauses.OfType<OrderByClause>().LastOrDefault();
                if (toQueryModelPreviousOrderByClause != null)
                {
                    foreach (var toQueryModelPreviousOrdering in toQueryModelPreviousOrderByClause.Orderings)
                    {
                        outerOrderByClause.Orderings.Add(toQueryModelPreviousOrdering);
                    }

                    toQueryModel.BodyClauses.Remove(toQueryModelPreviousOrderByClause);
                }

                toQueryModel.BodyClauses.Add(outerOrderByClause);
                fromQueryModel.BodyClauses.Remove(orderByClause);
            }
        }

        private static List<Expression> RemapOriginKeyExpressions(
            IEnumerable<Expression> originKeyExpressions,
            QuerySourceReferenceExpression targetQsre,
            List<Expression> targetExpressions)
        {
            var remappedKeys = new List<Expression>();

            int projectionIndex;
            foreach (var originKeyExpression in originKeyExpressions)
            {
                projectionIndex
                     = targetExpressions
                         .FindIndex(
                             e =>
                             {
                                 var expressionWithoutConvert = e.RemoveConvert();
                                 var projectionExpression = (expressionWithoutConvert as NullConditionalExpression)?.AccessOperation
                                                                   ?? expressionWithoutConvert;

                                 var argumentWithoutConvert = originKeyExpression.RemoveConvert();
                                 var argumentExpression = (argumentWithoutConvert as NullConditionalExpression)?.AccessOperation
                                                                   ?? argumentWithoutConvert;

                                 QuerySourceReferenceExpression projectionQsre = null;
                                 QuerySourceReferenceExpression argumentQsre = null;
                                 string projectionName = null;
                                 string argumentName = null;

                                 if (projectionExpression is MethodCallExpression projectionMethodCall
                                    && projectionMethodCall.IsEFProperty())
                                 {
                                     projectionQsre = projectionMethodCall.Arguments[0] as QuerySourceReferenceExpression;
                                     projectionName = (projectionMethodCall.Arguments[1] as ConstantExpression)?.Value as string;
                                 }
                                 else if (projectionExpression is MemberExpression projectionMember)
                                 {
                                     projectionQsre = projectionMember.Expression as QuerySourceReferenceExpression;
                                     projectionName = projectionMember.Member.Name;
                                 }

                                 if (argumentExpression is MethodCallExpression argumentMethodCall
                                    && argumentMethodCall.IsEFProperty())
                                 {
                                     argumentQsre = argumentMethodCall.Arguments[0] as QuerySourceReferenceExpression;
                                     argumentName = (argumentMethodCall.Arguments[1] as ConstantExpression)?.Value as string;
                                 }
                                 else if (argumentExpression is MemberExpression argumentMember)
                                 {
                                     argumentQsre = argumentMember.Expression as QuerySourceReferenceExpression;
                                     argumentName = argumentMember.Member.Name;
                                 }

                                 return projectionQsre?.ReferencedQuerySource == argumentQsre?.ReferencedQuerySource
                                    && projectionName == argumentName;
                             });

                Debug.Assert(projectionIndex != -1);

                var remappedKey
                    = Expression.Call(
                        targetQsre,
                        MaterializedAnonymousObject.GetValueMethodInfo,
                        Expression.Constant(projectionIndex));

                remappedKeys.Add(remappedKey);
            }

            return remappedKeys;
        }
    }
}
