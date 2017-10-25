// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.EntityFrameworkCore.Metadata;
using JetBrains.Annotations;
using Remotion.Linq.Clauses;

namespace Microsoft.EntityFrameworkCore.Query
{
    /// <summary>
    ///     Structure to store metadata needed for correlated collection optimizations.
    /// </summary>
    public class CorrelatedSubqueryMetadata
    {
        /// <summary>
        ///      Id associated with the collection that is being optimized.
        /// </summary>
        public virtual int Id { get; set; }

        /// <summary>
        ///     First navigation in the chain leading to collection navigation that is being optimized.
        /// </summary>
        public virtual INavigation FirstNavigation { get; [param: NotNull] set; }

        /// <summary>
        ///     Collection navigation that is being optimized.
        /// </summary>
        public virtual INavigation CollectionNavigation { get; [param: NotNull] set; }

        /// <summary>
        ///     Query source that is origin of the collection navigation.
        /// </summary>
        public virtual IQuerySource ParentQuerySource { get; [param: NotNull] set; }
    }
}
