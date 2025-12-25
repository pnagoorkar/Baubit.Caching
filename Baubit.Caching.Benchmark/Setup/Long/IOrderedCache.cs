using System;

namespace Baubit.Caching.Benchmark.Setup.Long
{
    public interface IOrderedCache<TValue> : Caching.IOrderedCache<long, TValue>
    {
    }
}
