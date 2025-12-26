using System;

namespace Baubit.Caching.Benchmark.Setup.Guid7
{
    public interface IOrderedCache<TValue> : Caching.IOrderedCache<Guid, TValue>
    {
    }
}
