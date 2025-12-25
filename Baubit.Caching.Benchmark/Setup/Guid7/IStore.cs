using System;

namespace Baubit.Caching.Benchmark.Setup.Guid7
{
    public interface IStore<TValue> : IStore<Guid, TValue>
    {
    }
}
