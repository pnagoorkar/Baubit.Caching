using Microsoft.Extensions.Logging;
using System;

namespace Baubit.Caching
{
    /// <summary>
    /// Abstract base class for store implementations with generic identifier support.
    /// Provides capacity management, growth/shrink logic, and common disposal patterns.
    /// Subclasses must implement storage operations (Add, Get, Remove, Update).
    /// </summary>
    /// <typeparam name="TId">The type of the entry identifier. Must be a struct implementing IComparable&lt;TId&gt; and IEquatable&lt;TId&gt;.</typeparam>
    /// <typeparam name="TValue">The type of values stored in this store.</typeparam>
    public abstract class Store<TId, TValue> : IStore<TId, TValue> where TId : struct, IComparable<TId>, IEquatable<TId>
    {
        /// <inheritdoc/>
        public bool Uncapped { get => !TargetCapacity.HasValue; }
        /// <inheritdoc/>
        public long? MinCapacity { get; set; } = null;
        /// <inheritdoc/>
        public long? MaxCapacity { get; set; } = null;
        /// <inheritdoc/>
        public long? TargetCapacity { get; private set; } = null;
        /// <inheritdoc/>
        public long? CurrentCapacity
        {
            get
            {
                if (Uncapped) return null;
                var count = GetCount();
                if (!count.HasValue) return null;
                return Math.Max(0, TargetCapacity.Value - count.Value);
            }
        }
        /// <inheritdoc/>
        public bool HasCapacity { get => Uncapped || CurrentCapacity > 0; }

        private ILogger<Store<TId, TValue>> logger;
        private bool disposedValue;

        /// <summary>
        /// Initializes a new instance of the <see cref="Store{TId, TValue}"/> class with specified capacity bounds.
        /// </summary>
        /// <param name="minCap">Minimum capacity the store may shrink to, or <c>null</c> for uncapped.</param>
        /// <param name="maxCap">Maximum capacity the store may grow to, or <c>null</c> for uncapped.</param>
        /// <param name="loggerFactory">Factory for creating loggers for diagnostics and tracing.</param>
        public Store(long? minCap,
                     long? maxCap,
                     ILoggerFactory loggerFactory)
        {
            TargetCapacity = MinCapacity = minCap;
            MaxCapacity = maxCap;
            logger = loggerFactory.CreateLogger<Store<TId, TValue>>();
        }

        /// <inheritdoc/>
        public abstract bool Add(IEntry<TId, TValue> entry);

        /// <inheritdoc/>
        public abstract bool Add(TId id, TValue value, out IEntry<TId, TValue> entry);

        /// <inheritdoc/>
        public abstract bool Add(TValue value, out IEntry<TId, TValue> entry);

        /// <inheritdoc/>
        public bool AddCapacity(int additionalCapacity)
        {
            if (Uncapped) return true;
            TargetCapacity = Math.Min(MaxCapacity.Value, TargetCapacity.Value + additionalCapacity);
            return true;
        }

        /// <inheritdoc/>
        public bool CutCapacity(int cap)
        {
            if (Uncapped) return true;
            TargetCapacity = Math.Max(MinCapacity.Value, TargetCapacity.Value - cap);
            return true;
        }

        /// <summary>
        /// Gets the current count of entries in the store.
        /// </summary>
        /// <returns>The count if successful; otherwise <c>null</c>.</returns>
        private long? GetCount()
        {
            return GetCount(out var count) ? count : (long?)null;
        }

        /// <inheritdoc/>
        public abstract bool GetCount(out long count);

        /// <inheritdoc/>
        public abstract bool GetEntryOrDefault(TId? id, out IEntry<TId, TValue> entry);

        /// <inheritdoc/>
        public abstract bool GetValueOrDefault(TId? id, out TValue value);

        /// <inheritdoc/>
        public abstract bool Remove(TId id, out IEntry<TId, TValue> entry);

        /// <inheritdoc/>
        public abstract bool Update(IEntry<TId, TValue> entry);

        /// <inheritdoc/>
        public abstract bool Update(TId id, TValue value);

        /// <summary>
        /// Performs store-specific disposal of resources. Called by <see cref="Dispose(bool)"/>.
        /// </summary>
        protected abstract void DisposeInternal();

        /// <summary>
        /// Releases the resources used by the <see cref="Store{TId, TValue}"/> class.
        /// </summary>
        /// <param name="disposing">When <c>true</c>, called from <see cref="Dispose()"/>; otherwise from the finalizer.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    DisposeInternal();
                }
                disposedValue = true;
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

    }
}