// Copyright (c) .NET Foundation. All rights reserved.
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

        private Dictionary<int, ChildCollectionMetadataElement> _childCollectionMetadata
            = new Dictionary<int, ChildCollectionMetadataElement>();

        private struct ChildCollectionMetadataElement
        {
            public ChildCollectionMetadataElement(
                IDisposable enumerator, 
                int lastOuterElementIndex, 
                int maxInnerElementIndex, 
                Tuple<object, AnonymousObject2> previous)
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
            
            var foundMatchingElement = false;
            while (true)
            {
                bool shouldCorrelate = false;

                if (childCollectionMetadataElement.Previous != null)
                {
                    shouldCorrelate = correlationnPredicate(outerKey, childCollectionMetadataElement.Previous.Item2);
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
            }
        }

        void IDisposable.Dispose()
        {
            foreach (var kv in _includedCollections)
            {
                kv.Value?.Dispose();
            }
        }
    }
}