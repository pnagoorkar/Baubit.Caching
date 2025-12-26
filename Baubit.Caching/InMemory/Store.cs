using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace Baubit.Caching.InMemory
{
    public class Store<TId, TValue> : Caching.Store<TId, TValue> where TId : struct, IComparable<TId>, IEquatable<TId>
    {
        private readonly Dictionary<TId, IEntry<TId, TValue>> data = new Dictionary<TId, IEntry<TId, TValue>>();
        private TId? lastGeneratedId;
        private Func<TId?, TId?> nextIdFactory;
        /// <summary>
        /// Initializes a new instance of the <see cref="Store{TId, TValue}"/> class with capacity bounds.
        /// </summary>
        /// <param name="minCap">Minimum capacity for the store.</param>
        /// <param name="maxCap">Maximum capacity for the store.</param>
        /// <param name="nextIdFactory">Factory function to generate the next ID based on the last generated ID.</param>
        /// <param name="loggerFactory">Factory for creating loggers.</param>
        public Store(long? minCap, long? maxCap, Func<TId?, TId?> nextIdFactory, ILoggerFactory loggerFactory) : base(minCap, maxCap, loggerFactory)
        {
            this.nextIdFactory = nextIdFactory;
        }

        /// <inheritdoc/>
        public override bool Add(IEntry<TId, TValue> entry)
        {
            if (!HasCapacity) return false;
            if (data.ContainsKey(entry.Id)) return false;
            data[entry.Id] = entry;
            return true;
        }

        /// <inheritdoc/>
        public override bool Add(TId id, TValue value, out IEntry<TId, TValue> entry)
        {
            entry = new Entry<TId, TValue>(id, value);
            return Add(entry);
        }

        /// <inheritdoc/>
        public override bool Add(TValue value, out IEntry<TId, TValue> entry)
        {
            var nextId = nextIdFactory(lastGeneratedId);
            if (nextId == null)
            {
                entry = default;
                return false;
            }
            lastGeneratedId = nextId;
            return Add(nextId.Value, value, out entry);
        }

        /// <inheritdoc/>
        public override bool GetCount(out long count)
        {
            count = data.Count;
            return true;
        }

        /// <inheritdoc/>
        public override bool GetEntryOrDefault(TId? id, out IEntry<TId, TValue> entry)
        {
            entry = null;
            return id.HasValue && data.TryGetValue(id.Value, out entry);
        }

        /// <inheritdoc/>
        public override bool GetValueOrDefault(TId? id, out TValue value)
        {
            value = default(TValue);
            if (!GetEntryOrDefault(id, out var entry)) return false;
            value = entry.Value;
            return true;
        }

        /// <inheritdoc/>
        public override bool Remove(TId id, out IEntry<TId, TValue> entry)
        {
            if (data.TryGetValue(id, out entry))
            {
                data.Remove(id);
                return true;
            }
            entry = default(IEntry<TId, TValue>);
            return false;
        }

        /// <inheritdoc/>
        public override bool Update(IEntry<TId, TValue> entry)
        {
            if (!data.ContainsKey(entry.Id)) return false;
            data[entry.Id] = entry;
            return true;
        }

        /// <inheritdoc/>
        public override bool Update(TId id, TValue value)
        {
            // Optimize: avoid creating new Entry if we can update in-place
            if (data.TryGetValue(id, out var existingEntry))
            {
                // Entry<TId, TValue>.Value is an auto-property with setter, allowing in-place modification
                if (existingEntry is Entry<TId, TValue> typedEntry)
                {
                    typedEntry.Value = value;
                    return true;
                }
            }
            // Fallback to creating a new entry
            return Update(new Entry<TId, TValue>(id, value));
        }

        /// <inheritdoc/>
        protected override void DisposeInternal()
        {
            data.Clear();
        }
    }
}