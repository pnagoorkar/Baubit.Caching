using System;

namespace Baubit.Caching
{
    public interface ICacheEnumerator
    {
        Guid? CurrentId { get; }
    }
}
