using Baubit.Tasks;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Baubit.Caching
{
    /// <summary>
    /// Base class for ordered cache implementations with generic identifier support.
    /// Provides two-tier storage (L1/L2), adaptive resizing, and async enumeration capabilities.
    /// </summary>
    /// <typeparam name="TId">The type of the entry identifier. Must be a struct implementing IComparable&lt;TId&gt; and IEquatable&lt;TId&gt;.</typeparam>
    /// <typeparam name="TValue">The type of values stored in the cache.</typeparam>
    public class OrderedCache<TId, TValue> : IOrderedCache<TId, TValue> where TId : struct, IComparable<TId>, IEquatable<TId>
    {
        /// <summary>
        /// Gets the runtime configuration for this cache instance.
        /// </summary>
        public Configuration Configuration { get; private set; }

        /// <inheritdoc/>
        public long Count { get => metadata.Count; }

        #region PrivateMembers
        private bool disposedValue;

        private Task<bool> adaptionRunner;
        private CancellationTokenSource adaptionCTS;
        private readonly ILogger<OrderedCache<TId, TValue>> logger;
        private readonly ICacheAsyncEnumeratorFactory<TId, TValue> enumeratorFactory;

        private readonly CacheEnumeratorCollection<TId> activeEnumerators;
        private int additionsSinceLastEviction = 0;
        #endregion

        #region ProtectedMembers
        protected readonly IStore<TId, TValue> l1Store;
        protected readonly IStore<TId, TValue> l2Store;
        protected readonly IMetadata<TId> metadata;

        /// <summary>
        /// A reader/writer lock guarding mutations and multi-field reads to ensure thread safety.
        /// </summary>
        protected readonly ReaderWriterLockSlim Locker = new ReaderWriterLockSlim();
        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="OrderedCache{TId, TValue}"/> class.
        /// </summary>
        /// <param name="cacheConfiguration">The cache configuration.</param>
        /// <param name="l1Store">Optional bounded L1 store (e.g., in-memory) for hot entries.</param>
        /// <param name="l2Store">Backing L2 store that must persist every entry.</param>
        /// <param name="metadata">Metadata that tracks head/tail ids and next-id lookups.</param>
        /// <param name="loggerFactory">Factory to create a logger for diagnostics and tracing.</param>
        /// <param name="cacheEnumeratorCollectionFactory">Optional factory for creating a cache enumerator collection. If null, uses default collection.</param>
        /// <param name="enumeratorFactory">Optional factory for creating enumerators. If null, uses default factory.</param>
        public OrderedCache(Configuration cacheConfiguration,
                            IStore<TId, TValue> l1Store,
                            IStore<TId, TValue> l2Store,
                            IMetadata<TId> metadata,
                            ILoggerFactory loggerFactory,
                            Func<CacheEnumeratorCollection<TId>> cacheEnumeratorCollectionFactory = null,
                            ICacheAsyncEnumeratorFactory<TId, TValue> enumeratorFactory = null)
        {
            logger = loggerFactory.CreateLogger<OrderedCache<TId, TValue>>();
            Configuration = cacheConfiguration;
            this.l1Store = l1Store;
            this.l2Store = l2Store;
            this.metadata = metadata;
            this.activeEnumerators = cacheEnumeratorCollectionFactory?.Invoke() ?? new CacheEnumeratorCollection<TId>();
            this.enumeratorFactory = enumeratorFactory ?? new CacheAsyncEnumeratorFactory<TId, TValue>();
            if (this.l1Store != null && !this.l1Store.Uncapped && Configuration?.RunAdaptiveResizing == true)
            {
                // Start a background loop that adjusts L1 capacity based on production rate.
                adaptionCTS = new CancellationTokenSource();
                adaptionRunner = RunAdaptiveResizing(adaptionCTS.Token);
            }
        }

        #region AdaptiveResizing
        /// <summary>
        /// Periodically adjusts the L1 capacity by measuring the rate at which new entries are produced.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to terminate the loop.</param>
        /// <returns><c>true</c> if the loop exits normally or is canceled; otherwise throws.</returns>
        private async Task<bool> RunAdaptiveResizing(CancellationToken cancellationToken = default)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(Configuration.AdaptionWindowMS, cancellationToken).ConfigureAwait(false);
                    var roomsThisCycle = metadata.ResetRoomCount();
                    logger.LogDebug($"Rooms this cycle: {roomsThisCycle}");

                    double roomRate = roomsThisCycle * 1_000.0 / Configuration.AdaptionWindowMS;

                    logger.LogTrace($"Room rate: {roomRate}");

                    if (roomRate > Configuration.RoomRateUpperLimit)
                    {
                        l1Store?.AddCapacity(Configuration.GrowStep);
                        logger.LogTrace($"Resized L1Store. New size: {l1Store?.TargetCapacity}");
                    }
                    else if (roomRate < Configuration.RoomRateLowerLimit)
                    {
                        l1Store?.CutCapacity(Configuration.ShrinkStep);
                        logger.LogTrace($"Resized L1Store. New size: {l1Store?.TargetCapacity}");
                    }
                    Locker.EnterWriteLock();
                    try { ReplenishL1Store(); }
                    catch { throw; }
                    finally { Locker.ExitWriteLock(); }
                }
                return true;
            }
            catch (TaskCanceledException)
            {
                return true;
            }
            catch
            {
                throw;
            }
        }
        #endregion

        /// <inheritdoc/>
        public bool Add(TValue value, out IEntry<TId, TValue> entry)
        {
            Locker.EnterWriteLock();
            try
            {
                if (disposedValue) { entry = default(IEntry<TId, TValue>); return false; }
                if (!l2Store.Add(value, out entry)) return false;
                if (l1Store?.HasCapacity == true)
                {
                    l1Store.Add(entry);
                }
                if (!metadata.AddTail(entry.Id)) return false;
                if (!TryEvict()) return false;
                return true;
            }
            finally { Locker.ExitWriteLock(); }
        }

        /// <summary>
        /// Attempts to evict entries that have been read by all active enumerators.
        /// Called periodically after a configured number of additions to maintain memory efficiency.
        /// <para><b>Critical:</b> This method is <b>not thread-safe</b>. Callers are expected to ensure thread safety before calling.</para>
        /// </summary>
        /// <returns><c>true</c> on success; otherwise <c>false</c>.</returns>
        protected bool TryEvict()
        {
            if (Configuration != null && ++additionsSinceLastEviction >= Configuration.EvictAfterEveryX)
            {
                var lowestId = activeEnumerators.LowestReadId;
                // If lowestId is null, check if there are active enumerators
                if (lowestId == null)
                {
                    // If there are active enumerators, at least one hasn't read the head yet - respect the reader
                    if (activeEnumerators.Count > 0) return true;
                    // If there are no active enumerators, evict all entries up to the tail
                    lowestId = metadata.TailId;
                }
                if (lowestId != null)
                {
                    metadata.GetIdsThrough(lowestId.Value, out var ids);
                    foreach (var id in ids)
                    {
                        RemoveInternal(id, out _);
                    }
                }
                additionsSinceLastEviction = 0;
            }
            return true;
        }

        /// <inheritdoc/>
        public bool Update(TId id, TValue value)
        {
            Locker.EnterWriteLock();
            try
            {
                if (disposedValue) { return false; }
                return l2Store.Update(id, value) && l1Store == null ? true : l1Store.Update(id, value);
            }
            finally { Locker.ExitWriteLock(); }
        }

        /// <inheritdoc/>
        public bool GetEntryOrDefault(TId? id, out IEntry<TId, TValue> entry)
        {
            Locker.EnterReadLock();
            try
            {
                if (disposedValue) { entry = default(IEntry<TId, TValue>); return false; }
                return GetEntryOrDefaultInternal(id, out entry);
            }
            finally { Locker.ExitReadLock(); }
        }

        /// <summary>
        /// Internal implementation of <see cref="GetEntryOrDefault"/> without locking.
        /// Searches L1 first, then falls back to L2 if not found.
        /// </summary>
        /// <param name="id">The entry identifier.</param>
        /// <param name="entry">On success, the located entry; otherwise <c>null</c>.</param>
        /// <returns><c>true</c> if the lookup succeeded (even when not found); otherwise <c>false</c>.</returns>
        private bool GetEntryOrDefaultInternal(TId? id, out IEntry<TId, TValue> entry)
        {
            entry = default(IEntry<TId, TValue>);
            if (id.HasValue && metadata.ContainsKey(id.Value))
            {
                if (l1Store?.GetEntryOrDefault(id, out entry) == true)
                {
                    return true;
                }
                else if (l2Store.GetEntryOrDefault(id, out entry))
                {
                    return true;
                }
            }
            return true;
        }

        /// <inheritdoc/>
        public bool GetNextOrDefault(TId? id, out IEntry<TId, TValue> entry)
        {
            Locker.EnterReadLock();
            try
            {
                if (disposedValue) { entry = default(IEntry<TId, TValue>); return false; }
                return GetNextOrDefaultInternal(id, out entry);
            }
            finally { Locker.ExitReadLock(); }
        }

        /// <summary>
        /// Internal implementation of <see cref="GetNextOrDefault"/> without locking.
        /// Uses metadata to determine the next ID and then retrieves the corresponding entry.
        /// </summary>
        /// <param name="id">The current identifier.</param>
        /// <param name="entry">On success, the next entry; otherwise <c>null</c>.</param>
        /// <returns><c>true</c> if the lookup succeeded (even when not found); otherwise <c>false</c>.</returns>
        private bool GetNextOrDefaultInternal(TId? id, out IEntry<TId, TValue> entry)
        {
            entry = default(IEntry<TId, TValue>);
            return metadata.GetNextId(id, out var nextId) && GetEntryOrDefaultInternal(nextId, out entry);

        }

        /// <inheritdoc/>
        public bool GetFirstOrDefault(out IEntry<TId, TValue> entry)
        {
            Locker.EnterReadLock();
            try
            {
                if (disposedValue) { entry = default(IEntry<TId, TValue>); return false; }
                entry = default(IEntry<TId, TValue>);
                return GetEntryOrDefaultInternal(metadata.HeadId, out entry);
            }
            finally { Locker.ExitReadLock(); }
        }

        /// <inheritdoc/>
        public bool GetFirstIdOrDefault(out TId? id)
        {
            Locker.EnterReadLock();
            try
            {
                if (disposedValue) { id = default(TId?); return false; }
                id = metadata.HeadId;
                return true;
            }
            finally { Locker.ExitReadLock(); }
        }

        /// <inheritdoc/>
        public bool GetLastOrDefault(out IEntry<TId, TValue> entry)
        {
            Locker.EnterReadLock();
            try
            {
                if (disposedValue) { entry = default(IEntry<TId, TValue>); return false; }
                entry = default(IEntry<TId, TValue>);
                return GetEntryOrDefaultInternal(metadata.TailId, out entry);
            }
            finally { Locker.ExitReadLock(); }
        }

        /// <inheritdoc/>
        public bool GetLastIdOrDefault(out TId? id)
        {
            Locker.EnterReadLock();
            try
            {
                if (disposedValue) { id = default(TId?); return false; }
                id = metadata.TailId;
                return true;
            }
            finally { Locker.ExitReadLock(); }
        }

        /// <inheritdoc/>
        public Task<IEntry<TId, TValue>> GetNextAsync(TId? id = null, CancellationToken cancellationToken = default)
        {
            Locker.EnterReadLock();
            try
            {
                if (disposedValue) { Task.FromCanceled<IEntry<TId, TValue>>(cancellationToken); }
                if (GetNextOrDefaultInternal(id, out var entry) && entry != null)
                {
                    return Task.FromResult(entry);
                }
                else
                {
                    return GetFutureFirstOrDefaultAsyncInternal(cancellationToken);
                }
            }
            finally { Locker.ExitReadLock(); }
        }

        /// <summary>
        /// Internal implementation of future entry retrieval without locking.
        /// Waits for the next entry to be added after the current tail.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the wait.</param>
        /// <returns>A task that completes with the next entry to be added.</returns>
        private Task<IEntry<TId, TValue>> GetFutureFirstOrDefaultAsyncInternal(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested) { Task.FromCanceled<IEntry<TId, TValue>>(cancellationToken); }
            var currentTailId = metadata.TailId;
            return metadata.GetNextIdAsync(currentTailId, cancellationToken)
                            .ContinueWith(task =>
                            {
                                if (task.IsCanceled) throw new TaskCanceledException();
                                GetEntryOrDefault(task.Result, out var nextEntry);
                                return nextEntry;
                            }, cancellationToken);
        }

        /// <inheritdoc/>
        public Task<IEntry<TId, TValue>> GetFutureFirstOrDefaultAsync(CancellationToken cancellationToken = default)
        {
            Locker.EnterReadLock();
            try
            {
                return GetFutureFirstOrDefaultAsyncInternal(cancellationToken);
            }
            finally { Locker.ExitReadLock(); }
        }

        /// <inheritdoc/>
        public bool Remove(TId id, out IEntry<TId, TValue> entry)
        {
            Locker.EnterWriteLock();
            try
            {
                return RemoveInternal(id, out entry);
            }
            finally { Locker.ExitWriteLock(); }
        }

        /// <summary>
        /// Internal implementation of entry removal without external locking.
        /// Removes from L2, L1 (if present), metadata, and triggers L1 replenishment.
        /// </summary>
        /// <param name="id">The identifier of the entry to remove.</param>
        /// <param name="entry">On success, the removed entry.</param>
        /// <returns><c>true</c> if the entry was removed; otherwise <c>false</c>.</returns>
        private bool RemoveInternal(TId id, out IEntry<TId, TValue> entry)
        {
            if (disposedValue) { entry = default(IEntry<TId, TValue>); return false; }
            entry = null;
            if (!l2Store.Remove(id, out var l2Entry)) return false;
            if (l1Store?.GetEntryOrDefault(id, out var l1Entry) == true && l1Entry != null)
            {
                if (!l1Store.Remove(id, out l1Entry)) return false;
            }
            if (!metadata.Remove(id)) return false;
            if (!ReplenishL1Store()) return false;
            entry = l2Entry;
            return true;
        }

        /// <summary>
        /// Fills the L1 store from L2 until either L1 reaches its capacity or there are no more
        /// entries between the L1 tail and the global tail.
        /// <para><b>Critical:</b> This method is <b>not thread-safe</b>. Callers are expected to ensure thread safety before calling.</para>
        /// </summary>
        /// <returns><c>true</c> always; the method is best‑effort.</returns>
        protected bool ReplenishL1Store()
        {
            while (l1Store?.HasCapacity == true &&
                   metadata.GetNextId(l1Store.LastAddedId, out var nextId) &&
                   l2Store.GetEntryOrDefault(nextId, out var nextEntry) &&
                   nextEntry != null && l1Store.Add(nextEntry));
            return true;
        }

        /// <inheritdoc/>
        public bool Clear()
        {
            Locker.EnterWriteLock();
            try
            {
                return ClearInternal();
            }
            finally { Locker.ExitWriteLock(); }
        }

        /// <summary>
        /// Internal implementation of cache clearing without external locking.
        /// Removes all entries by iterating through metadata and calling RemoveInternal for each.
        /// </summary>
        /// <returns><c>true</c> on success; otherwise <c>false</c>.</returns>
        private bool ClearInternal()
        {
            if (disposedValue) { return false; }
            if (metadata.TailId == null) return true;
            metadata.GetIdsThrough(metadata.TailId.Value, out var ids);
            foreach (var id in ids)
            {
                RemoveInternal(id, out _);
            }
            return true;
        }

        /// <summary>
        /// Returns an asynchronous enumerator that iterates through the cache entries from the current head.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the asynchronous enumeration.</param>
        /// <returns>An asynchronous enumerator for the cache entries.</returns>
        public IAsyncEnumerator<IEntry<TId, TValue>> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            var retVal = enumeratorFactory.CreateEnumerator(this, e => activeEnumerators.Remove(e), cancellationToken);
            activeEnumerators.Add(retVal as ICacheEnumerator<TId>);
            return retVal;
        }

        /// <summary>
        /// Returns an asynchronous enumerator that iterates through future cache entries starting from the current tail.
        /// This enumerator waits for new entries to be added to the cache.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the asynchronous enumeration.</param>
        /// <returns>An asynchronous enumerator for future cache entries.</returns>
        public IAsyncEnumerator<IEntry<TId, TValue>> GetFutureAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            var retVal = enumeratorFactory.CreateFutureEnumerator(this, e => activeEnumerators.Remove(e), cancellationToken);
            activeEnumerators.Add(retVal as ICacheEnumerator<TId>);
            return retVal;
        }

        /// <inheritdoc/>
        public async IAsyncEnumerable<(TId, T)> EnumerateAsync<T>([EnumeratorCancellation] CancellationToken cancellationToken = default) where T : TValue
        {
            var enumerator = GetAsyncEnumerator(cancellationToken);
            while (await enumerator.MoveNextAsync())
            {
                if (enumerator.Current.Value is T value)
                {
                    yield return (enumerator.Current.Id, value);
                }
            }
        }

        /// <inheritdoc/>
        public async IAsyncEnumerable<(TId, T)> EnumerateFutureAsync<T>([EnumeratorCancellation] CancellationToken cancellationToken = default) where T : TValue
        {
            var enumerator = GetFutureAsyncEnumerator(cancellationToken);
            while (await enumerator.MoveNextAsync())
            {
                if (enumerator.Current.Value is T value)
                {
                    yield return (enumerator.Current.Id, value);
                }
            }
        }

        /// <inheritdoc/>
        public async Task<bool> OnNextAsync<T>(Func<(TId, T), object, Task<bool>> handler, 
                                               object state, 
                                               CancellationToken cancellationToken = default) where T : TValue
        {
            await foreach (var tuple in EnumerateFutureAsync<T>(cancellationToken))
            {
                await handler?.Invoke(tuple, state);
            }
            return true;
        }

        /// <summary>
        /// Releases managed and unmanaged resources.
        /// </summary>
        /// <param name="disposing">When <c>true</c>, called from <see cref="Dispose()"/>; otherwise from the finalizer.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Locker.EnterWriteLock();
                    try
                    {
                        adaptionCTS?.Cancel();
                        adaptionRunner?.Wait(true);
                        l1Store?.Dispose();
                        l2Store?.Dispose();
                    }
                    finally { Locker.ExitWriteLock(); }
                    Locker.Dispose();
                }
                disposedValue = true;
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}