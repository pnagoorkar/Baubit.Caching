using Microsoft.Extensions.Logging;
using System;

namespace Baubit.Caching
{
    public abstract class Store<TValue> : IStore<TValue>
    {
        public bool Uncapped { get => !TargetCapacity.HasValue; }
        public long? MinCapacity { get; set; } = null;
        public long? MaxCapacity { get; set; } = null;
        public long? TargetCapacity { get; private set; } = null;
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
        public bool HasCapacity { get => Uncapped || CurrentCapacity > 0; }

        public abstract Guid? HeadId { get; }

        public abstract Guid? TailId { get; }

        private ILogger<Store<TValue>> logger;
        private bool disposedValue;

        public Store(long? minCap,
                     long? maxCap,
                     ILoggerFactory loggerFactory)
        {
            TargetCapacity = MinCapacity = minCap;
            MaxCapacity = maxCap;
            logger = loggerFactory.CreateLogger<Store<TValue>>();
        }

        public abstract bool Add(IEntry<TValue> entry);

        public abstract bool Add(Guid id, TValue value, out IEntry<TValue> entry);

        public abstract bool Add(TValue value, out IEntry<TValue> entry);

        public bool AddCapacity(int additionalCapacity)
        {
            if (Uncapped) return true;
            TargetCapacity = Math.Min(MaxCapacity.Value, TargetCapacity.Value + additionalCapacity);
            return true;
        }

        public bool CutCapacity(int cap)
        {
            if (Uncapped) return true;
            TargetCapacity = Math.Max(MinCapacity.Value, TargetCapacity.Value - cap);
            return true;
        }

        private long? GetCount()
        {
            return GetCount(out var count) ? count : (long?)null;
        }

        public abstract bool GetCount(out long count);

        public abstract bool GetEntryOrDefault(Guid? id, out IEntry<TValue> entry);

        public abstract bool GetValueOrDefault(Guid? id, out TValue value);

        public abstract bool Remove(Guid id, out IEntry<TValue> entry);

        public abstract bool Update(IEntry<TValue> entry);

        public abstract bool Update(Guid id, TValue value);

        protected abstract void DisposeInternal();

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

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}