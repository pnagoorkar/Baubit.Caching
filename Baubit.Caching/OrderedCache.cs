using Baubit.Collections;
using Baubit.Tasks;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Baubit.Caching
{
    /// <summary>
    /// Default <see cref="IOrderedCache{TValue}"/> implementation that composes an optional bounded L1 store
    /// and a required L2 store, with metadata to maintain ordering. Supports adaptive resizing of the L1 store.
    /// </summary>
    /// <typeparam name="TValue">The element type held in the cache.</typeparam>
    public class OrderedCache<TValue> : IOrderedCache<TValue>
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
        private readonly ILogger<OrderedCache<TValue>> logger;

        private readonly IList<ICacheEnumerator> activeEnumerators = new ConcurrentList<ICacheEnumerator>();
        private int additionsSinceLastEviction = 0;
        #endregion

        #region ProtectedMembers
        protected readonly IStore<TValue> l1Store;
        protected readonly IStore<TValue> l2Store;
        protected readonly IMetadata metadata;
        /// <summary>
        /// A reader/writer lock guarding mutations and multi-field reads.
        /// </summary>
        protected readonly ReaderWriterLockSlim Locker = new ReaderWriterLockSlim();
        #endregion

        /// <summary>
        /// Creates a new <see cref="OrderedCache{TValue}"/>.
        /// </summary>
        /// <param name="cacheConfiguration">The cache configuration.</param>
        /// <param name="l1Store">Optional bounded L1 store (e.g., in-memory) for hot entries.</param>
        /// <param name="l2Store">Backing L2 store that must persist every entry.</param>
        /// <param name="metadata">Metadata that tracks head/tail ids and next-id lookups.</param>
        /// <param name="loggerFactory">Factory to create a logger for diagnostics and tracing.</param>
        public OrderedCache(Configuration cacheConfiguration,
                            IStore<TValue> l1Store,
                            IStore<TValue> l2Store,
                            IMetadata metadata,
                            ILoggerFactory loggerFactory)
        {
            logger = loggerFactory.CreateLogger<OrderedCache<TValue>>();
            Configuration = cacheConfiguration;
            this.l1Store = l1Store;
            this.l2Store = l2Store;
            this.metadata = metadata;
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
        public bool Add(TValue value, out IEntry<TValue> entry)
        {
            Locker.EnterWriteLock();
            try
            {
                if (disposedValue) { entry = default(IEntry<TValue>); return false; }
                if (!l2Store.Add(value, out entry)) return false;
                if (l1Store?.HasCapacity == true)
                {
                    if (!l1Store.Add(entry)) return false;
                }
                if (!metadata.AddTail(entry.Id)) return false;
                if (!TryEvict()) return false;
                return true;
            }
            finally { Locker.ExitWriteLock(); }
        }

        private bool TryEvict()
        {
            if (Configuration != null && ++additionsSinceLastEviction >= Configuration.EvictAfterEveryX)
            {
                var lowestId = activeEnumerators.Min(e => e.CurrentId);
                if (lowestId == null) return true; // there is at least 1 enumerator that hasnt read even the head. respect the reader and short circuit
                metadata.GetIdsThrough(lowestId.Value, out var ids);
                foreach (var id in ids)
                {
                    RemoveInternal(id, out _);
                }
                additionsSinceLastEviction = 0;
            }
            return true;
        }

        /// <inheritdoc/>
        public bool Update(Guid id, TValue value)
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
        public bool GetEntryOrDefault(Guid? id, out IEntry<TValue> entry)
        {
            Locker.EnterReadLock();
            try
            {
                if (disposedValue) { entry = default(IEntry<TValue>); return false; }
                return GetEntryOrDefaultInternal(id, out entry);
            }
            finally { Locker.ExitReadLock(); }
        }

        private bool GetEntryOrDefaultInternal(Guid? id, out IEntry<TValue> entry)
        {
            entry = default(IEntry<TValue>);
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
        public bool GetNextOrDefault(Guid? id, out IEntry<TValue> entry)
        {
            Locker.EnterReadLock();
            try
            {
                if (disposedValue) { entry = default(IEntry<TValue>); return false; }
                return GetNextOrDefaultInternal(id, out entry);
            }
            finally { Locker.ExitReadLock(); }
        }

        private bool GetNextOrDefaultInternal(Guid? id, out IEntry<TValue> entry)
        {
            entry = default(IEntry<TValue>);
            return metadata.GetNextId(id, out var nextId) && GetEntryOrDefaultInternal(nextId, out entry);

        }

        /// <inheritdoc/>
        public bool GetFirstOrDefault(out IEntry<TValue> entry)
        {
            Locker.EnterReadLock();
            try
            {
                if (disposedValue) { entry = default(IEntry<TValue>); return false; }
                entry = default(IEntry<TValue>);
                return GetEntryOrDefaultInternal(metadata.HeadId, out entry);
            }
            finally { Locker.ExitReadLock(); }
        }

        /// <inheritdoc/>
        public bool GetFirstIdOrDefault(out Guid? id)
        {
            Locker.EnterReadLock();
            try
            {
                if (disposedValue) { id = default(Guid?); return false; }
                id = metadata.HeadId;
                return true;
            }
            finally { Locker.ExitReadLock(); }
        }

        /// <inheritdoc/>
        public bool GetLastOrDefault(out IEntry<TValue> entry)
        {
            Locker.EnterReadLock();
            try
            {
                if (disposedValue) { entry = default(IEntry<TValue>); return false; }
                entry = default(IEntry<TValue>);
                return GetEntryOrDefaultInternal(metadata.TailId, out entry);
            }
            finally { Locker.ExitReadLock(); }
        }

        /// <inheritdoc/>
        public bool GetLastIdOrDefault(out Guid? id)
        {
            Locker.EnterReadLock();
            try
            {
                if (disposedValue) { id = default(Guid?); return false; }
                id = metadata.TailId;
                return true;
            }
            finally { Locker.ExitReadLock(); }
        }

        /// <inheritdoc/>
        public Task<IEntry<TValue>> GetNextAsync(Guid? id = null, CancellationToken cancellationToken = default)
        {
            Locker.EnterReadLock();
            try
            {
                if (disposedValue) { Task.FromCanceled<IEntry<TValue>>(cancellationToken); }
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

        /// <inheritdoc/>
        public Task<IEntry<TValue>> GetFutureFirstOrDefaultAsync(CancellationToken cancellationToken = default)
        {
            Locker.EnterReadLock();
            try
            {
                return GetFutureFirstOrDefaultAsyncInternal(cancellationToken);
            }
            finally { Locker.ExitReadLock(); }
        }

        private Task<IEntry<TValue>> GetFutureFirstOrDefaultAsyncInternal(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested) { Task.FromCanceled<IEntry<TValue>>(cancellationToken); }
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
        public bool Remove(Guid id, out IEntry<TValue> entry)
        {
            Locker.EnterWriteLock();
            try
            {
                return RemoveInternal(id, out entry);
            }
            finally { Locker.ExitWriteLock(); }
        }

        private bool RemoveInternal(Guid id, out IEntry<TValue> entry)
        {
            if (disposedValue) { entry = default(IEntry<TValue>); return false; }
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
        /// </summary>
        /// <returns><c>true</c> always; the method is best‑effort.</returns>
        private bool ReplenishL1Store()
        {
            while (l1Store?.CurrentCapacity > 0 &&
                   metadata.GetNextId(l1Store.TailId, out var nextId) &&
                   l2Store.GetEntryOrDefault(nextId, out var nextEntry) &&
                   nextEntry != null && l1Store.Add(nextEntry)) ;
            return true;
        }

        /// <inheritdoc/>
        public bool Clear()
        {
            Locker.EnterWriteLock();
            try
            {
                return ClearInternal();
                //return l2Store.Clear() && (l1Store == null ? true : l1Store.Clear()) && metadata.Clear();
            }
            finally { Locker.ExitWriteLock(); }
        }

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

        public IAsyncEnumerator<IEntry<TValue>> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            var retVal = new CacheAsyncEnumerator<TValue>(this, e => activeEnumerators.Remove(e), cancellationToken);
            activeEnumerators.Add(retVal);
            return retVal;
        }

        public IAsyncEnumerator<IEntry<TValue>> GetFutureAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            var retVal = new CacheFutureAsyncEnumerator<TValue>(this, e => activeEnumerators.Remove(e), cancellationToken);
            activeEnumerators.Add(retVal);
            return retVal;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}