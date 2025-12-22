using System;
using System.Collections.Generic;
using System.Threading;

namespace Baubit.Caching
{
    /// <summary>
    /// Default implementation of <see cref="ICacheAsyncEnumeratorFactory{TValue}"/>.
    /// Creates standard cache enumerators and future enumerators.
    /// </summary>
    /// <typeparam name="TValue">The type of value in the cache entries.</typeparam>
    public class CacheAsyncEnumeratorFactory<TValue> : ICacheAsyncEnumeratorFactory<TValue>
    {
        /// <inheritdoc/>
        public IAsyncEnumerator<IEntry<TValue>> CreateEnumerator(
            IOrderedCache<TValue> cache,
            Action<ICacheEnumerator> onDispose,
            CancellationToken cancellationToken)
        {
            return new CacheAsyncEnumerator<TValue>(cache, onDispose, cancellationToken);
        }

        /// <inheritdoc/>
        public IAsyncEnumerator<IEntry<TValue>> CreateFutureEnumerator(
            IOrderedCache<TValue> cache,
            Action<ICacheEnumerator> onDispose,
            CancellationToken cancellationToken)
        {
            return new CacheFutureAsyncEnumerator<TValue>(cache, onDispose, cancellationToken);
        }
    }
}
