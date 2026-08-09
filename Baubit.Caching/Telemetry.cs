using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Baubit.Caching
{
    /// <summary>
    /// Centralized, library-wide telemetry primitives for Baubit.Caching.
    /// A single <see cref="Meter"/> and <see cref="ActivitySource"/> are shared across all
    /// <see cref="OrderedCache{TId, TValue}"/> instances; individual caches are distinguished
    /// using the low-cardinality <c>cache.name</c> tag (see <see cref="CacheTelemetryContext"/>).
    /// </summary>
    /// <remarks>
    /// This class intentionally depends only on <see cref="System.Diagnostics.Metrics"/> and
    /// <see cref="System.Diagnostics.ActivitySource"/>. It has effectively zero runtime cost when no
    /// listener (e.g. an OpenTelemetry <c>MeterProvider</c>/<c>TracerProvider</c>) is attached.
    /// </remarks>
    internal static class Telemetry
    {
        /// <summary>
        /// The shared name used for both the <see cref="Meter"/> and the <see cref="ActivitySource"/>.
        /// </summary>
        internal const string SourceName = "Baubit.Caching";

        private static readonly string version = typeof(Telemetry).Assembly.GetName().Version?.ToString();

        /// <summary>
        /// The single <see cref="Meter"/> shared by every cache instance in the process.
        /// </summary>
        internal static readonly Meter Meter = new Meter(SourceName, version);

        /// <summary>
        /// The single <see cref="ActivitySource"/> shared by every cache instance in the process.
        /// </summary>
        internal static readonly ActivitySource ActivitySource = new ActivitySource(SourceName, version);

        /// <summary>
        /// Counts logical cache operations (get/add/update/remove). Tags: cache.name, operation.
        /// </summary>
        internal static readonly Counter<long> Operations = Meter.CreateCounter<long>("baubit.cache.operations");

        /// <summary>
        /// Counts cache lookups satisfied by a store tier. Tags: cache.name, cache.tier.
        /// </summary>
        internal static readonly Counter<long> Hits = Meter.CreateCounter<long>("baubit.cache.hits");

        /// <summary>
        /// Counts cache lookups that were not satisfied by any tier. Tags: cache.name.
        /// </summary>
        internal static readonly Counter<long> Misses = Meter.CreateCounter<long>("baubit.cache.misses");

        /// <summary>
        /// Counts entries evicted, incremented once per eviction batch by the number removed. Tags: cache.name.
        /// </summary>
        internal static readonly Counter<long> Evictions = Meter.CreateCounter<long>("baubit.cache.evictions");

        /// <summary>
        /// Tracks the current number of entries persisted for a cache (L1 + L2 combined). Tags: cache.name.
        /// </summary>
        internal static readonly UpDownCounter<long> Entries = Meter.CreateUpDownCounter<long>("baubit.cache.entries");

        /// <summary>
        /// Tracks the current number of entries resident in the L1 store. Tags: cache.name.
        /// </summary>
        internal static readonly UpDownCounter<long> L1Entries = Meter.CreateUpDownCounter<long>("baubit.cache.l1.entries");

        /// <summary>
        /// Tracks the current number of active enumerators/consumers for a cache. Tags: cache.name.
        /// </summary>
        internal static readonly UpDownCounter<long> Enumerators = Meter.CreateUpDownCounter<long>("baubit.cache.enumerators");

        /// <summary>
        /// Counts batches where entries were replenished into L1 from L2, by number of entries added. Tags: cache.name.
        /// </summary>
        internal static readonly Counter<long> L1Replenishments = Meter.CreateCounter<long>("baubit.cache.l1.replenishments");

        /// <summary>
        /// Counts adaptive L1 capacity changes. Tags: cache.name, direction (grow/shrink).
        /// </summary>
        internal static readonly Counter<long> L1Resizes = Meter.CreateCounter<long>("baubit.cache.l1.resizes");

        /// <summary>
        /// Records the duration, in milliseconds, of cache operations for which latency is meaningful. Tags: cache.name, operation.
        /// </summary>
        internal static readonly Histogram<double> OperationDuration = Meter.CreateHistogram<double>("baubit.cache.operation.duration", unit: "ms");

        private static readonly object l1CapacityProvidersLock = new object();
        private static readonly List<L1CapacityRegistration> l1CapacityProviders = new List<L1CapacityRegistration>();

        /// <summary>
        /// Reports the current effective/configured L1 capacity for every registered cache that has an L1 store.
        /// Backed by a single shared observable instrument rather than one instrument per cache instance.
        /// </summary>
        internal static readonly ObservableGauge<long> L1Capacity = Meter.CreateObservableGauge<long>("baubit.cache.l1.capacity", ObserveL1Capacity);

        /// <summary>
        /// Registers a lightweight, weakly-referenced provider of the current L1 target capacity for a cache.
        /// The provider is polled only when a listener observes <see cref="L1Capacity"/>, and the registration
        /// does not keep the owning cache alive.
        /// </summary>
        /// <param name="tags">The tags (e.g. cache.name) to attach to reported measurements.</param>
        /// <param name="capacityProvider">A delegate returning the current L1 target capacity, or <c>null</c> if unbounded/unknown.</param>
        internal static void RegisterL1CapacityProvider(TagList tags, Func<long?> capacityProvider)
        {
            lock (l1CapacityProvidersLock)
            {
                l1CapacityProviders.Add(new L1CapacityRegistration(new WeakReference<Func<long?>>(capacityProvider), tags));
            }
        }

        private static IEnumerable<Measurement<long>> ObserveL1Capacity()
        {
            L1CapacityRegistration[] registrationsSnapshot;
            lock (l1CapacityProvidersLock)
            {
                l1CapacityProviders.RemoveAll(registration => !registration.Provider.TryGetTarget(out _));
                registrationsSnapshot = l1CapacityProviders.ToArray();
            }

            var results = new List<Measurement<long>>(registrationsSnapshot.Length);
            foreach (var registration in registrationsSnapshot)
            {
                if (!registration.Provider.TryGetTarget(out var provider)) continue;
                var capacity = provider();
                if (capacity.HasValue)
                {
                    results.Add(new Measurement<long>(capacity.Value, registration.Tags));
                }
            }
            return results;
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
    }
}
