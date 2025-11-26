using System;

namespace Baubit.Caching
{
    public interface ICacheEnumerator
    {
        public Guid? CurrentId { get; }
    }
}
