using System;

namespace Baubit.Caching.Test.Long
{
    public interface IOrderedCache<TValue> : Caching.IOrderedCache<long, TValue>
    {
    }
}
