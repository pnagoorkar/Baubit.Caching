using System;
using System.Threading;

namespace Baubit.Caching
{
    public class CacheAsyncEnumerator<TValue> : BaseCacheAsyncEnumerator<TValue>
    {
        public CacheAsyncEnumerator(IOrderedCache<TValue> cache,
                                    Action<ICacheEnumerator> onDispose,
                                    CancellationToken cancellationToken = default) : base(cache, onDispose, cancellationToken)
        {
        }
    }
}