using Baubit.Identity;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace Baubit.Caching.InMemory
{
    public class Store<TValue> : Caching.Store<TValue>
    {
        private readonly Dictionary<Guid, IEntry<TValue>> data = new Dictionary<Guid, IEntry<TValue>>();
        private readonly IIdentityGenerator identityGenerator;
        private Guid? lastGeneratedId;

        private ILogger<Store<TValue>> logger;

        public Store(long? minCap,
                     long? maxCap,
                     IIdentityGenerator identityGenerator,
                     ILoggerFactory loggerFactory) : base(minCap, maxCap, loggerFactory)
        {
            this.identityGenerator = identityGenerator;
            logger = loggerFactory.CreateLogger<Store<TValue>>();
        }

        public Store(IIdentityGenerator identityGenerator, ILoggerFactory loggerFactory) : this(null, null, identityGenerator, loggerFactory)
        {

        }

        public override bool Add(IEntry<TValue> entry)
        {
            if (!HasCapacity) return false;
            if (data.ContainsKey(entry.Id)) return false;
            data[entry.Id] = entry;
            return true;
        }

        public override bool Add(Guid id, TValue value, out IEntry<TValue> entry)
        {
            entry = new Entry<TValue>(id, value);
            return Add(entry);
        }

        public override bool Add(TValue value, out IEntry<TValue> entry)
        {
            if (identityGenerator == null)
            {
                entry = default(IEntry<TValue>);
                return false;
            }

            // Initialize from last generated ID if available to ensure monotonicity
            if (lastGeneratedId.HasValue)
            {
                identityGenerator.InitializeFrom(lastGeneratedId.Value);
            }

            var nextId = identityGenerator.GetNext();
            lastGeneratedId = nextId;
            return Add(nextId, value, out entry);
        }

        public override bool GetCount(out long count)
        {
            count = data.Count;
            return true;
        }

        public override bool GetEntryOrDefault(Guid? id, out IEntry<TValue> entry)
        {
            entry = null;
            return id.HasValue && data.TryGetValue(id.Value, out entry);
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
            if (data.TryGetValue(id, out entry))
            {
                data.Remove(id);
                return true;
            }
            entry = default(IEntry<TValue>);
            return false;
        }

        public override bool Update(IEntry<TValue> entry)
        {
            if (!data.ContainsKey(entry.Id)) return false;
            data[entry.Id] = entry;
            return true;
        }

        public override bool Update(Guid id, TValue value)
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
            return Update(new Entry<TValue>(id, value));
        }

        protected override void DisposeInternal()
        {
            data.Clear();
        }
    }
}