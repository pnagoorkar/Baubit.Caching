using System;

namespace Baubit.Caching.Benchmark.Setup.Guid7.InMemory
{
    public class Entry<TValue> : Caching.InMemory.Entry<Guid, TValue>
    {
        public Entry(Guid id, TValue value) : base(id, value)
        {
        }
    }
}
