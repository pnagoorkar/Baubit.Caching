using System;

namespace Baubit.Caching.Test.Guid7
{
    public interface IOrderedCache<TValue> : Caching.IOrderedCache<Guid, TValue>
    {
    }
}
