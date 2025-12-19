using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace Baubit.Caching.InMemory
{
    public class Store<TValue> : Caching.Store<TValue>
    {
        // Cache the head and tail IDs to avoid O(n) Min/Max operations
        private Guid? _headId;
        private Guid? _tailId;

        public override Guid? HeadId { get => _headId; }
        public override Guid? TailId { get => _tailId; }

        private readonly Dictionary<Guid, IEntry<TValue>> _data = new Dictionary<Guid, IEntry<TValue>>();

        private ILogger<Store<TValue>> _logger;

        public Store(long? minCap,
                     long? maxCap,
                     ILoggerFactory loggerFactory) : base(minCap, maxCap, loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<Store<TValue>>();
        }

        public Store(ILoggerFactory loggerFactory) : this(null, null, loggerFactory)
        {

        }

        public override bool Add(IEntry<TValue> entry)
        {
            if (!HasCapacity) return false;
            if (_data.ContainsKey(entry.Id)) return false;
            _data[entry.Id] = entry;
            UpdateHeadTailOnAdd(entry.Id);
            return true;
        }

        public override bool Add(Guid id, TValue value, out IEntry<TValue> entry)
        {
            entry = new Entry<TValue>(id, value);
            return Add(entry);
        }

        private void UpdateHeadTailOnAdd(Guid id)
        {
            // GuidV7 IDs are time-ordered, so new entries are always the tail
            // Head is the smallest, tail is the largest
            if (!_headId.HasValue || id.CompareTo(_headId.Value) < 0)
            {
                _headId = id;
            }
            if (!_tailId.HasValue || id.CompareTo(_tailId.Value) > 0)
            {
                _tailId = id;
            }
        }

        private void UpdateHeadTailOnRemove(Guid id)
        {
            if (_data.Count == 0)
            {
                _headId = null;
                _tailId = null;
                return;
            }

            // Only recalculate if we removed the head or tail
            if (_headId.HasValue && id.CompareTo(_headId.Value) == 0)
            {
                _headId = FindMin();
            }
            if (_tailId.HasValue && id.CompareTo(_tailId.Value) == 0)
            {
                _tailId = FindMax();
            }
        }

        private Guid? FindMin()
        {
            Guid? min = null;
            foreach (var key in _data.Keys)
            {
                if (!min.HasValue || key.CompareTo(min.Value) < 0)
                {
                    min = key;
                }
            }
            return min;
        }

        private Guid? FindMax()
        {
            Guid? max = null;
            foreach (var key in _data.Keys)
            {
                if (!max.HasValue || key.CompareTo(max.Value) > 0)
                {
                    max = key;
                }
            }
            return max;
        }

        public override bool GetCount(out long count)
        {
            count = _data.Count;
            return true;
        }

        public override bool GetEntryOrDefault(Guid? id, out IEntry<TValue> entry)
        {
            entry = null;
            return id.HasValue && _data.TryGetValue(id.Value, out entry);
        }

        public override bool GetValueOrDefault(Guid? id, out TValue value)
        {
            value = default(TValue);
            if (!GetEntryOrDefault(id, out var entry)) return false;
            value = entry.Value;
            return true;
        }

        public override bool Remove(Guid id, out IEntry<TValue> entry)
        {
            if (_data.TryGetValue(id, out entry))
            {
                _data.Remove(id);
                UpdateHeadTailOnRemove(id);
                return true;
            }
            entry = default(IEntry<TValue>);
            return false;
        }

        public override bool Update(IEntry<TValue> entry)
        {
            if (!_data.ContainsKey(entry.Id)) return false;
            _data[entry.Id] = entry;
            return true;
        }

        public override bool Update(Guid id, TValue value)
        {
            // Optimize: avoid creating new Entry if we can update in-place
            if (_data.TryGetValue(id, out var existingEntry))
            {
                // Entry<TValue>.Value is an auto-property with setter, allowing in-place modification
                if (existingEntry is Entry<TValue> typedEntry)
                {
                    typedEntry.Value = value;
                    return true;
                }
            }
            // Fallback to creating a new entry
            return Update(new Entry<TValue>(id, value));
        }

        protected override void DisposeInternal()
        {
            _data.Clear();
            _headId = null;
            _tailId = null;
        }
    }
}