using Baubit.Collections;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Xml.Linq;

namespace Baubit.Caching
{
    public static class Telemetry
    {
        public const string CacheNameTagKey = "cache.name";
        public const string OperationTagKey = "operation";
        public const string CacheTierTagKey = "cache.tier";

        public const string SourceName = "Baubit.Caching";
        public const string OperationsCounterKey = "baubit.cache.operations";
        public const string HitsCounterKey = "baubit.cache.hits";
        public const string MissessCounterKey = "baubit.cache.misses";
        public const string EvictionsCounterKey = "baubit.cache.evictions";
        public const string EntriesCounterKey = "baubit.cache.entries";
        public const string L1EntriesCounterKey = "baubit.cache.l1.entries";
        public const string EnumeratorsCounterKey = "baubit.cache.enumerators";
        public const string L1ReplenishmentsKey = "baubit.cache.l1.replenishments";
        public const string L1ResizesKey = "baubit.cache.l1.resizes";
        public const string OperationDurationKey = "baubit.cache.operation.duration";
        public const string OperationDurationUnit = "ms";
        public const string L1CapacityKey = "baubit.cache.l1.capacity";

        private static readonly string version = typeof(Telemetry).Assembly.GetName().Version?.ToString();
        private static readonly Meter meter = new Meter(SourceName, version);

        /// <summary>
        /// The single <see cref="ActivitySource"/> shared by every cache instance in the process.
        /// </summary>
        internal static readonly ActivitySource ActivitySource = new ActivitySource(SourceName, version);

        /// <summary>
        /// Counts logical cache operations (get/add/update/remove). Tags: cache.name, operation.
        /// </summary>
        internal static readonly Counter<long> Operations = meter.CreateCounter<long>(OperationsCounterKey);

        /// <summary>
        /// Counts cache lookups satisfied by a store tier. Tags: cache.name, cache.tier.
        /// </summary>
        internal static readonly Counter<long> Hits = meter.CreateCounter<long>(HitsCounterKey);

        /// <summary>
        /// Counts cache lookups that were not satisfied by any tier. Tags: cache.name.
        /// </summary>
        internal static readonly Counter<long> Misses = meter.CreateCounter<long>(MissessCounterKey);

        /// <summary>
        /// Counts entries evicted, incremented once per eviction batch by the number removed. Tags: cache.name.
        /// </summary>
        internal static readonly Counter<long> Evictions = meter.CreateCounter<long>(EvictionsCounterKey);

        /// <summary>
        /// Tracks the current number of entries persisted for a cache (L1 + L2 combined). Tags: cache.name.
        /// </summary>
        internal static readonly UpDownCounter<long> Entries = meter.CreateUpDownCounter<long>(EntriesCounterKey);

        /// <summary>
        /// Tracks the current number of entries resident in the L1 store. Tags: cache.name.
        /// </summary>
        internal static readonly UpDownCounter<long> L1Entries = meter.CreateUpDownCounter<long>(L1EntriesCounterKey);

        /// <summary>
        /// Tracks the current number of active enumerators/consumers for a cache. Tags: cache.name.
        /// </summary>
        internal static readonly UpDownCounter<long> Enumerators = meter.CreateUpDownCounter<long>(EnumeratorsCounterKey);

        /// <summary>
        /// Counts batches where entries were replenished into L1 from L2, by number of entries added. Tags: cache.name.
        /// </summary>
        internal static readonly Counter<long> L1Replenishments = meter.CreateCounter<long>(L1ReplenishmentsKey);

        /// <summary>
        /// Counts adaptive L1 capacity changes. Tags: cache.name, direction (grow/shrink).
        /// </summary>
        internal static readonly Counter<long> L1Resizes = meter.CreateCounter<long>(L1ResizesKey);

        /// <summary>
        /// Records the duration, in milliseconds, of cache operations for which latency is meaningful. Tags: cache.name, operation.
        /// </summary>
        internal static readonly Histogram<double> OperationDuration = meter.CreateHistogram<double>(OperationDurationKey, unit: OperationDurationUnit);

        /// <summary>
        /// Reports the current effective/configured L1 capacity for every registered cache that has an L1 store.
        /// Backed by a single shared observable instrument rather than one instrument per cache instance.
        /// </summary>
        internal static readonly ObservableGauge<long> L1Capacity = meter.CreateObservableGauge<long>(L1CapacityKey, ObserveL1Capacity);

        private static readonly L1CapacityRegistrations l1CapacityRegistrations = new L1CapacityRegistrations();

        private static IEnumerable<Measurement<long>> ObserveL1Capacity()
        {
            foreach (var (provider, tags) in l1CapacityRegistrations.GetLivingProviders())
            {
                var capacity = provider();
                if (capacity.HasValue)
                {
                    yield return new Measurement<long>(capacity.Value, tags);
                }
            }
        }

        internal static void RecordOperation(string operation, Configuration configuration)
        {
            var tags = NameTag(configuration);
            tags.Add(OperationTagKey, operation);
            Operations.Add(1, in tags);
        }

        internal static void RecordHit(string tier, Configuration configuration)
        {
            var tags = NameTag(configuration);
            tags.Add(CacheTierTagKey, tier);
            Hits.Add(1, in tags);
        }

        internal static void RecordMiss(Configuration configuration)
        {
            var tags = NameTag(configuration);
            Misses.Add(1, in tags);
        }

        internal static void RecordEviction(int count, Configuration configuration)
        {
            if (count <= 0) return;
            var tags = NameTag(configuration);
            Evictions.Add(count, in tags);
        }

        internal static void AdjustEntries(long delta, Configuration configuration)
        {
            var tags = NameTag(configuration);
            Entries.Add(delta, in tags);
        }

        internal static void AdjustL1Entries(long delta, Configuration configuration)
        {
            var tags = NameTag(configuration);
            L1Entries.Add(delta, in tags);
        }

        internal static void AdjustEnumerators(long delta, Configuration configuration)
        {
            var tags = NameTag(configuration);
            Enumerators.Add(delta, in tags);
        }

        internal static void RecordReplenishment(int count, Configuration configuration)
        {
            if (count <= 0) return;
            var tags = NameTag(configuration);
            L1Replenishments.Add(count, in tags);
        }

        internal static void RecordResize(string direction, Configuration configuration)
        {
            var tags = NameTag(configuration);
            tags.Add("direction", direction);
            L1Resizes.Add(1, in tags);
        }

        internal static void RecordDuration(string operation, double elapsedMilliseconds, Configuration configuration)
        {
            var tags = NameTag(configuration);
            tags.Add("operation", operation);
            OperationDuration.Record(elapsedMilliseconds, in tags);
        }

        internal static void RegisterL1CapacityProvider(Func<long?> capacityProvider, Configuration configuration)
        {
            var tags = NameTag(configuration);
            l1CapacityRegistrations.Add(new L1CapacityRegistration(new WeakReference<Func<long?>>(capacityProvider), tags));
        }

        internal static long? BeginDuration()
        {
            return OperationDuration.Enabled ? (long?)Stopwatch.GetTimestamp() : null;
        }

        internal static void EndDuration(string operation, long? start, Configuration configuration)
        {
            if (!start.HasValue) return;
            double elapsedMs = (Stopwatch.GetTimestamp() - start.Value) * 1000.0 / Stopwatch.Frequency;
            RecordDuration(operation, elapsedMs, configuration);
        }

        internal static Activity StartActivity(string operationName, Configuration configuration)
        {
            var activity = ActivitySource.StartActivity(operationName);
            if(configuration?.Name != null)
            {
                activity.SetTag(CacheNameTagKey, configuration.Name);
            }
            return activity;
        }

        private static TagList NameTag(Configuration configuration)
        {
            var tags = default(TagList);
            if (configuration?.Name != null)
            {
                tags.Add(CacheNameTagKey, configuration.Name);
            }
            return tags;
        }

        /// <summary>
        /// Associates a weakly-referenced capacity provider delegate with the tags to report alongside it.
        /// </summary>
        private readonly struct L1CapacityRegistration
        {
            public WeakReference<Func<long?>> Provider { get; }
            public TagList Tags { get; }

            public L1CapacityRegistration(WeakReference<Func<long?>> provider, TagList tags)
            {
                Provider = provider;
                Tags = tags;
            }
        }

        private class L1CapacityRegistrations : ConcurrentList<L1CapacityRegistration>
        {
            internal IEnumerable<(Func<long?>, TagList)> GetLivingProviders()
            {
                foreach (var registration in this)
                {
                    if (registration.Provider.TryGetTarget(out var provider))
                    {
                        yield return (provider, registration.Tags);
                    }
                    else
                    {
                        this.Remove(registration); // This is safe with ConcurrentList<>
                    }
                }
            }
        }
    }
}
