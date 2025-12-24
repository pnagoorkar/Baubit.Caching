using System;
using System.Collections.Generic;
using System.Threading;

namespace Baubit.Caching
{
    /// <summary>
    /// Factory interface for creating cache enumerators.
    /// Enables dependency injection and testability by decoupling enumerator creation from the cache.
    /// </summary>
    /// <typeparam name="TValue">The type of value in the cache entries.</typeparam>
    public interface ICacheAsyncEnumeratorFactory<TId, TValue> where TId : struct, IComparable<TId>, IEquatable<TId>
    {
        /// <summary>
        /// Creates an asynchronous enumerator that iterates through cache entries from the current head.
        /// </summary>
        /// <param name="cache">The cache to enumerate.</param>
        /// <param name="onDispose">Callback invoked when the enumerator is disposed.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous enumeration.</param>
        /// <returns>An asynchronous enumerator for the cache entries.</returns>
        IAsyncEnumerator<IEntry<TId, TValue>> CreateEnumerator(
            IOrderedCache<TId, TValue> cache,
            Action<ICacheEnumerator<TId>> onDispose,
            CancellationToken cancellationToken);

        /// <summary>
        /// Creates an asynchronous enumerator that iterates through future cache entries starting from the current tail.
        /// This enumerator waits for new entries to be added to the cache.
        /// </summary>
        /// <param name="cache">The cache to enumerate.</param>
        /// <param name="onDispose">Callback invoked when the enumerator is disposed.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous enumeration.</param>
        /// <returns>An asynchronous enumerator for future cache entries.</returns>
        IAsyncEnumerator<IEntry<TId, TValue>> CreateFutureEnumerator(
            IOrderedCache<TId, TValue> cache,
            Action<ICacheEnumerator<TId>> onDispose,
            CancellationToken cancellationToken);
    }
}
