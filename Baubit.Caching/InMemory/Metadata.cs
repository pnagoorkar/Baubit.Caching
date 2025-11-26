using Baubit.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Baubit.Caching.InMemory
{
    public class Metadata : IMetadata
    {
        public LinkedList<Guid> CurrentOrder { get; set; } = new LinkedList<Guid>();
        public Dictionary<Guid, LinkedListNode<Guid>> IdNodeMap { get; set; } = new Dictionary<Guid, LinkedListNode<Guid>>();

        public long Count { get => IdNodeMap.Count; }

        public Guid? HeadId { get => CurrentOrder?.First?.Value; }
        public Guid? TailId { get => CurrentOrder?.Last?.Value; }

        /// <summary>
        /// Gets the runtime configuration for this cache instance.
        /// </summary>
        public Configuration Configuration { get; set; }

        private long _roomCount;

        // Coordinates awaiters for the next id produced.
        private WaitingRoom<Guid> _waitingRoom = new WaitingRoom<Guid>();

        private GuidV7Generator idGenerator = GuidV7Generator.CreateNew();
        private bool disposedValue;

        public long ResetRoomCount()
        {
            return Interlocked.Exchange(ref _roomCount, 0);
        }

        public bool AddTail(Guid id)
        {
            IdNodeMap.Add(id, CurrentOrder.AddLast(id));
            return SignalAwaiters(id);
        }

        private bool SignalAwaiters(Guid id)
        {
            if (!_waitingRoom.HasGuests) return true;
            if (Configuration?.RunAdaptiveResizing == true) Interlocked.Increment(ref _roomCount);
            var prevRoom = _waitingRoom;
            _waitingRoom = new WaitingRoom<Guid>();
            return prevRoom.TrySetResult(id);
        }

        public bool ContainsKey(Guid id) => IdNodeMap.ContainsKey(id);

        public bool GetNextId(Guid? id, out Guid? nextId)
        {
            if (id == null) nextId = HeadId;
            else if (HeadId == null) nextId = null; // if id is not null but HeadId is null means id is the tail that was deleted just before the call arrived here. Return null so the caller can get the next arriving item
            else if (IsIdSmallerThanHeadId(id)) nextId = HeadId;
            else if (IsIdTailId(id)) nextId = null;
            else if (id.HasValue && IdNodeMap.TryGetValue(id.Value, out var node)) nextId = node.Next.Value;
            else nextId = IdNodeMap.Keys.OrderBy(key => key).FirstOrDefault(key => key.CompareTo(id.Value) > 0); // If an id is neither null, nor less than head nor tail nor an in-between id and the id is not found in IdNodeMap means the value was deleted out of order. Return the next big id after it.
            return true;
        }

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

        public bool GenerateNextId(out Guid nextId)
        {
            if (TailId.HasValue)
            {
                idGenerator.InitializeFrom(TailId.Value);
            }
            nextId = idGenerator.GetNext();
            return true;
        }

        public bool GetIdsThrough(Guid id, out IEnumerable<Guid> ids)
        {
            // (Empty store || if id preceeds the head) = do nothing
            if (CurrentOrder.Count == 0 || (HeadId.HasValue && id.CompareTo(HeadId.Value) < 0))
            {
                ids = new Guid[0];
                return false;
            }

            // If id is at/after the tail -> whole list
            if (TailId.HasValue && id.CompareTo(TailId.Value) >= 0)
            {
                ids = Enumerate(CurrentOrder.First, CurrentOrder.Last).ToArray();
                return true;
            }

            if (!IdNodeMap.TryGetValue(id, out var end))
            {
                // this method is intended to be called from the ordered cache and it is assumed that the cache will ALWAYS send an id that IS present in the IdNodeMap.
                // if this block ever gets executed, the above assumption must not longer be true.
                ids = new Guid[0];
                return false;
            }

            ids = Enumerate(CurrentOrder.First, end).ToArray();
            return true;

            IEnumerable<Guid> Enumerate(LinkedListNode<Guid> start, LinkedListNode<Guid> endInclusive)
            {
                for (var n = start; n != null; n = n.Next)
                {
                    yield return n.Value;
                    if (ReferenceEquals(n, endInclusive)) yield break;
                }
            }
        }

        private bool IsIdSmallerThanHeadId(Guid? id) => id.HasValue && HeadId.HasValue && id.Value.CompareTo(HeadId.Value) < 0;

        private bool IsIdTailId(Guid? id) => id.HasValue && TailId.HasValue && id == TailId;

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

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {

                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}