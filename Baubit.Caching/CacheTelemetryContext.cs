using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Baubit.Caching
{
    /// <summary>
    /// Holds the low-cardinality telemetry identity for a single <see cref="OrderedCache{TId, TValue}"/>
    /// instance and provides low-allocation helpers for recording the library's shared metrics.
    /// </summary>
    /// <remarks>
    /// This is not a general-purpose telemetry abstraction; it exists solely to avoid recomputing
    /// or re-allocating tag collections on every cache operation. Instruments themselves live on
    /// <see cref="Telemetry"/> and are shared by every cache instance in the process.
    /// </remarks>
    internal readonly struct CacheTelemetryContext
    {
        /// <summary>
        /// The optional, application-assigned logical name of the cache (e.g. "users").
        /// When <c>null</c>, the <c>cache.name</c> tag is omitted from all measurements.
        /// </summary>
        public string Name { get; }

        public CacheTelemetryContext(string name)
        {
            Name = name;
        }

        private bool HasName => !string.IsNullOrEmpty(Name);

        private TagList NameTag()
        {
            var tags = default(TagList);
            if (HasName) tags.Add("cache.name", Name);
            return tags;
        }

        /// <summary>
        /// Records a logical operation (get/add/update/remove) against <see cref="Telemetry.Operations"/>.
        /// </summary>
        public void RecordOperation(string operation)
        {
            var tags = NameTag();
            tags.Add("operation", operation);
            Telemetry.Operations.Add(1, in tags);
        }

        /// <summary>
        /// Records a hit against <see cref="Telemetry.Hits"/> for the given tier ("l1" or "l2").
        /// </summary>
        public void RecordHit(string tier)
        {
            var tags = NameTag();
            tags.Add("cache.tier", tier);
            Telemetry.Hits.Add(1, in tags);
        }

        /// <summary>
        /// Records a miss against <see cref="Telemetry.Misses"/>.
        /// </summary>
        public void RecordMiss()
        {
            var tags = NameTag();
            Telemetry.Misses.Add(1, in tags);
        }

        /// <summary>
        /// Records a batch eviction of <paramref name="count"/> entries. No-op when <paramref name="count"/> is not positive.
        /// </summary>
        public void RecordEviction(int count)
        {
            if (count <= 0) return;
            var tags = NameTag();
            Telemetry.Evictions.Add(count, in tags);
        }

        /// <summary>
        /// Adjusts the total (L1+L2) entry count by <paramref name="delta"/> (+1/-1).
        /// </summary>
        public void AdjustEntries(long delta)
        {
            var tags = NameTag();
            Telemetry.Entries.Add(delta, in tags);
        }

        /// <summary>
        /// Adjusts the L1 resident entry count by <paramref name="delta"/> (+1/-1).
        /// </summary>
        public void AdjustL1Entries(long delta)
        {
            var tags = NameTag();
            Telemetry.L1Entries.Add(delta, in tags);
        }

        /// <summary>
        /// Adjusts the active enumerator count by <paramref name="delta"/> (+1/-1).
        /// </summary>
        public void AdjustEnumerators(long delta)
        {
            var tags = NameTag();
            Telemetry.Enumerators.Add(delta, in tags);
        }

        /// <summary>
        /// Records a batch replenishment of <paramref name="count"/> entries promoted from L2 into L1.
        /// No-op when <paramref name="count"/> is not positive.
        /// </summary>
        public void RecordReplenishment(int count)
        {
            if (count <= 0) return;
            var tags = NameTag();
            Telemetry.L1Replenishments.Add(count, in tags);
        }

        /// <summary>
        /// Records an adaptive L1 capacity resize in the given <paramref name="direction"/> ("grow" or "shrink").
        /// </summary>
        public void RecordResize(string direction)
        {
            var tags = NameTag();
            tags.Add("direction", direction);
            Telemetry.L1Resizes.Add(1, in tags);
        }

        /// <summary>
        /// Records the duration, in milliseconds, of a completed <paramref name="operation"/>.
        /// </summary>
        public void RecordDuration(string operation, double elapsedMilliseconds)
        {
            var tags = NameTag();
            tags.Add("operation", operation);
            Telemetry.OperationDuration.Record(elapsedMilliseconds, in tags);
        }

        /// <summary>
        /// Registers a weakly-referenced provider of the current L1 target capacity so it can be reported
        /// via the shared <see cref="Telemetry.L1Capacity"/> observable instrument.
        /// </summary>
        public void RegisterL1CapacityProvider(Func<long?> capacityProvider)
        {
            Telemetry.RegisterL1CapacityProvider(NameTag(), capacityProvider);
        }

        /// <summary>
        /// Captures a zero-allocation start timestamp for <see cref="EndDuration"/>, or <c>null</c>
        /// when no listener is attached to <see cref="Telemetry.OperationDuration"/> (avoiding the cost
        /// of reading the clock entirely).
        /// </summary>
        public long? BeginDuration()
        {
            return Telemetry.OperationDuration.Enabled ? (long?)Stopwatch.GetTimestamp() : null;
        }

        /// <summary>
        /// Records the elapsed duration since <paramref name="start"/> was captured by <see cref="BeginDuration"/>.
        /// No-op if <paramref name="start"/> is <c>null</c>.
        /// </summary>
        public void EndDuration(string operation, long? start)
        {
            if (!start.HasValue) return;
            double elapsedMs = (Stopwatch.GetTimestamp() - start.Value) * 1000.0 / Stopwatch.Frequency;
            RecordDuration(operation, elapsedMs);
        }

        /// <summary>
        /// Starts an <see cref="Activity"/> for the named operation if a listener is attached; otherwise returns <c>null</c>.
        /// </summary>
        public Activity StartActivity(string operationName)
        {
            var activity = Telemetry.ActivitySource.StartActivity(operationName);
            if (activity != null && HasName)
            {
                activity.SetTag("cache.name", Name);
            }
            return activity;
        }
    }
}
