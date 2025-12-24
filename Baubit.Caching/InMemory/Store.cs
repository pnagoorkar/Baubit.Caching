using Baubit.Identity;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace Baubit.Caching.InMemory
{
    public abstract class Store<TId, TValue> : Caching.Store<TId, TValue> where TId : struct, IComparable<TId>, IEquatable<TId>
    {
        private readonly Dictionary<TId, IEntry<TId, TValue>> data = new Dictionary<TId, IEntry<TId, TValue>>();
        private readonly IIdentityGenerator identityGenerator;
        private TId? lastGeneratedId;
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
                // Entry<TValue>.Value is an auto-property with setter, allowing in-place modification
                if (existingEntry is Entry<TValue> typedEntry)
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
    /// <summary>
    /// In-memory implementation of <see cref="IStore{TValue}"/> using a dictionary for storage.
    /// Thread-safe for concurrent readers/writers when used with external synchronization.
    /// </summary>
    /// <typeparam name="TValue">The type of values stored in this store.</typeparam>
    public class Store<TValue> : Store<Guid, TValue>, IStore<TValue>
    {
        private readonly IIdentityGenerator identityGenerator;

        private ILogger<Store<TValue>> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="Store{TValue}"/> class with capacity bounds.
        /// </summary>
        /// <param name="minCap">Minimum capacity for the store.</param>
        /// <param name="maxCap">Maximum capacity for the store.</param>
        /// <param name="identityGenerator">Optional identity generator for auto-generating entry IDs.</param>
        /// <param name="loggerFactory">Factory for creating loggers.</param>
        public Store(long? minCap,
                     long? maxCap,
                     IIdentityGenerator identityGenerator,
                     ILoggerFactory loggerFactory) : base(minCap, maxCap, loggerFactory)
        {
            this.identityGenerator = identityGenerator;
            logger = loggerFactory.CreateLogger<Store<TValue>>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Store{TValue}"/> class without capacity bounds (uncapped).
        /// </summary>
        /// <param name="identityGenerator">Optional identity generator for auto-generating entry IDs.</param>
        /// <param name="loggerFactory">Factory for creating loggers.</param>
        public Store(IIdentityGenerator identityGenerator, ILoggerFactory loggerFactory) : this(null, null, identityGenerator, loggerFactory)
        {

        }

        protected override Guid? GenerateNextId(Guid? lastGeneratedId)
        {
            if (identityGenerator == null) return null;
            // Initialize from last generated ID if available to ensure monotonicity
            if (lastGeneratedId.HasValue)
            {
                identityGenerator.InitializeFrom(lastGeneratedId.Value);
            }

            return identityGenerator.GetNext();
        }
    }
}