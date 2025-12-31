using Baubit.Collections;
using System;
using System.Linq;

namespace Baubit.Caching
{
    /// <summary>
    /// A thread-safe collection of cache enumerators that tracks active enumerators and provides
    /// access to the lowest read position across all enumerators.
    /// </summary>
    public class CacheEnumeratorCollection<TId> : ConcurrentList<ICacheEnumerator<TId>> where TId : struct, IComparable<TId>, IEquatable<TId>
    {
        /// <summary>
        /// Gets the lowest (earliest) entry ID that has been read across all active enumerators.
        /// Returns <c>null</c> if the collection is empty or if no enumerator has read any entries yet.
        /// This is used to determine which cache entries can be safely evicted.
        /// </summary>
        public TId? LowestReadId 
        {
            get => this.Min(e => e.CurrentId);
        }
    }
}
