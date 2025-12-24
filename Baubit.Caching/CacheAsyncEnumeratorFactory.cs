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
    public class CacheAsyncEnumeratorFactory<TId, TValue> : ICacheAsyncEnumeratorFactory<TId, TValue> where TId : struct, IComparable<TId>, IEquatable<TId>
    {
        /// <inheritdoc/>
        public IAsyncEnumerator<IEntry<TId, TValue>> CreateEnumerator(
            IOrderedCache<TId, TValue> cache,
            Action<ICacheEnumerator<TId>> onDispose,
            CancellationToken cancellationToken)
        {
            return new CacheAsyncEnumerator<TId, TValue>(cache, onDispose, cancellationToken);
        }

        /// <inheritdoc/>
        public IAsyncEnumerator<IEntry<TId, TValue>> CreateFutureEnumerator(
            IOrderedCache<TId, TValue> cache,
            Action<ICacheEnumerator<TId>> onDispose,
            CancellationToken cancellationToken)
        {
            return new CacheFutureAsyncEnumerator<TId, TValue>(cache, onDispose, cancellationToken);
        }
    }
}
