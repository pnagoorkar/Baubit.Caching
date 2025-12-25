using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace Baubit.Caching.InMemory
{
    /// <summary>
    /// Abstract in-memory store implementation with generic identifier support.
    /// Uses a dictionary for O(1) lookups. Subclasses must provide ID generation logic.
    /// Thread-safe when used with external synchronization.
    /// </summary>
    /// <typeparam name="TId">The type of the entry identifier. Must be a struct implementing IComparable&lt;TId&gt; and IEquatable&lt;TId&gt;.</typeparam>
    /// <typeparam name="TValue">The type of values stored in this store.</typeparam>
    public abstract class Store<TId, TValue> : Caching.Store<TId, TValue> where TId : struct, IComparable<TId>, IEquatable<TId>
    {
        private readonly Dictionary<TId, IEntry<TId, TValue>> data = new Dictionary<TId, IEntry<TId, TValue>>();
        private TId? lastGeneratedId;
        /// <summary>
        /// Initializes a new instance of the <see cref="Store{TId, TValue}"/> class with capacity bounds.
        /// </summary>
        /// <param name="minCap">Minimum capacity for the store.</param>
        /// <param name="maxCap">Maximum capacity for the store.</param>
        /// <param name="loggerFactory">Factory for creating loggers.</param>
        protected Store(long? minCap, long? maxCap, ILoggerFactory loggerFactory) : base(minCap, maxCap, loggerFactory)
        {
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
            var nextId = GenerateNextId(lastGeneratedId);
            if (nextId == null)
            {
                entry = default;
                return false;
            }
            lastGeneratedId = nextId;
            return Add(nextId.Value, value, out entry);
        }

        /// <summary>
        /// Generates the next identifier for a new entry.
        /// Subclasses must implement this to provide ID generation logic.
        /// </summary>
        /// <param name="lastGeneratedId">The last generated ID, or null if no IDs have been generated yet.</param>
        /// <returns>The next ID to use, or null if ID generation fails.</returns>
        protected abstract TId? GenerateNextId(TId? lastGeneratedId);

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