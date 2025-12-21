using Baubit.Identity;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Baubit.Caching.InMemory
{
    /// <summary>
    /// In-memory implementation of <see cref="IMetadata"/> for tracking cache entry ordering and identifiers.
    /// Thread-safe for concurrent access.
    /// </summary>
    public class Metadata : IMetadata
    {
        /// <summary>
        /// Gets the linked list representing the current order of entry identifiers.
        /// </summary>
        protected LinkedList<Guid> CurrentOrder { get; private set; } = new LinkedList<Guid>();
        /// <summary>
        /// Gets the mapping from entry identifiers to their linked list nodes.
        /// </summary>
        protected Dictionary<Guid, LinkedListNode<Guid>> IdNodeMap { get; private set; } = new Dictionary<Guid, LinkedListNode<Guid>>();

        /// <inheritdoc/>
        public long Count { get => IdNodeMap.Count; }

        /// <inheritdoc/>
        public Guid? HeadId { get => CurrentOrder?.First?.Value; }
        /// <inheritdoc/>
        public Guid? TailId { get => CurrentOrder?.Last?.Value; }

        /// <summary>
        /// Gets the runtime configuration for this cache instance.
        /// </summary>
        protected Configuration Configuration { get; private set; }

        private long _roomCount;

        // Coordinates awaiters for the next id produced.
        private WaitingRoom<Guid> _waitingRoom = new WaitingRoom<Guid>();

        private IIdentityGenerator identityGenerator;
        private bool disposedValue;
        private ILogger<Metadata> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="Metadata"/> class.
        /// </summary>
        /// <param name="configuration">The cache configuration.</param>
        /// <param name="identityGenerator">The identity generator for producing new entry IDs.</param>
        /// <param name="loggerFactory">The logger factory for diagnostics.</param>
        public Metadata(Configuration configuration, 
                        IIdentityGenerator identityGenerator, 
                        ILoggerFactory loggerFactory)
        {
            logger = loggerFactory.CreateLogger<Metadata>();
            this.Configuration = configuration;
            this.identityGenerator = identityGenerator;
        }

        /// <inheritdoc/>
        public long ResetRoomCount()
        {
            return Interlocked.Exchange(ref _roomCount, 0);
        }

        /// <inheritdoc/>
        public bool AddTail(Guid id)
        {
            IdNodeMap.Add(id, CurrentOrder.AddLast(id));
            return SignalAwaiters(id);
        }

        /// <summary>
        /// Signals any awaiters waiting for the next ID.
        /// </summary>
        /// <param name="id">The new ID to signal.</param>
        /// <returns><c>true</c> if signaled; otherwise <c>false</c>.</returns>
        private bool SignalAwaiters(Guid id)
        {
            if (!_waitingRoom.HasGuests) return true;
            if (Configuration?.RunAdaptiveResizing == true) Interlocked.Increment(ref _roomCount);
            var prevRoom = _waitingRoom;
            _waitingRoom = new WaitingRoom<Guid>();
            return prevRoom.TrySetResult(id);
        }

        /// <inheritdoc/>
        public bool ContainsKey(Guid id) => IdNodeMap.ContainsKey(id);

        /// <inheritdoc/>
        public bool GetNextId(Guid? id, out Guid? nextId)
        {
            if (id == null) nextId = HeadId;
            else if (HeadId == null) nextId = null; // if id is not null but HeadId is null means id is the tail that was deleted just before the call arrived here. Return null so the caller can get the next arriving item
            else if (IsIdSmallerThanHeadId(id)) nextId = HeadId;
            else if (IsIdTailId(id)) nextId = null;
            else if (id.HasValue && IdNodeMap.TryGetValue(id.Value, out var node)) nextId = node.Next?.Value;
            // If an id is neither null, nor less than head nor tail nor an in-between id and the id is not found in IdNodeMap means the value was deleted out of order. Return the next big id after it.
            else nextId = FindNextGreaterId(id.Value); // Optimized: avoid LINQ OrderBy
            return true;
        }

        /// <summary>
        /// Finds the smallest ID in the map that is greater than the given ID.
        /// This is an O(n) linear scan but avoids LINQ sorting allocations.
        /// </summary>
        /// <param name="id">The ID to compare against.</param>
        /// <returns>The next greater ID if found; otherwise <c>null</c>.</returns>
        private Guid? FindNextGreaterId(Guid id)
        {
            Guid? result = null;
            foreach (var key in IdNodeMap.Keys)
            {
                if (key.CompareTo(id) > 0)
                {
                    if (!result.HasValue || key.CompareTo(result.Value) < 0)
                    {
                        result = key;
                    }
                }
            }
            return result;
        }

        /// <inheritdoc/>
        public Task<Guid> GetNextIdAsync(Guid? id, CancellationToken cancellationToken)
        {
            if (!GetNextId(id, out var nextId))
            {
                // unexpected. Handle appropriately
            }
            if (nextId != null)
            {
                return Task.FromResult(nextId.Value);
            }
            return _waitingRoom.Join(cancellationToken);
        }

        /// <inheritdoc/>
        public bool GenerateNextId(out Guid nextId)
        {
            if (TailId.HasValue)
            {
                identityGenerator.InitializeFrom(TailId.Value);
            }
            nextId = identityGenerator.GetNext();
            return true;
        }

        /// <inheritdoc/>
        public bool GetIdsThrough(Guid id, out IEnumerable<Guid> ids)
        {
            // (Empty store || if id preceeds the head) = do nothing
            if (CurrentOrder.Count == 0 || (HeadId.HasValue && id.CompareTo(HeadId.Value) < 0))
            {
                ids = Array.Empty<Guid>();
                return false;
            }

            // If id is at/after the tail -> whole list
            if (TailId.HasValue && id.CompareTo(TailId.Value) >= 0)
            {
                ids = EnumerateToList(CurrentOrder.First, CurrentOrder.Last);
                return true;
            }

            if (!IdNodeMap.TryGetValue(id, out var end))
            {
                // this method is intended to be called from the ordered cache and it is assumed that the cache will ALWAYS send an id that IS present in the IdNodeMap.
                // if this block ever gets executed, the above assumption must not longer be true.
                ids = Array.Empty<Guid>();
                return false;
            }

            ids = EnumerateToList(CurrentOrder.First, end);
            return true;
        }

        /// <summary>
        /// Enumerates nodes from start to endInclusive and returns a list to avoid iterator allocations.
        /// </summary>
        /// <param name="start">The starting node.</param>
        /// <param name="endInclusive">The ending node (inclusive).</param>
        /// <returns>A list of GUIDs from start to endInclusive.</returns>
        private static List<Guid> EnumerateToList(LinkedListNode<Guid> start, LinkedListNode<Guid> endInclusive)
        {
            var result = new List<Guid>();
            for (var n = start; n != null; n = n.Next)
            {
                result.Add(n.Value);
                if (ReferenceEquals(n, endInclusive)) break;
            }
            return result;
        }

        /// <summary>
        /// Determines if the given ID is smaller than the head ID.
        /// </summary>
        /// <param name="id">The ID to check.</param>
        /// <returns><c>true</c> if smaller; otherwise <c>false</c>.</returns>
        private bool IsIdSmallerThanHeadId(Guid? id) => id.HasValue && HeadId.HasValue && id.Value.CompareTo(HeadId.Value) < 0;

        /// <summary>
        /// Determines if the given ID is the tail ID.
        /// </summary>
        /// <param name="id">The ID to check.</param>
        /// <returns><c>true</c> if it is the tail; otherwise <c>false</c>.</returns>
        private bool IsIdTailId(Guid? id) => id.HasValue && TailId.HasValue && id == TailId;

        /// <inheritdoc/>
        public bool Remove(Guid id)
        {
            if (IdNodeMap.TryGetValue(id, out var node))
            {
                CurrentOrder.Remove(node);
                IdNodeMap.Remove(id);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Releases the resources used by the <see cref="Metadata"/> class.
        /// </summary>
        /// <param name="disposing">Whether called from Dispose().</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    IdNodeMap.Clear();
                    IdNodeMap = null;
                    CurrentOrder.Clear();
                    CurrentOrder = null;
                    Configuration = null;
                }
                disposedValue = true;
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}