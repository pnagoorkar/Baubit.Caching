using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Baubit.Caching.InMemory
{
    public class Store<TValue> : AStore<TValue>
    {
        public override Guid? HeadId { get => _data.Count > 0 ? (Guid?)_data.Keys.Min() : null; }
        public override Guid? TailId { get => _data.Count > 0 ? (Guid?)_data.Keys.Max() : null; }

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
            return true;
        }

        public override bool Add(Guid id, TValue value, out IEntry<TValue> entry)
        {
            entry = new Entry<TValue>(id, value);
            return Add(entry);
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
            return Update(new Entry<TValue>(id, value));
        }

        protected override void DisposeInternal()
        {
            _data.Clear();
        }
    }
}