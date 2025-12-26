using System;

namespace Baubit.Caching.Test.Guid7
{
    public interface IStore<TValue> : IStore<Guid, TValue>
    {
    }
}
