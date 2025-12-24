using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Baubit.Caching
{
    /// <summary>
    /// Base class for asynchronous enumerators over cache entries.
    /// </summary>
    /// <typeparam name="TValue">The type of value in the cache entry.</typeparam>
    public abstract class BaseCacheAsyncEnumerator<TId, TValue> : IAsyncEnumerator<IEntry<TId, TValue>>, ICacheEnumerator<TId> where TId : struct, IComparable<TId>, IEquatable<TId>
    {
        /// <summary>
        /// Gets the current cache entry.
        /// </summary>
        public IEntry<TId, TValue> Current { get; protected set; }
        /// <summary>
        /// Gets the identifier of the current entry, or <c>null</c> if not positioned.
        /// </summary>
        public TId? CurrentId => Current?.Id;

        protected readonly IOrderedCache<TId, TValue> cache;
        private Action<ICacheEnumerator<TId>> onDispose;
        private CancellationToken cancellationToken;
        private CancellationTokenRegistration cancellationTokenRegistration;
        /// <summary>
        /// Initializes a new instance of the <see cref="BaseCacheAsyncEnumerator{TValue}"/> class.
        /// </summary>
        /// <param name="cache">The cache to enumerate.</param>
        /// <param name="onDispose">Callback invoked when the enumerator is disposed.</param>
        /// <param name="cancellationToken">A token to cancel the enumeration.</param>
        public BaseCacheAsyncEnumerator(IOrderedCache<TId, TValue> cache,
                                    Action<ICacheEnumerator<TId>> onDispose,
                                    CancellationToken cancellationToken = default)
        {
            this.cache = cache;
            this.onDispose = onDispose;
            this.cancellationToken = cancellationToken;
            cancellationTokenRegistration = this.cancellationToken.Register(() => DisposeAsync());
        }
        /// <summary>
        /// Disposes the enumerator asynchronously.
        /// </summary>
        /// <returns>A value task representing the asynchronous dispose operation.</returns>
        public virtual ValueTask DisposeAsync()
        {
            onDispose?.Invoke(this);
            cancellationTokenRegistration.Dispose();
            return default(ValueTask);
        }
        /// <summary>
        /// Advances the enumerator asynchronously to the next entry.
        /// </summary>
        /// <returns><c>true</c> if the enumerator was advanced; otherwise <c>false</c>.</returns>
        public virtual async ValueTask<bool> MoveNextAsync()
        {
            if (cancellationToken.IsCancellationRequested) return false;
            try
            {
                Current = await cache.GetNextAsync(CurrentId, cancellationToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                // expected when cancellationToken is cancelled
                return false;
            }
            return !cancellationToken.IsCancellationRequested;
        }
    }
}