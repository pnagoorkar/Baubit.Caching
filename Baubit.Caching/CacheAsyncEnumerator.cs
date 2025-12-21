using System;
using System.Threading;

namespace Baubit.Caching
{
    /// <summary>
    /// Asynchronous enumerator for cache entries, starting from the current position.
    /// </summary>
    /// <typeparam name="TValue">The type of value in the cache entry.</typeparam>
    public class CacheAsyncEnumerator<TValue> : BaseCacheAsyncEnumerator<TValue>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CacheAsyncEnumerator{TValue}"/> class.
        /// </summary>
        /// <param name="cache">The cache to enumerate.</param>
        /// <param name="onDispose">Callback invoked when the enumerator is disposed.</param>
        /// <param name="cancellationToken">A token to cancel the enumeration.</param>
        public CacheAsyncEnumerator(IOrderedCache<TValue> cache,
                                    Action<ICacheEnumerator> onDispose,
                                    CancellationToken cancellationToken = default) : base(cache, onDispose, cancellationToken)
        {
        }
    }
}