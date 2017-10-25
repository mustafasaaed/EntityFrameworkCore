// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq.Expressions;
using Remotion.Linq.Clauses.Expressions;
using Remotion.Linq.Clauses;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Metadata;
using Remotion.Linq;
using Remotion.Linq.Parsing;
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

        private static readonly ExpressionEqualityComparer _expressionEqualityComparer
            = new ExpressionEqualityComparer();

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public CorrelatedCollectionOptimizingVisitor(
            QueryCompilationContext queryCompilationContext, 
            QueryModel parentQueryModel,
            bool canOptimizeCorrelatedCollections)
        {
            _queryCompilationContext = queryCompilationContext;
            _parentQueryModel = parentQueryModel;
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

                //OdNowaWszystko(subQueryExpression.QueryModel, correlatedSubqueryMetadata.CollectionNavigation, parentQsre);

                //CorrelateSubquery3(parentQsre);


                var result = Rewrite2(subQueryExpression.QueryModel, correlatedSubqueryMetadata.CollectionNavigation, parentQsre);


                return result;

                //return subQueryExpression;
            }

            return base.VisitSubQuery(subQueryExpression);
        }








        private void OdNowaWszystko(QueryModel collectionQueryModel, INavigation navigation, QuerySourceReferenceExpression originQuerySource)
        {
            var querySourceReferenceFindingExpressionTreeVisitor
                = new QuerySourceReferenceFindingExpressionTreeVisitor();

            var correlationPredicate = collectionQueryModel.BodyClauses.OfType<WhereClause>().Single(c => c.Predicate is NullConditionalEqualExpression);
            correlationPredicate.TransformExpressions(querySourceReferenceFindingExpressionTreeVisitor.Visit);
            collectionQueryModel.BodyClauses.Remove(correlationPredicate);

            //var outerQsre = ExtractOuterQsreFromCorrelationPredicate((NullConditionalEqualExpression)correlationPredicate.Predicate);




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










        //private Expression CorrelateSubquery3(
        //    QuerySourceReferenceExpression parentQuerySource)
        //{


        //    //TODO: correlation predicate can be extracted from the where clause thingy
        //}








        private int _correlatedCollectionCount = 0;




        private Expression CorrelateSubquery2(
            QuerySourceReferenceExpression originQuerySourceExpression,
            
            
            //QuerySourceReferenceExpression outerExpression, TODO: do we even need this?
            INavigation firstNavigation,
            INavigation collectionNavigation,
            SubQueryExpression subQueryExpression)
        {
            var subQueryModel = subQueryExpression.QueryModel;


            var outerExpression = default(Expression);

            var outerKey = BuildKeyAccess(collectionNavigation.ForeignKey.PrincipalKey.Properties, outerExpression);
            var innerKey = BuildKeyAccess(collectionNavigation.ForeignKey.Properties, new QuerySourceReferenceExpression(subQueryModel.MainFromClause));
            var correlationnPredicate = CreateCorrelationPredicate(collectionNavigation);


            // TODO: move this somewhere else, we need it!

            //if (_processedQueryModels.Contains(subQueryModel))
            //{
            //    subQueryModel = subQueryModel.Clone();
            //}
            //else
            //{
            //    _processedQueryModels.Add(subQueryModel);
            //}

            var subQueryResultElementType = subQueryModel.SelectClause.Selector.Type;



            //                if (!_processedQueryModelsMap.TryGetValue(subQueryModel, out var subQueryResultElementType))
            {
                //var subQueryResultElementType = subQueryModel.SelectClause.Selector.Type;
                //_processedQueryModelsMap.Add(subQueryModel, subQueryResultElementType);


                //}

                //if (!_processedQueryModels.Contains(subQueryModel))
                //{
                //    _processedQueryModels.Add(subQueryModel);

                var kvp = typeof(KeyValuePair<,>).MakeGenericType(subQueryResultElementType, typeof(AnonymousObject2));
                var kvpCtor = kvp.GetTypeInfo().DeclaredConstructors.FirstOrDefault();

                var kvp2 = typeof(KeyValuePair<,>).MakeGenericType(kvp, typeof(AnonymousObject2));
                var kvp2Ctor = kvp2.GetTypeInfo().DeclaredConstructors.FirstOrDefault();

                var originEntityType = _queryCompilationContext.Model.FindEntityType(originQuerySourceExpression.Type);
                var originKey = BuildKeyAccess(originEntityType.FindPrimaryKey().Properties, originQuerySourceExpression);

                subQueryModel.SelectClause.Selector = Expression.New(
                    kvp2Ctor,
                    Expression.New(kvpCtor, subQueryModel.SelectClause.Selector, innerKey),
                    originKey);

                //subQueryModel.SelectClause.Selector = Expression.New(kvpCtor, subQueryModel.SelectClause.Selector, innerKey);
                subQueryModel.ResultTypeOverride = typeof(IEnumerable<>).MakeGenericType(subQueryModel.SelectClause.Selector.Type);
            }

            var arguments = new List<Expression>
                    {
                        Expression.Constant(_correlatedCollectionCount++),
                        originQuerySourceExpression,
                        Expression.Constant(collectionNavigation),
                        Expression.Constant(firstNavigation),
                        outerKey,
                        Expression.Lambda(new SubQueryExpression(subQueryModel)),
                        correlationnPredicate
                    };

            var generic = _correlateSubqueryMethodInfo.MakeGenericMethod(subQueryResultElementType);

            var result = Expression.Call(
                Expression.Property(
                    EntityQueryModelVisitor.QueryContextParameter,
                    nameof(QueryContext.QueryBuffer)),
                generic,
                arguments);

            return result;

            //// TODO: needed?
            //var resultCollectionType = collectionNavigation.GetCollectionAccessor().CollectionType;

            //return resultCollectionType.GetTypeInfo().IsGenericType && resultCollectionType.GetGenericTypeDefinition() == typeof(ICollection<>)
            //    ? (Expression)result
            //    : Expression.Convert(result, resultCollectionType);
        }








        private Expression Rewrite2(QueryModel collectionQueryModel, INavigation navigation, QuerySourceReferenceExpression originQuerySource)
        {
            var querySourceReferenceFindingExpressionTreeVisitor
                = new QuerySourceReferenceFindingExpressionTreeVisitor();

            var prune = collectionQueryModel.BodyClauses.OfType<WhereClause>().Single(c => c.Predicate is NullConditionalEqualExpression);

            prune.TransformExpressions(querySourceReferenceFindingExpressionTreeVisitor.Visit);

            collectionQueryModel.BodyClauses.Remove(prune);



            // origin
            // parent
            // current











            var parentQuerySourceReferenceExpression
                = querySourceReferenceFindingExpressionTreeVisitor.QuerySourceReferenceExpression;





            var querySourceReferenceFindingExpressionTreeVisitor2
                = new QuerySourceReferenceFindingExpressionTreeVisitor();

            querySourceReferenceFindingExpressionTreeVisitor2.Visit(((NullConditionalEqualExpression)prune.Predicate).InnerKey);

            var currentKey = BuildKeyAccess(navigation.ForeignKey.Properties, querySourceReferenceFindingExpressionTreeVisitor2.QuerySourceReferenceExpression);


            


            //var parentKey = BuildKeyAccess(navigation.ForeignKey.Properties, parentQuerySourceReferenceExpression);



            var originKey = BuildKeyAccess(_queryCompilationContext.Model.FindEntityType(originQuerySource.Type).FindPrimaryKey().Properties, originQuerySource);


            var outerKey = BuildKeyAccess(navigation.ForeignKey.PrincipalKey.Properties, parentQuerySourceReferenceExpression);




            //if (innerKey.Type == typeof(AnonymousObject))
            //{
            //    innerKey = Expression.New(
            //        AnonymousObject2.AnonymousObjectCtor,
            //        ((NewExpression)innerKey).Arguments[0]);
            //}
            //else
            //{
            //    innerKey = Expression.New(
            //        AnonymousObject2.AnonymousObjectCtor,
            //        Expression.NewArrayInit(
            //            typeof(object),
            //            innerKey));
            //}







            var parentQuerySource = parentQuerySourceReferenceExpression.ReferencedQuerySource;



            BuildOriginQuerySourceOrderings(_parentQueryModel, originQuerySource, ParentOrderings);

            BuildParentOrderings(
                _parentQueryModel,
                navigation,
                parentQuerySourceReferenceExpression,
                ParentOrderings);

            var querySourceMapping = new QuerySourceMapping();
            var clonedParentQueryModel = _parentQueryModel.Clone(querySourceMapping);
            _queryCompilationContext.UpdateMapping(querySourceMapping);

            _queryCompilationContext.CloneAnnotations(querySourceMapping, clonedParentQueryModel);

            var clonedParentQuerySourceReferenceExpression
                = (QuerySourceReferenceExpression)querySourceMapping.GetExpression(parentQuerySource);

            var clonedParentQuerySource
                = clonedParentQuerySourceReferenceExpression.ReferencedQuerySource;

            //AdjustPredicate(
            //    clonedParentQueryModel,
            //    clonedParentQuerySource,
            //    clonedParentQuerySourceReferenceExpression);

            clonedParentQueryModel.SelectClause
                = new SelectClause(Expression.Default(typeof(AnonymousObject2)));

            var subQueryProjection = new List<Expression>();

            var lastResultOperator = ProcessResultOperators(clonedParentQueryModel, isInclude: false);

            clonedParentQueryModel.ResultTypeOverride
                = typeof(IQueryable<>).MakeGenericType(clonedParentQueryModel.SelectClause.Selector.Type);

            var parentItemName
                = parentQuerySource.HasGeneratedItemName()
                    ? navigation.DeclaringEntityType.DisplayName()[0].ToString().ToLowerInvariant()
                    : parentQuerySource.ItemName;

            collectionQueryModel.MainFromClause.ItemName = $"{parentItemName}.{navigation.Name}";

            var collectionQuerySourceReferenceExpression
                = new QuerySourceReferenceExpression(collectionQueryModel.MainFromClause);

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





            //var subQueryResultElementType = //subQueryModel.SelectClause.Selector.Type;
            //    collectionQueryModel.SelectClause.Selector.Type;


            //var kvp = typeof(KeyValuePair<,>).MakeGenericType(subQueryResultElementType, typeof(AnonymousObject2));
            //var kvpCtor = kvp.GetTypeInfo().DeclaredConstructors.FirstOrDefault();

            //var kvp2 = typeof(KeyValuePair<,>).MakeGenericType(kvp, typeof(AnonymousObject2));
            //var kvp2Ctor = kvp2.GetTypeInfo().DeclaredConstructors.FirstOrDefault();















            //var originEntityType = _queryCompilationContext.Model.FindEntityType(originQuerySourceExpression.Type);
            //var originKey = BuildKeyAccess(originEntityType.FindPrimaryKey().Properties, originQuerySourceExpression);

            //subQueryModel.SelectClause.Selector = Expression.New(
            //    kvp2Ctor,
            //    Expression.New(kvpCtor, subQueryModel.SelectClause.Selector, innerKey),
            //    originKey);

            ////subQueryModel.SelectClause.Selector = Expression.New(kvpCtor, subQueryModel.SelectClause.Selector, innerKey);
            //subQueryModel.ResultTypeOverride = typeof(IEnumerable<>).MakeGenericType(subQueryModel.SelectClause.Selector.Type);




























            //var foo = 

            //collectionQueryModel.SelectClause.Selector = 





            //    ((NewExpression)collectionQueryModel.SelectClause.Selector).Arguments[0],
            //    Expression.New(
            //        AnonymousObject2.AnonymousObjectCtor,
            //        Expression.NewArrayInit(
            //            typeof(object),
            //            newInnerArguments2)));




















            //var selectorSecondArg = ((NewExpression)collectionQueryModel.SelectClause.Selector).Arguments[1];
            //var innerArguments = ((NewArrayExpression)(((NewExpression)selectorSecondArg).Arguments[0])).Expressions;




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
                        //parentKey,
                        //originKey,

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
                        Expression.Constant(_childCollectionIndex++),
                        //originQuerySourceExpression,
                        Expression.Constant(navigation),
                        //Expression.Constant(firstNavigation),
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



            //var ctor = ((NewExpression)collectionQueryModel.SelectClause.Selector).Constructor;





            //collectionQueryModel.SelectClause.Selector = Expression.New(
            //    ctor,
            //    ((NewExpression)collectionQueryModel.SelectClause.Selector).Arguments[0],
            //    Expression.New(
            //        AnonymousObject2.AnonymousObjectCtor,
            //        Expression.NewArrayInit(
            //            typeof(object),
            //            newInnerArguments2)));
        }





        private int _childCollectionIndex = 0;






















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

                if (!orderings.Any(
                    o =>
                        _expressionEqualityComparer.Equals(o.Expression, propertyExpression)
                        || o.Expression is MemberExpression memberExpression
                        && memberExpression.Expression is QuerySourceReferenceExpression memberQuerySourceReferenceExpression
                        && ReferenceEquals(memberQuerySourceReferenceExpression.ReferencedQuerySource, originQsre.ReferencedQuerySource)
                        && memberExpression.Member.Equals(property.PropertyInfo)))
                {
                    parentOrderings.Add(new Ordering(propertyExpression, OrderingDirection.Asc));
                }
            }
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

                if (!orderings.Any(
                    o =>
                        _expressionEqualityComparer.Equals(o.Expression, propertyExpression)
                        || o.Expression is MemberExpression memberExpression
                        && memberExpression.Expression is QuerySourceReferenceExpression memberQuerySourceReferenceExpression
                        && ReferenceEquals(memberQuerySourceReferenceExpression.ReferencedQuerySource, querySourceReferenceExpression.ReferencedQuerySource)
                        && memberExpression.Member.Equals(property.PropertyInfo)))
                {
                    parentOrderings.Add(new Ordering(propertyExpression, OrderingDirection.Asc));
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

                //var foo = (((MethodCallExpression)ordering.Expression).Arguments[0] as QuerySourceReferenceExpression).ReferencedQuerySource;
                //var bar = (((MethodCallExpression)newExpression).Arguments[0] as QuerySourceReferenceExpression).ReferencedQuerySource;



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
                                .FindIndex(e => _expressionEqualityComparer.Equals(e.RemoveConvert(), ordering.Expression));
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

















        private class QuerySourceReferenceFindingExpressionTreeVisitor : RelinqExpressionVisitor
        {
            public QuerySourceReferenceExpression QuerySourceReferenceExpression { get; private set; }

            protected override Expression VisitQuerySourceReference(QuerySourceReferenceExpression querySourceReferenceExpression)
            {
                if (QuerySourceReferenceExpression == null)
                {
                    QuerySourceReferenceExpression = querySourceReferenceExpression;
                }

                return querySourceReferenceExpression;
            }
        }






    }
}
