// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq.Expressions;
using Remotion.Linq.Clauses.Expressions;
using Remotion.Linq.Clauses;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Metadata;
using Remotion.Linq;
using System.Linq;
using Microsoft.EntityFrameworkCore.Query.Expressions.Internal;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Extensions.Internal;
using System;
using Microsoft.EntityFrameworkCore.Internal;
using Remotion.Linq.Clauses.ExpressionVisitors;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Remotion.Linq.Clauses.ResultOperators;
using System.Reflection;

namespace Microsoft.EntityFrameworkCore.Query.ExpressionVisitors.Internal
{
    /// <summary>
    ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public class CorrelatedCollectionOptimizingVisitor : ExpressionVisitorBase
    {
        private readonly QueryCompilationContext _queryCompilationContext;
        private readonly bool _canOptimizeCorrelatedCollections;
        private readonly QueryModel _parentQueryModel;

        private readonly List<GroupJoinClause> _flattenedGroupJoinClauses = new List<GroupJoinClause>();

        private static readonly ExpressionEqualityComparer _expressionEqualityComparer
            = new ExpressionEqualityComparer();

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public CorrelatedCollectionOptimizingVisitor(
            QueryCompilationContext queryCompilationContext, 
            QueryModel parentQueryModel,
            List<GroupJoinClause> flattenedGroupJoinClauses,
            bool canOptimizeCorrelatedCollections)
        {
            _queryCompilationContext = queryCompilationContext;
            _parentQueryModel = parentQueryModel;
            _flattenedGroupJoinClauses = flattenedGroupJoinClauses;
            _canOptimizeCorrelatedCollections = canOptimizeCorrelatedCollections;
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public List<Ordering> ParentOrderings { get; } = new List<Ordering>();

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        protected override Expression VisitSubQuery(SubQueryExpression subQueryExpression)
        {
            if (_queryCompilationContext.CorrelatedSubqueryMetadataMap.TryGetValue(subQueryExpression.QueryModel, out var correlatedSubqueryMetadata))
            {
                var parentQsre = new QuerySourceReferenceExpression(correlatedSubqueryMetadata.ParentQuerySource);

                var result = Rewrite2(subQueryExpression.QueryModel, correlatedSubqueryMetadata.CollectionNavigation, parentQsre);


                return result;
            }

            return base.VisitSubQuery(subQueryExpression);
        }

        private QuerySourceReferenceExpression ExtractOuterQsreFromCorrelationPredicate(NullConditionalEqualExpression correlationPredicate)
        {
            if (correlationPredicate.OuterKey.RemoveConvert() is MethodCallExpression outerMethodCall
                && outerMethodCall.IsEFProperty())
            {
                return (QuerySourceReferenceExpression)outerMethodCall.Arguments[0];
            }

            if (correlationPredicate.OuterKey is NewExpression outerNew
                && outerNew.Arguments[0] is NewArrayExpression outerNewArray
                && outerNewArray.Expressions[0].RemoveConvert() is MethodCallExpression outerNewMetdhodCall
                && outerNewMetdhodCall.IsEFProperty())
            {
                return (QuerySourceReferenceExpression)outerNewMetdhodCall.Arguments[0];
            }

            return null;
        }

        private static Expression BuildKeyAccess(IEnumerable<IProperty> keyProperties, Expression qsre)
        {
            var keyAccessExpressions = keyProperties.Select(p => qsre.CreateEFPropertyExpression(p)).ToArray();

            return Expression.New(
                AnonymousObject2.AnonymousObjectCtor,
                Expression.NewArrayInit(
                    typeof(object),
                    keyAccessExpressions.Select(k => Expression.Convert(k, typeof(object)))));
        }







        private static Expression CreateCorrelationPredicate(INavigation navigation)
        {
            var foreignKey = navigation.ForeignKey;
            var primaryKeyProperties = foreignKey.PrincipalKey.Properties;
            var foreignKeyProperties = foreignKey.Properties;

            var outerKeyParameter = Expression.Parameter(typeof(AnonymousObject2), "o");
            var innerKeyParameter = Expression.Parameter(typeof(AnonymousObject2), "i");

            return Expression.Lambda(
                primaryKeyProperties
                    .Select((pk, i) => new { pk, i })
                    .Zip(
                        foreignKeyProperties,
                        (outer, inner) =>
                        {
                                //Expression outerKeyAccess =
                                //    Expression.Convert(
                                //        Expression.Call(
                                //            outerKeyParameter,
                                //            AnonymousObject.GetValueMethodInfo,
                                //            Expression.Constant(outer.i)),
                                //        primaryKeyProperties[outer.i].ClrType);

                                var outerKeyAccess =
                                Expression.Call(
                                    outerKeyParameter,
                                    AnonymousObject2.GetValueMethodInfo,
                                    Expression.Constant(outer.i));

                            var typedOuterKeyAccess =
                                Expression.Convert(
                                    outerKeyAccess,
                                    primaryKeyProperties[outer.i].ClrType);

                                //Expression innerKeyAccess =
                                //    Expression.Convert(
                                //        Expression.Call(
                                //            innerKeyParameter,
                                //            AnonymousObject.GetValueMethodInfo,
                                //            Expression.Constant(outer.i)),
                                //        foreignKeyProperties[outer.i].ClrType);

                                var innerKeyAccess =
                                Expression.Call(
                                    innerKeyParameter,
                                    AnonymousObject2.GetValueMethodInfo,
                                    Expression.Constant(outer.i));

                            var typedInnerKeyAccess =
                                Expression.Convert(
                                    innerKeyAccess,
                                    foreignKeyProperties[outer.i].ClrType);







                                //Expression equalityExpression;
                                //if (outerKeyAccess.Type != innerKeyAccess.Type)
                                //{
                                //    if (outerKeyAccess.Type.IsNullableType())
                                //    {
                                //        innerKeyAccess = Expression.Convert(innerKeyAccess, outerKeyAccess.Type);
                                //    }
                                //    else
                                //    {
                                //        outerKeyAccess = Expression.Convert(outerKeyAccess, innerKeyAccess.Type);
                                //    }
                                //}





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













                                //if (typeof(IStructuralEquatable).GetTypeInfo()
                                //    .IsAssignableFrom(pkMemberAccess.Type.GetTypeInfo()))
                                //{
                                //    equalityExpression
                                //        = Expression.Call(_structuralEqualsMethod, pkMemberAccess, fkMemberAccess);
                                //}
                                //else
                                {
                                    //equalityExpression = Expression.Equal(outerKeyAccess, innerKeyAccess);
                                    equalityExpression = Expression.Equal(typedOuterKeyAccess, typedInnerKeyAccess);
                            }

                            return inner.ClrType.IsNullableType()
                                ? Expression.Condition(
                                    Expression.OrElse(
                                        Expression.Equal(innerKeyAccess, Expression.Default(innerKeyAccess.Type)),
                                        Expression.Equal(outerKeyAccess, Expression.Default(outerKeyAccess.Type)))
                                        ,
                                    //Expression.Equal(innerKeyAccess, Expression.Constant(null)),
                                    Expression.Constant(false),
                                    equalityExpression)
                                : equalityExpression;
                        })
                    .Aggregate((e1, e2) => Expression.AndAlso(e1, e2)),
                outerKeyParameter,
                innerKeyParameter);
        }










        private static MethodInfo _correlateSubqueryMethodInfo = typeof(IQueryBuffer).GetMethod(nameof(IQueryBuffer.CorrelateSubquery));

        private int _correlatedCollectionCount = 0;




        private Expression Rewrite2(QueryModel collectionQueryModel, INavigation navigation, QuerySourceReferenceExpression originQuerySource)
        {
            var querySourceReferenceFindingExpressionTreeVisitor
                = new QuerySourceReferenceFindingExpressionTreeVisitor();

            var prune = collectionQueryModel.BodyClauses.OfType<WhereClause>().Single(c => c.Predicate is NullConditionalEqualExpression);

            prune.TransformExpressions(querySourceReferenceFindingExpressionTreeVisitor.Visit);

            collectionQueryModel.BodyClauses.Remove(prune);


            var parentQuerySourceReferenceExpression
                = querySourceReferenceFindingExpressionTreeVisitor.QuerySourceReferenceExpression;

            var querySourceReferenceFindingExpressionTreeVisitor2
                = new QuerySourceReferenceFindingExpressionTreeVisitor();

            querySourceReferenceFindingExpressionTreeVisitor2.Visit(((NullConditionalEqualExpression)prune.Predicate).InnerKey);

            var currentKey = BuildKeyAccess(navigation.ForeignKey.Properties, querySourceReferenceFindingExpressionTreeVisitor2.QuerySourceReferenceExpression);



            var originKey = BuildKeyAccess(_queryCompilationContext.Model.FindEntityType(originQuerySource.Type).FindPrimaryKey().Properties, originQuerySource);
            var outerKey = BuildKeyAccess(navigation.ForeignKey.PrincipalKey.Properties, parentQuerySourceReferenceExpression);


            var parentQuerySource = parentQuerySourceReferenceExpression.ReferencedQuerySource;

            BuildOriginQuerySourceOrderings(_parentQueryModel, originQuerySource, ParentOrderings);

            BuildParentOrderings(
                _parentQueryModel,
                navigation,
                parentQuerySourceReferenceExpression,
                ParentOrderings);




            var querySourceMapping = _queryCompilationContext.CorrelatedSubqueryMetadataMap[collectionQueryModel].QuerySourceMapping;
            var clonedParentQueryModel = _queryCompilationContext.CorrelatedSubqueryMetadataMap[collectionQueryModel].ClonedParentQueryModel;


            //var querySourceMapping = new QuerySourceMapping();
            //var clonedParentQueryModel = _parentQueryModel.Clone(querySourceMapping);
            //_queryCompilationContext.UpdateMapping(querySourceMapping);















            _queryCompilationContext.CloneAnnotations(querySourceMapping, clonedParentQueryModel);

            var clonedParentQuerySourceReferenceExpression
                = (QuerySourceReferenceExpression)querySourceMapping.GetExpression(parentQuerySource);

            var clonedParentQuerySource
                = clonedParentQuerySourceReferenceExpression.ReferencedQuerySource;




            // TODO: recreate GroupJoinClauses that may have been removed due to GJ flattening


















            //AdjustPredicate(
            //    clonedParentQueryModel,
            //    clonedParentQuerySource,
            //    clonedParentQuerySourceReferenceExpression);

            //clonedParentQueryModel.SelectClause
            //    = new SelectClause(Expression.Default(typeof(AnonymousObject2)));

            var lastResultOperator = ProcessResultOperators(clonedParentQueryModel, isInclude: false);


            // ???????
            //clonedParentQueryModel.ResultTypeOverride
            //    = typeof(IQueryable<>).MakeGenericType(clonedParentQueryModel.SelectClause.Selector.Type);

            var parentItemName
                = parentQuerySource.HasGeneratedItemName()
                    ? navigation.DeclaringEntityType.DisplayName()[0].ToString().ToLowerInvariant()
                    : parentQuerySource.ItemName;

            collectionQueryModel.MainFromClause.ItemName = $"{parentItemName}.{navigation.Name}";

            var collectionQuerySourceReferenceExpression
                = new QuerySourceReferenceExpression(collectionQueryModel.MainFromClause);

            var subQueryProjection = new List<Expression>();

            var joinQuerySourceReferenceExpression
                = CreateJoinToParentQuery2(
                    clonedParentQueryModel,
                    clonedParentQuerySourceReferenceExpression,
                    collectionQuerySourceReferenceExpression,
                    navigation.ForeignKey,
                    collectionQueryModel,
                    subQueryProjection);

            // need to explicity mark this, since the logic that normally handles this has already ran
            _queryCompilationContext.AddQuerySourceRequiringMaterialization(joinQuerySourceReferenceExpression.ReferencedQuerySource);

            ApplyParentOrderings(
                ParentOrderings,
                clonedParentQueryModel,
                querySourceMapping,
                lastResultOperator);

            LiftOrderBy2(
                clonedParentQuerySource,
                joinQuerySourceReferenceExpression,
                clonedParentQueryModel,
                collectionQueryModel,
                subQueryProjection);

            clonedParentQueryModel.SelectClause.Selector
                = Expression.New(
                    AnonymousObject2.AnonymousObjectCtor,
                    Expression.NewArrayInit(
                        typeof(object),
                        subQueryProjection));



            var newSelectorSecondArg = CloningExpressionVisitor
                    .AdjustExpressionAfterCloning(originKey, querySourceMapping);


            var innerArguments2 = ((NewArrayExpression)(((NewExpression)newSelectorSecondArg).Arguments[0])).Expressions;

            var newInnerArguments2 = new List<Expression>();

            int projectionIndex;
            foreach (var innerArgument2 in innerArguments2)
            {
                projectionIndex
                     = subQueryProjection
                         .FindIndex(
                             e =>
                             {
                                 var expressionWithoutConvert = e.RemoveConvert();
                                 var projectionExpression = (expressionWithoutConvert as NullConditionalExpression)?.AccessOperation
                                                                   ?? expressionWithoutConvert;

                                 var argumentWithoutConvert = innerArgument2.RemoveConvert();

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

                                 if (argumentWithoutConvert is MethodCallExpression argumentMethodCall
                                    && argumentMethodCall.IsEFProperty())
                                 {
                                     argumentQsre = argumentMethodCall.Arguments[0] as QuerySourceReferenceExpression;
                                     argumentName = (argumentMethodCall.Arguments[1] as ConstantExpression)?.Value as string;
                                 }
                                 else if (argumentWithoutConvert is MemberExpression argumentMember)
                                 {
                                     argumentQsre = argumentMember.Expression as QuerySourceReferenceExpression;
                                     argumentName = argumentMember.Member.Name;
                                 }

                                 return projectionQsre?.ReferencedQuerySource == argumentQsre?.ReferencedQuerySource
                                    && projectionName == argumentName;
                             });

                if (projectionIndex == -1)
                {
                    throw new InvalidOperationException("this shouldn't happen");
                }

                var newExpression
                    = Expression.Call(
                        joinQuerySourceReferenceExpression,
                        AnonymousObject2.GetValueMethodInfo,
                        Expression.Constant(projectionIndex));


                newInnerArguments2.Add(newExpression);
            }








            var tupleCtor = typeof(Tuple<,,>).MakeGenericType(
                collectionQueryModel.SelectClause.Selector.Type,
                typeof(AnonymousObject2),
                typeof(AnonymousObject2)).GetConstructors().FirstOrDefault();



            var correlateSubqueryMethod = _correlateSubqueryMethodInfo.MakeGenericMethod(collectionQueryModel.SelectClause.Selector.Type);


            collectionQueryModel.SelectClause.Selector
                = Expression.New(
                    tupleCtor,
                    new Expression[]
                    {
                        collectionQueryModel.SelectClause.Selector,
                        currentKey,
                        Expression.New(
                            AnonymousObject2.AnonymousObjectCtor,
                            Expression.NewArrayInit(
                                typeof(object),
                                newInnerArguments2))
                    });


            collectionQueryModel.ResultTypeOverride = typeof(IEnumerable<>).MakeGenericType(collectionQueryModel.SelectClause.Selector.Type);

            var correlationPredicate = CreateCorrelationPredicate(navigation);

            var arguments = new List<Expression>
                    {
                        Expression.Constant(_correlatedCollectionCount++),
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

            return result;
        }

        private void BuildOriginQuerySourceOrderings(
            QueryModel queryModel,
            QuerySourceReferenceExpression originQsre,
            ICollection<Ordering> parentOrderings)
        {
            var orderings = parentOrderings;

            var orderByClause
                = queryModel.BodyClauses.OfType<OrderByClause>().LastOrDefault();

            if (orderByClause != null)
            {
                orderings = orderings.Concat(orderByClause.Orderings).ToArray();
            }

            var originEntityType = _queryCompilationContext.Model.FindEntityType(originQsre.Type);

            foreach (var property in originEntityType.FindPrimaryKey().Properties)
            {
                var propertyExpression = originQsre.CreateEFPropertyExpression(property);

                var orderingExpression = Expression.Convert(
                    new NullConditionalExpression(
                        originQsre,
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
                    parentOrderings.Add(new Ordering(orderingExpression, OrderingDirection.Asc));
                }
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

        private static void BuildParentOrderings(
            QueryModel queryModel,
            INavigation navigation,
            QuerySourceReferenceExpression querySourceReferenceExpression,
            ICollection<Ordering> parentOrderings)
        {
            var orderings = parentOrderings;

            var orderByClause
                = queryModel.BodyClauses.OfType<OrderByClause>().LastOrDefault();

            if (orderByClause != null)
            {
                orderings = orderings.Concat(orderByClause.Orderings).ToArray();
            }

            foreach (var property in navigation.ForeignKey.PrincipalKey.Properties)
            {
                var propertyExpression = querySourceReferenceExpression.CreateEFPropertyExpression(property);

                var orderingExpression = Expression.Convert(
                    new NullConditionalExpression(
                        querySourceReferenceExpression,
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
                    parentOrderings.Add(new Ordering(orderingExpression, OrderingDirection.Asc));
                }
            }
        }






        private static bool ProcessResultOperators(QueryModel queryModel, bool isInclude)
        {
            var choiceResultOperator
                = queryModel.ResultOperators.LastOrDefault() as ChoiceResultOperatorBase;

            var lastResultOperator = false;

            if (choiceResultOperator != null)
            {
                queryModel.ResultOperators.Remove(choiceResultOperator);
                queryModel.ResultOperators.Add(new TakeResultOperator(Expression.Constant(1)));

                lastResultOperator = choiceResultOperator is LastResultOperator;
            }

            foreach (var groupResultOperator
                in queryModel.ResultOperators.OfType<GroupResultOperator>()
                    .ToArray())
            {
                queryModel.ResultOperators.Remove(groupResultOperator);

                var orderByClause = queryModel.BodyClauses.OfType<OrderByClause>().LastOrDefault();

                if (orderByClause == null)
                {
                    queryModel.BodyClauses.Add(orderByClause = new OrderByClause());
                }

                orderByClause.Orderings.Add(new Ordering(groupResultOperator.KeySelector, OrderingDirection.Asc));
            }

            if (isInclude && queryModel.BodyClauses
                    .Count(
                        bc => bc is AdditionalFromClause
                              || bc is JoinClause
                              || bc is GroupJoinClause) > 0)
            {
                queryModel.ResultOperators.Add(new DistinctResultOperator());
            }

            return lastResultOperator;
        }













        private static QuerySourceReferenceExpression CreateJoinToParentQuery2(
            QueryModel parentQueryModel,
            QuerySourceReferenceExpression parentQuerySourceReferenceExpression,
            Expression outerTargetExpression,
            IForeignKey foreignKey,
            QueryModel targetQueryModel,
            ICollection<Expression> subQueryProjection)
        {
            var subQueryExpression = new SubQueryExpression(parentQueryModel);
            var parentQuerySource = parentQuerySourceReferenceExpression.ReferencedQuerySource;

            var joinClause
                = new JoinClause(
                    "_" + parentQuerySource.ItemName,
                    typeof(AnonymousObject2),
                    subQueryExpression,
                    CreateKeyAccessExpression(
                        outerTargetExpression,
                        foreignKey.Properties),
                    Expression.Constant(null));

            var joinQuerySourceReferenceExpression = new QuerySourceReferenceExpression(joinClause);
            var innerKeyExpressions = new List<Expression>();

            foreach (var principalKeyProperty in foreignKey.PrincipalKey.Properties)
            {
                innerKeyExpressions.Add(
                    Expression.Convert(
                        Expression.Call(
                            joinQuerySourceReferenceExpression,
                            AnonymousObject2.GetValueMethodInfo,
                            Expression.Constant(subQueryProjection.Count)),
                        principalKeyProperty.ClrType.MakeNullable()));

                var propertyExpression
                    = parentQuerySourceReferenceExpression.CreateEFPropertyExpression(principalKeyProperty);

                subQueryProjection.Add(
                    Expression.Convert(
                        new NullConditionalExpression(
                            parentQuerySourceReferenceExpression,
                            propertyExpression),
                        typeof(object)));
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








        // TODO: Unify this with other versions
        private static Expression CreateKeyAccessExpression(Expression target, IReadOnlyList<IProperty> properties)
            => properties.Count == 1
                ? target.CreateEFPropertyExpression(properties[0])
                : Expression.New(
                    AnonymousObject.AnonymousObjectCtor,
                    Expression.NewArrayInit(
                        typeof(object),
                        properties
                            .Select(
                                p =>
                                    Expression.Convert(
                                        target.CreateEFPropertyExpression(p),
                                        typeof(object)))
                            .Cast<Expression>()
                            .ToArray()));








        private static void ApplyParentOrderings(
            IEnumerable<Ordering> parentOrderings,
            QueryModel queryModel,
            QuerySourceMapping querySourceMapping,
            bool reverseOrdering)
        {
            var orderByClause = queryModel.BodyClauses.OfType<OrderByClause>().LastOrDefault();

            if (orderByClause == null)
            {
                queryModel.BodyClauses.Add(orderByClause = new OrderByClause());
            }

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

            if (reverseOrdering)
            {
                foreach (var ordering in orderByClause.Orderings)
                {
                    ordering.OrderingDirection
                        = ordering.OrderingDirection == OrderingDirection.Asc
                            ? OrderingDirection.Desc
                            : OrderingDirection.Asc;
                }
            }
        }














        private bool AreSamePropertyAccessExpressions(Expression propertyAccess1, Expression propertyAccess2)
        {
            var unwrapped1 = (propertyAccess1.RemoveConvert() as NullConditionalExpression)?.AccessOperation.RemoveConvert() ?? propertyAccess1.RemoveConvert();
            var unwrapped2 = (propertyAccess2.RemoveConvert() as NullConditionalExpression)?.AccessOperation.RemoveConvert() ?? propertyAccess2.RemoveConvert();

            QuerySourceReferenceExpression propertyQsre1 = null;
            string propertyName1 = null;

            QuerySourceReferenceExpression propertyQsre2 = null;
            string propertyName2 = null;

            if (unwrapped1 is MethodCallExpression methodCall1
                && methodCall1.Method.IsEFPropertyMethod())
            {
                propertyQsre1 = (QuerySourceReferenceExpression)methodCall1.Arguments[0];
                propertyName1 = (string)((ConstantExpression)methodCall1.Arguments[1]).Value;
            }

            if (unwrapped1 is MemberExpression member1)
            {
                propertyQsre1 = (QuerySourceReferenceExpression)member1.Expression;
                propertyName1 = member1.Member.Name;
            }

            if (unwrapped2 is MethodCallExpression methodCall2
                && methodCall2.Method.IsEFPropertyMethod())
            {
                propertyQsre2 = (QuerySourceReferenceExpression)methodCall2.Arguments[0];
                propertyName2 = (string)((ConstantExpression)methodCall2.Arguments[1]).Value;
            }

            if (unwrapped2 is MemberExpression member2)
            {
                propertyQsre2 = (QuerySourceReferenceExpression)member2.Expression;
                propertyName2 = member2.Member.Name;
            }

            return propertyName1 != null
                && propertyName2 != null
                && propertyQsre1 != null
                && propertyQsre2 != null
                && propertyQsre1.ReferencedQuerySource == propertyQsre2.ReferencedQuerySource
                && propertyName1 == propertyName2;
        }

        private static void LiftOrderBy2(
            IQuerySource querySource,
            Expression targetExpression,
            QueryModel fromQueryModel,
            QueryModel toQueryModel,
            List<Expression> subQueryProjection)
        {
            var canRemove
                = !fromQueryModel.ResultOperators
                    .Any(r => r is SkipResultOperator || r is TakeResultOperator);

            foreach (var orderByClause
                in fromQueryModel.BodyClauses.OfType<OrderByClause>().ToArray())
            {
                var outerOrderByClause = new OrderByClause();

                foreach (var ordering in orderByClause.Orderings)
                {
                    int projectionIndex;

                    if (ordering.Expression is MemberExpression memberExpression
                        && memberExpression.Expression is QuerySourceReferenceExpression memberQsre
                        && memberQsre.ReferencedQuerySource == querySource)
                    {
                        projectionIndex
                            = subQueryProjection
                                .FindIndex(
                                    e =>
                                    {
                                        var expressionWithoutConvert = e.RemoveConvert();
                                        var projectionExpression = (expressionWithoutConvert as NullConditionalExpression)?.AccessOperation
                                                                   ?? expressionWithoutConvert;

                                        if (projectionExpression is MethodCallExpression methodCall
                                            && methodCall.Method.IsEFPropertyMethod())
                                        {
                                            var properyQsre = (QuerySourceReferenceExpression)methodCall.Arguments[0];
                                            var propertyName = (string)((ConstantExpression)methodCall.Arguments[1]).Value;

                                            return properyQsre.ReferencedQuerySource == memberQsre.ReferencedQuerySource
                                                   && propertyName == memberExpression.Member.Name;
                                        }

                                        if (projectionExpression is MemberExpression projectionMemberExpression)
                                        {
                                            var projectionMemberQsre = (QuerySourceReferenceExpression)projectionMemberExpression.Expression;

                                            return projectionMemberQsre.ReferencedQuerySource == memberQsre.ReferencedQuerySource
                                                   && projectionMemberExpression.Member.Name == memberExpression.Member.Name;
                                        }

                                        return false;
                                    });
                    }
                    else
                    {
                        projectionIndex
                            = subQueryProjection
                                .FindIndex(e => _expressionEqualityComparer.Equals(e.RemoveConvert(), ordering.Expression.RemoveConvert()));
                    }

                    if (projectionIndex == -1)
                    {
                        projectionIndex = subQueryProjection.Count;

                        subQueryProjection.Add(
                            Expression.Convert(
                                // Workaround re-linq#RMLNQ-111 - When this is fixed the Clone can go away
                                CloningExpressionVisitor.AdjustExpressionAfterCloning(
                                    ordering.Expression,
                                    new QuerySourceMapping()),
                                typeof(object)));
                    }

                    var newExpression
                        = Expression.Call(
                            targetExpression,
                            AnonymousObject2.GetValueMethodInfo,
                            Expression.Constant(projectionIndex));

                    outerOrderByClause.Orderings
                        .Add(new Ordering(newExpression, ordering.OrderingDirection));
                }

                toQueryModel.BodyClauses.Add(outerOrderByClause);

                if (canRemove)
                {
                    fromQueryModel.BodyClauses.Remove(orderByClause);
                }
            }
        }
    }
}
