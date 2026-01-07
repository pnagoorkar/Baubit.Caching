using System;
using System.Threading;

namespace Baubit.Caching
{
    /// <summary>
    /// Asynchronous enumerator for future cache entries, starting from the current tail.
    /// </summary>
    /// <typeparam name="TId">The type of the entry identifier. Must be a struct implementing IComparable&lt;TId&gt; and IEquatable&lt;TId&gt;.</typeparam>
    /// <typeparam name="TValue">The type of value in the cache entry.</typeparam>
    public class CacheFutureAsyncEnumerator<TId, TValue> : BaseCacheAsyncEnumerator<TId, TValue> where TId : struct, IComparable<TId>, IEquatable<TId>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CacheFutureAsyncEnumerator{TId, TValue}"/> class.
        /// </summary>
        /// <param name="cache">The cache to enumerate.</param>
        /// <param name="onDispose">Callback invoked when the enumerator is disposed.</param>
        /// <param name="cancellationToken">A token to cancel the enumeration.</param>
        /// <param name="name">The name of the enumerator. If not provided, a new GUID will be generated.</param>
        public CacheFutureAsyncEnumerator(IOrderedCache<TId, TValue> cache,
                                          Action<ICacheEnumerator<TId>> onDispose,
                                          CancellationToken cancellationToken = default,
                                          string name = null) : base(cache, onDispose, cancellationToken, name)
        {
            cache.GetLastOrDefault(out var lastEntry);
            Current = lastEntry; // this to ensure the evictor knows we are not interested in any entries through the current tail
        }
    }
}