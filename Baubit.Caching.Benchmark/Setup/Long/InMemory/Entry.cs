using System;

namespace Baubit.Caching.Benchmark.Setup.Long.InMemory
{
    public class Entry<TValue> : Caching.InMemory.Entry<long, TValue>
    {
        public Entry(long id, TValue value) : base(id, value)
        {
        }
    }
}
