﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using System.Linq;

namespace Microsoft.EntityFrameworkCore.Query.Internal
{
    /// <summary>
    ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public class QueryBuffer : IQueryBuffer
    {
        private readonly QueryContextDependencies _dependencies;

        private IWeakReferenceIdentityMap _identityMap0;
        private IWeakReferenceIdentityMap _identityMap1;
        private Dictionary<IKey, IWeakReferenceIdentityMap> _identityMaps;

        private readonly ConditionalWeakTable<object, object> _valueBuffers
            = new ConditionalWeakTable<object, object>();

        private readonly Dictionary<int, IDisposable> _includedCollections
            = new Dictionary<int, IDisposable>(); // IDisposable as IEnumerable/IAsyncEnumerable

        //private readonly Dictionary<int, IDisposable> _childCollections
        //    = new Dictionary<int, IDisposable>(); // IDisposable as IEnumerable/IAsyncEnumerable

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public QueryBuffer([NotNull] QueryContextDependencies dependencies) 
            => _dependencies = dependencies;

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public virtual object GetEntity(
            IKey key,
            EntityLoadInfo entityLoadInfo,
            bool queryStateManager,
            bool throwOnNullKey)
        {
            if (queryStateManager)
            {
                var entry = _dependencies.StateManager.TryGetEntry(key, entityLoadInfo.ValueBuffer, throwOnNullKey);

                if (entry != null)
                {
                    return entry.Entity;
                }
            }

            var identityMap = GetOrCreateIdentityMap(key);

            var weakReference = identityMap.TryGetEntity(entityLoadInfo.ValueBuffer, throwOnNullKey, out var hasNullKey);

            if (hasNullKey)
            {
                return null;
            }

            if (weakReference == null
                || !weakReference.TryGetTarget(out var entity))
            {
                entity = entityLoadInfo.Materialize();

                if (weakReference != null)
                {
                    weakReference.SetTarget(entity);
                }
                else
                {
                    identityMap.CollectGarbage();
                    identityMap.Add(entityLoadInfo.ValueBuffer, entity);
                }

                _valueBuffers.Add(entity, entityLoadInfo.ForType(entity.GetType()));
            }

            return entity;
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public virtual object GetPropertyValue(object entity, IProperty property)
        {
            var entry = _dependencies.StateManager.TryGetEntry(entity);

            if (entry != null)
            {
                return entry[property];
            }

            var found = _valueBuffers.TryGetValue(entity, out var boxedValueBuffer);

            Debug.Assert(found);

            var valueBuffer = (ValueBuffer)boxedValueBuffer;

            return valueBuffer[property.GetIndex()];
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public virtual void StartTracking(object entity, EntityTrackingInfo entityTrackingInfo)
        {
            if (!_valueBuffers.TryGetValue(entity, out var boxedValueBuffer))
            {
                boxedValueBuffer = ValueBuffer.Empty;
            }

            entityTrackingInfo.StartTracking(_dependencies.StateManager, entity, (ValueBuffer)boxedValueBuffer);
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public virtual void StartTracking(object entity, IEntityType entityType)
        {
            if (!_valueBuffers.TryGetValue(entity, out var boxedValueBuffer))
            {
                boxedValueBuffer = ValueBuffer.Empty;
            }

            _dependencies.StateManager
                .StartTrackingFromQuery(
                    entityType,
                    entity,
                    (ValueBuffer)boxedValueBuffer,
                    handledForeignKeys: null);
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public virtual void IncludeCollection<TEntity, TRelated>(
            int includeId,
            INavigation navigation,
            INavigation inverseNavigation,
            IEntityType targetEntityType,
            IClrCollectionAccessor clrCollectionAccessor,
            IClrPropertySetter inverseClrPropertySetter,
            bool tracking,
            TEntity entity,
            Func<IEnumerable<TRelated>> relatedEntitiesFactory,
            Func<TEntity, TRelated, bool> joinPredicate)
        {
            IDisposable untypedEnumerator = null;
            IEnumerator<TRelated> enumerator = null;

            if (includeId == -1
                || !_includedCollections.TryGetValue(includeId, out untypedEnumerator))
            {
                enumerator = relatedEntitiesFactory().GetEnumerator();

                if (!enumerator.MoveNext())
                {
                    enumerator.Dispose();
                    enumerator = null;
                }

                if (includeId != -1)
                {
                    _includedCollections.Add(includeId, enumerator);
                }
            }

            if (enumerator == null)
            {
                if (untypedEnumerator == null)
                {
                    clrCollectionAccessor.GetOrCreate(entity);

                    return;
                }

                enumerator = (IEnumerator<TRelated>)untypedEnumerator;
            }

            var relatedEntities = new List<object>();

            IIncludeKeyComparer keyComparer = null;

            if (joinPredicate == null)
            {
                keyComparer = CreateIncludeKeyComparer(entity, navigation);
            }

            while (true)
            {
                bool shouldInclude;

                if (joinPredicate == null)
                {
                    if (_valueBuffers.TryGetValue(enumerator.Current, out var relatedValueBuffer))
                    {
                        shouldInclude = keyComparer.ShouldInclude((ValueBuffer)relatedValueBuffer);
                    }
                    else
                    {
                        var entry = _dependencies.StateManager.TryGetEntry(enumerator.Current);

                        Debug.Assert(entry != null);

                        shouldInclude = keyComparer.ShouldInclude(entry);
                    }
                }
                else
                {
                    shouldInclude = joinPredicate(entity, enumerator.Current);
                }

                if (shouldInclude)
                {
                    relatedEntities.Add(enumerator.Current);

                    if (tracking)
                    {
                        StartTracking(enumerator.Current, targetEntityType);
                    }

                    if (inverseNavigation != null)
                    {
                        Debug.Assert(inverseClrPropertySetter != null);

                        inverseClrPropertySetter.SetClrValue(enumerator.Current, entity);

                        if (tracking)
                        {
                            var internalEntityEntry = _dependencies.StateManager.TryGetEntry(enumerator.Current);

                            Debug.Assert(internalEntityEntry != null);

                            internalEntityEntry.SetRelationshipSnapshotValue(inverseNavigation, entity);
                        }
                    }

                    if (!enumerator.MoveNext())
                    {
                        enumerator.Dispose();

                        if (includeId != -1)
                        {
                            _includedCollections[includeId] = null;
                        }

                        break;
                    }
                }
                else
                {
                    break;
                }
            }

            clrCollectionAccessor.AddRange(entity, relatedEntities);

            if (tracking)
            {
                var internalEntityEntry = _dependencies.StateManager.TryGetEntry(entity);

                Debug.Assert(internalEntityEntry != null);

                internalEntityEntry.AddRangeToCollectionSnapshot(navigation, relatedEntities);
                internalEntityEntry.SetIsLoaded(navigation);
            }
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public virtual async Task IncludeCollectionAsync<TEntity, TRelated>(
            int includeId,
            INavigation navigation,
            INavigation inverseNavigation,
            IEntityType targetEntityType,
            IClrCollectionAccessor clrCollectionAccessor,
            IClrPropertySetter inverseClrPropertySetter,
            bool tracking,
            TEntity entity,
            Func<IAsyncEnumerable<TRelated>> relatedEntitiesFactory,
            Func<TEntity, TRelated, bool> joinPredicate,
            CancellationToken cancellationToken)
        {
            IDisposable untypedAsyncEnumerator = null;
            IAsyncEnumerator<TRelated> asyncEnumerator = null;

            if (includeId == -1
                || !_includedCollections.TryGetValue(includeId, out untypedAsyncEnumerator))
            {
                asyncEnumerator = relatedEntitiesFactory().GetEnumerator();

                if (!await asyncEnumerator.MoveNext(cancellationToken))
                {
                    asyncEnumerator.Dispose();
                    asyncEnumerator = null;
                }

                if (includeId != -1)
                {
                    _includedCollections.Add(includeId, asyncEnumerator);
                }
            }

            if (asyncEnumerator == null)
            {
                if (untypedAsyncEnumerator == null)
                {
                    clrCollectionAccessor.GetOrCreate(entity);

                    return;
                }

                asyncEnumerator = (IAsyncEnumerator<TRelated>)untypedAsyncEnumerator;
            }

            var relatedEntities = new List<object>();

            IIncludeKeyComparer keyComparer = null;

            if (joinPredicate == null)
            {
                keyComparer = CreateIncludeKeyComparer(entity, navigation);
            }

            while (true)
            {
                bool shouldInclude;

                if (joinPredicate == null)
                {
                    if (_valueBuffers.TryGetValue(asyncEnumerator.Current, out var relatedValueBuffer))
                    {
                        shouldInclude = keyComparer.ShouldInclude((ValueBuffer)relatedValueBuffer);
                    }
                    else
                    {
                        var entry = _dependencies.StateManager.TryGetEntry(asyncEnumerator.Current);

                        Debug.Assert(entry != null);

                        shouldInclude = keyComparer.ShouldInclude(entry);
                    }
                }
                else
                {
                    shouldInclude = joinPredicate(entity, asyncEnumerator.Current);
                }

                if (shouldInclude)
                {
                    relatedEntities.Add(asyncEnumerator.Current);

                    if (tracking)
                    {
                        StartTracking(asyncEnumerator.Current, targetEntityType);
                    }

                    if (inverseNavigation != null)
                    {
                        Debug.Assert(inverseClrPropertySetter != null);

                        inverseClrPropertySetter.SetClrValue(asyncEnumerator.Current, entity);

                        if (tracking)
                        {
                            var internalEntityEntry = _dependencies.StateManager.TryGetEntry(asyncEnumerator.Current);

                            Debug.Assert(internalEntityEntry != null);

                            internalEntityEntry.SetRelationshipSnapshotValue(inverseNavigation, entity);
                        }
                    }

                    if (!await asyncEnumerator.MoveNext(cancellationToken))
                    {
                        asyncEnumerator.Dispose();

                        _includedCollections[includeId] = null;

                        break;
                    }
                }
                else
                {
                    break;
                }
            }

            clrCollectionAccessor.AddRange(entity, relatedEntities);

            if (tracking)
            {
                var internalEntityEntry = _dependencies.StateManager.TryGetEntry(entity);

                Debug.Assert(internalEntityEntry != null);

                internalEntityEntry.AddRangeToCollectionSnapshot(navigation, relatedEntities);
                internalEntityEntry.SetIsLoaded(navigation);
            }
        }

        private IIncludeKeyComparer CreateIncludeKeyComparer(
            object entity,
            INavigation navigation)
        {
            var identityMap = GetOrCreateIdentityMap(navigation.ForeignKey.PrincipalKey);

            if (!_valueBuffers.TryGetValue(entity, out var boxedValueBuffer))
            {
                var entry = _dependencies.StateManager.TryGetEntry(entity);

                Debug.Assert(entry != null);

                return identityMap.CreateIncludeKeyComparer(navigation, entry);
            }

            return identityMap.CreateIncludeKeyComparer(navigation, (ValueBuffer)boxedValueBuffer);
        }

        private IWeakReferenceIdentityMap GetOrCreateIdentityMap(IKey key)
        {
            if (_identityMap0 == null)
            {
                _identityMap0 = key.GetWeakReferenceIdentityMapFactory()();

                return _identityMap0;
            }

            if (_identityMap0.Key == key)
            {
                return _identityMap0;
            }

            if (_identityMap1 == null)
            {
                _identityMap1 = key.GetWeakReferenceIdentityMapFactory()();

                return _identityMap1;
            }

            if (_identityMap1.Key == key)
            {
                return _identityMap1;
            }

            if (_identityMaps == null)
            {
                _identityMaps = new Dictionary<IKey, IWeakReferenceIdentityMap>();
            }

            if (!_identityMaps.TryGetValue(key, out var identityMap))
            {
                identityMap = key.GetWeakReferenceIdentityMapFactory()();

                _identityMaps[key] = identityMap;
            }

            return identityMap;
        }











        ///// <summary>
        /////     This API supports the Entity Framework Core infrastructure and is not intended to be used
        /////     directly from your code. This API may change or be removed in future releases.
        ///// </summary>
        //public virtual IEnumerable<TInner> CorrelateSubquery<TInner>(
        //    int childCollectionId,
        //    //object originQuerySource,
        //    INavigation navigation,
        //    //INavigation firstNavigation,
        //    AnonymousObject2 outerKey,
        //    Func<IEnumerable<Tuple<TInner, AnonymousObject2, AnonymousObject2>>> childCollectionElementFactory,
        //    Func<AnonymousObject2, AnonymousObject2, bool> correlationnPredicate)
        //{
        //    IDisposable untypedEnumerator = null;
        //    IEnumerator<Tuple<TInner, AnonymousObject2, AnonymousObject2>> enumerator = null;

        //    if (childCollectionId == -1
        //        || !_childCollections.TryGetValue(childCollectionId, out untypedEnumerator))
        //    {
        //        enumerator = childCollectionElementFactory().GetEnumerator();

        //        if (!enumerator.MoveNext())
        //        {
        //            enumerator.Dispose();
        //            enumerator = null;
        //        }

        //        if (childCollectionId != -1)
        //        {
        //            _childCollections.Add(childCollectionId, enumerator);
        //        }
        //    }

        //    if (enumerator == null)
        //    {
        //        if (untypedEnumerator == null)
        //        {
        //            var clrCollectionAccessor = navigation.GetCollectionAccessor();

        //            var sequenceType = clrCollectionAccessor.CollectionType.TryGetSequenceType();
        //            if (sequenceType != typeof(TInner))
        //            {
        //                return new List<TInner>();
        //            }

        //            var result = clrCollectionAccessor.Create();

        //            return (IEnumerable<TInner>)result;
        //        }

        //        enumerator = (IEnumerator<Tuple<TInner, AnonymousObject2, AnonymousObject2>>)untypedEnumerator;
        //    }

        //    var originKeysMap = new Dictionary<AnonymousObject2, int>();

        //    var previousOriginKey = default(AnonymousObject2);
        //    var firstCollection = true;

        //    var elementCount = -1;

        //    var inners = new List<TInner>();
        //    while (true)
        //    {
        //        var shouldInclude = correlationnPredicate(outerKey, enumerator.Current.Item2);
        //        if (shouldInclude)
        //        {
        //            if (originKeysMap.TryGetValue(outerKey, out var expectedCount)
        //                && expectedCount > elementCount)
        //            {
        //                shouldInclude = false;
        //            }

        //            if (!firstCollection
        //                && outerKey != previousOriginKey)
        //            {
        //                shouldInclude = false;

        //                originKeysMap[previousOriginKey] = elementCount;
        //            }
        //        }

        //        firstCollection = false;
        //        previousOriginKey = outerKey;




        //        //if (shouldInclude)
        //        //{
        //        //    if (originKeysMap.TryGetValue(outerKey, out var foobar))
        //        //    {
        //        //        if (enumerator.Current.Value.Equals(foobar))
        //        //        {
        //        //            shouldInclude = false;
        //        //        }

        //        //        // compare

        //        //    }
        //        //    else
        //        //    {
        //        //        originKeysMap[outerKey] = enumerator.Current.Value;
        //        //    }
        //        //}


        //        //if (firstOriginElementKey == null
        //        //    || firstOriginElementKey.Equals(enumerator.Current.Value))
        //        //{
        //        //    shouldInclude = false;
        //        //}


        //        if (shouldInclude)
        //        {
        //            inners.Add(enumerator.Current.Item1);

        //            // TODO: is tracking needed here?

        //            //if (tracking)
        //            //{
        //            //    StartTracking(enumerator.Current, targetEntityType);
        //            //}

        //            //if (inverseNavigation != null)
        //            //{
        //            //    Debug.Assert(inverseClrPropertySetter != null);

        //            //    inverseClrPropertySetter.SetClrValue(enumerator.Current, entity);

        //            //    if (tracking)
        //            //    {
        //            //        var internalEntityEntry = _dependencies.StateManager.TryGetEntry(enumerator.Current);

        //            //        Debug.Assert(internalEntityEntry != null);

        //            //        internalEntityEntry.SetRelationshipSnapshotValue(inverseNavigation, entity);
        //            //    }
        //            //}

        //            elementCount++;

        //            if (!enumerator.MoveNext())
        //            {
        //                enumerator.Dispose();

        //                if (childCollectionId != -1)
        //                {
        //                    _childCollections[childCollectionId] = null;
        //                }

        //                //firstCollection = false;
        //                //previousOriginKey = outerKey;

        //                break;
        //            }
        //        }
        //        else
        //        {
        //            //originKeysMap[previousOriginKey] = elementCount;
        //            //previousOriginKey = outerKey;

        //            //firstCollection = false;
        //            //previousOriginKey = outerKey;

        //            break;
        //        }
        //    }

        //    return inners;
        //}




        //private Dictionary<int, Tuple<IDisposable, int, int>> _childCollections2 = new Dictionary<int, Tuple<IDisposable, int, int>>();

        private struct ChildCollectionMetadataElement
        {
            public ChildCollectionMetadataElement(IDisposable enumerator, int lastOuterElementIndex, int maxInnerElementIndex, Tuple<object, AnonymousObject2> previous)
            {
                Enumerator = enumerator;
                LastOuterElementIndex = lastOuterElementIndex;
                MaxInnerElementIndex = maxInnerElementIndex;
                Previous = previous;
            }

            public IDisposable Enumerator { get; set; }
            public int LastOuterElementIndex { get; set; }
            public int MaxInnerElementIndex { get; set; }
            public Tuple<object, AnonymousObject2> Previous { get; set; }
        }

        private Dictionary<int, ChildCollectionMetadataElement> _childCollectionMetadata = new Dictionary<int, ChildCollectionMetadataElement>();

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public virtual IEnumerable<TInner> CorrelateSubquery<TInner>(
            int childCollectionId,
            int outerElementIndex,
            INavigation navigation,
            AnonymousObject2 outerKey,
            Func<IEnumerable<Tuple<TInner, AnonymousObject2, AnonymousObject2>>> childCollectionElementFactory,
            Func<AnonymousObject2, AnonymousObject2, bool> correlationnPredicate)
        {
            IDisposable untypedEnumerator = null;
            ChildCollectionMetadataElement childCollectionMetadataElement;
            IEnumerator<Tuple<TInner, AnonymousObject2, AnonymousObject2>> enumerator = null;
            //Tuple<object, AnonymousObject2> previous;

            if (!_childCollectionMetadata.TryGetValue(childCollectionId, out childCollectionMetadataElement))
            {
                enumerator = childCollectionElementFactory().GetEnumerator();

                childCollectionMetadataElement = new ChildCollectionMetadataElement(enumerator, -1, -1, default);
                _childCollectionMetadata[childCollectionId] = childCollectionMetadataElement;
            }
            else
            {
                untypedEnumerator = childCollectionMetadataElement.Enumerator;
            }

            if (enumerator == null)
            {
                if (untypedEnumerator == null)
                {
                    yield break;
                }

                enumerator = (IEnumerator<Tuple<TInner, AnonymousObject2, AnonymousObject2>>)untypedEnumerator;
            }

            // = 1 - sequential
            // > 1 - skipping forward, need to go thru reader until matching element is found or reaching end of the reader
            // < 1 - got back to earlier element, need to reset reader and then look for elements till find a match (just like with skipping forward case)
            var outerElementAccessDirection = outerElementIndex - childCollectionMetadataElement.LastOuterElementIndex;
            if (outerElementAccessDirection < 1)
            {
                enumerator.Dispose();
                enumerator = childCollectionElementFactory().GetEnumerator();
                childCollectionMetadataElement.Enumerator = enumerator;
                childCollectionMetadataElement.Previous = default;
            }

            childCollectionMetadataElement.LastOuterElementIndex = outerElementIndex;
            _childCollectionMetadata[childCollectionId] = childCollectionMetadataElement;
            //previous = childCollectionMetadataElement.Previous;
            
            var foundMatchingElement = false;
            //TInner result = default;
            while (true)
            {
                bool shouldCorrelate = false;

                if (childCollectionMetadataElement.Previous != null)
                {
                    shouldCorrelate = correlationnPredicate(outerKey, childCollectionMetadataElement.Previous.Item2);
                    //result = (TInner)childCollectionMetadataElement.Previous.Item1;
                }
                else
                {
                    if (!enumerator.MoveNext())
                    {
                        enumerator.Dispose();
                        _childCollectionMetadata[childCollectionId] = default;

                        break;
                    }

                    shouldCorrelate = correlationnPredicate(outerKey, enumerator.Current.Item2);
                    //result = enumerator.Current.Item1;
                }

                foundMatchingElement |= shouldCorrelate;
                childCollectionMetadataElement.Previous = default;

                if (shouldCorrelate)
                {
                    _childCollectionMetadata[childCollectionId] = childCollectionMetadataElement;

                    yield return enumerator.Current.Item1;
                }
                else
                {
                    // if the current element is not correlated with the parent, store it for the next comparison
                    if (outerElementAccessDirection == 1 || foundMatchingElement)
                    {
                        childCollectionMetadataElement.Previous = new Tuple<object, AnonymousObject2>(enumerator.Current.Item1, enumerator.Current.Item2);
                        _childCollectionMetadata[childCollectionId] = childCollectionMetadataElement;

                        // if inner element doesnt match and we are in sequential access mode, this means that all inners for a given outer have been iterated over and we can break;
                        // in case of non-sequential access, we can only stop iterating if we have found a match earlier - otherwise we need to keep looking, until we find a match or reach end of the stream
                        break;
                    }
                }

                //else
                //{
                //    break;
                //}
            }
        }









        /*






        private Tuple<object, AnonymousObject2> _previousElement = null;

        private Dictionary<int, int> _lastProcessedOuterMap = new Dictionary<int, int>();

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public virtual IEnumerable<TInner> CorrelateSubquery2<TInner>(
            int childCollectionId,
            int outerElementIndex,
            INavigation navigation,
            AnonymousObject2 outerKey,
            Func<IEnumerable<Tuple<TInner, AnonymousObject2, AnonymousObject2>>> childCollectionElementFactory,
            Func<AnonymousObject2, AnonymousObject2, bool> correlationnPredicate)
        {
            IDisposable untypedEnumerator = null;
            IEnumerator<Tuple<TInner, AnonymousObject2, AnonymousObject2>> enumerator = null;

            if (!_lastProcessedOuterMap.TryGetValue(childCollectionId, out var lastProcessedOuter))
            {
                lastProcessedOuter = -1;
            }

            // = 1 - sequential
            // > 1 - skipping forward, need to go thru reader until matching element is found or reaching end of the reader
            // < 1 - got back to earlier element, need to reset reader and then look for elements till find a match (just like with skipping forward case)
            var accessDirection = outerElementIndex - lastProcessedOuter;

            _lastProcessedOuterMap[childCollectionId] = outerElementIndex;

            if (childCollectionId == -1
                || !_childCollections.TryGetValue(childCollectionId, out untypedEnumerator)
                || accessDirection < 1)
            {
                enumerator = childCollectionElementFactory().GetEnumerator();

                if (!enumerator.MoveNext())
                {
                    enumerator.Dispose();
                    enumerator = null;
                }

                if (childCollectionId != -1)
                {
                    _childCollections[childCollectionId] = enumerator;
                }
            }

            if (enumerator == null)
            {
                if (untypedEnumerator == null)
                {
                    yield break;
                    //return Enumerable.Empty<TInner>();
                    //var clrCollectionAccessor = navigation.GetCollectionAccessor();

                    //var sequenceType = clrCollectionAccessor.CollectionType.TryGetSequenceType();
                    //if (sequenceType != typeof(TInner))
                    //{
                    //    return new List<TInner>();
                    //}

                    //var result = clrCollectionAccessor.Create();

                    //return (IEnumerable<TInner>)result;
                }

                enumerator = (IEnumerator<Tuple<TInner, AnonymousObject2, AnonymousObject2>>)untypedEnumerator;
            }

            while (true)
            {
                if (enumerator.Current == null)
                {
                    break;
                }

                var shouldCorrelate = correlationnPredicate(outerKey, enumerator.Current.Item2);

                var result = enumerator.Current.Item1;
                _previousElement = new Tuple<object, AnonymousObject2>(result, enumerator.Current.Item2);

                if (!enumerator.MoveNext())
                {
                    enumerator.Dispose();

                    if (childCollectionId != -1)
                    {
                        _childCollections[childCollectionId] = null;
                    }

                    //break;
                }

                if (shouldCorrelate)
                {
                    _previousElement = null;

                    yield return result;
                }
                else
                {
                    break;
                }
            }

            //foreach (var inner in childCollectionElementFactory())
            //{
            //    var shouldCorrelate = correlationnPredicate(outerKey, inner.Item2);
            //    if (shouldCorrelate)
            //    {
            //        yield return inner.Item1;
            //    }
            //}

            //var originKeysMap = new Dictionary<AnonymousObject2, int>();

            //var previousOriginKey = default(AnonymousObject2);
            //var firstCollection = true;

            //var elementCount = -1;

            //var inners = new List<TInner>();
            //while (true)
            //{
            //    var shouldInclude = correlationnPredicate(outerKey, enumerator.Current.Item2);
            //    if (shouldInclude)
            //    {
            //        if (originKeysMap.TryGetValue(outerKey, out var expectedCount)
            //            && expectedCount > elementCount)
            //        {
            //            shouldInclude = false;
            //        }

            //        if (!firstCollection
            //            && outerKey != previousOriginKey)
            //        {
            //            shouldInclude = false;

            //            originKeysMap[previousOriginKey] = elementCount;
            //        }
            //    }

            //    firstCollection = false;
            //    previousOriginKey = outerKey;


            //    if (shouldInclude)
            //    {
            //        inners.Add(enumerator.Current.Item1);

            //        elementCount++;

            //        if (!enumerator.MoveNext())
            //        {
            //            enumerator.Dispose();

            //            if (childCollectionId != -1)
            //            {
            //                _childCollections[childCollectionId] = null;
            //            }

            //            break;
            //        }
            //    }
            //    else
            //    {
            //        //originKeysMap[previousOriginKey] = elementCount;
            //        //previousOriginKey = outerKey;

            //        //firstCollection = false;
            //        //previousOriginKey = outerKey;

            //        break;
            //    }
            //}

            //return inners;
        }





    */




























        ///// <summary>
        /////     This API supports the Entity Framework Core infrastructure and is not intended to be used
        /////     directly from your code. This API may change or be removed in future releases.
        ///// </summary>
        //public IEnumerable<TInner> CorrelateSubqueryStreaming<TInner>(
        //    IEnumerable<Tuple<TInner, AnonymousObject2, AnonymousObject2>> source,
        //    int childCollectionId, 
        //    INavigation navigation, 
        //    AnonymousObject2 outerKey, 
        //    Func<AnonymousObject2, AnonymousObject2, bool> correlationnPredicate)
        //{
        //    IDisposable untypedEnumerator = null;
        //    IEnumerator<Tuple<TInner, AnonymousObject2, AnonymousObject2>> enumerator = null;

        //    if (childCollectionId == -1
        //        || !_childCollections.TryGetValue(childCollectionId, out untypedEnumerator))
        //    {
        //        enumerator = source.GetEnumerator();

        //        if (!enumerator.MoveNext())
        //        {
        //            enumerator.Dispose();
        //            enumerator = null;
        //        }

        //        if (childCollectionId != -1)
        //        {
        //            _childCollections.Add(childCollectionId, enumerator);
        //        }
        //    }

        //    if (enumerator == null)
        //    {
        //        //if (untypedEnumerator == null)
        //        //{
        //        //    var clrCollectionAccessor = navigation.GetCollectionAccessor();

        //        //    var sequenceType = clrCollectionAccessor.CollectionType.TryGetSequenceType();
        //        //    if (sequenceType != typeof(TInner))
        //        //    {
        //        //        return new List<TInner>();
        //        //    }

        //        //    var result = clrCollectionAccessor.Create();

        //        //    return (IEnumerable<TInner>)result;
        //        //}

        //        enumerator = (IEnumerator<Tuple<TInner, AnonymousObject2, AnonymousObject2>>)untypedEnumerator;
        //    }

        //    foreach (var inner in source)
        //    {

        //    }


        //    while (true)
        //    {


        //        var shouldInclude = correlationnPredicate(outerKey, enumerator.Current.Item2);

        //    }



















        //    var originKeysMap = new Dictionary<AnonymousObject2, int>();

        //    var previousOriginKey = default(AnonymousObject2);
        //    var firstCollection = true;

        //    var elementCount = -1;

        //    var inners = new List<TInner>();
        //    while (true)
        //    {
        //        var shouldInclude = correlationnPredicate(outerKey, enumerator.Current.Item2);
        //        if (shouldInclude)
        //        {
        //            if (originKeysMap.TryGetValue(outerKey, out var expectedCount)
        //                && expectedCount > elementCount)
        //            {
        //                shouldInclude = false;
        //            }

        //            if (!firstCollection
        //                && outerKey != previousOriginKey)
        //            {
        //                shouldInclude = false;

        //                originKeysMap[previousOriginKey] = elementCount;
        //            }
        //        }

        //        firstCollection = false;
        //        previousOriginKey = outerKey;

        //        if (shouldInclude)
        //        {
        //            inners.Add(enumerator.Current.Item1);
        //            elementCount++;

        //            if (!enumerator.MoveNext())
        //            {
        //                enumerator.Dispose();

        //                if (childCollectionId != -1)
        //                {
        //                    _childCollections[childCollectionId] = null;
        //                }

        //                break;
        //            }
        //        }
        //        else
        //        {
        //            break;
        //        }
        //    }

        //    return inners;
        //}

        void IDisposable.Dispose()
        {
            foreach (var kv in _includedCollections)
            {
                kv.Value?.Dispose();
            }
        }

    }
}