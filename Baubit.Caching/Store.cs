using Microsoft.Extensions.Logging;
using System;

namespace Baubit.Caching
{
    /// <summary>
    /// Abstract base implementation of <see cref="IStore{TValue}"/> providing common capacity management logic.
    /// Derived classes must implement storage-specific operations (add, get, remove, update).
    /// </summary>
    /// <typeparam name="TValue">The type of values stored in this store.</typeparam>
    public abstract class Store<TValue> : IStore<TValue>
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

        private ILogger<Store<TValue>> logger;
        private bool disposedValue;

        /// <summary>
        /// Initializes a new instance of the <see cref="Store{TValue}"/> class with specified capacity bounds.
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
            logger = loggerFactory.CreateLogger<Store<TValue>>();
        }

        /// <inheritdoc/>
        public abstract bool Add(IEntry<TValue> entry);

        /// <inheritdoc/>
        public abstract bool Add(Guid id, TValue value, out IEntry<TValue> entry);

        /// <inheritdoc/>
        public abstract bool Add(TValue value, out IEntry<TValue> entry);

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
        public abstract bool GetEntryOrDefault(Guid? id, out IEntry<TValue> entry);

        /// <inheritdoc/>
        public abstract bool GetValueOrDefault(Guid? id, out TValue value);

        /// <inheritdoc/>
        public abstract bool Remove(Guid id, out IEntry<TValue> entry);

        /// <inheritdoc/>
        public abstract bool Update(IEntry<TValue> entry);

        /// <inheritdoc/>
        public abstract bool Update(Guid id, TValue value);

        /// <summary>
        /// Performs store-specific disposal of resources. Called by <see cref="Dispose(bool)"/>.
        /// </summary>
        protected abstract void DisposeInternal();

        /// <summary>
        /// Releases the resources used by the <see cref="Store{TValue}"/> class.
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