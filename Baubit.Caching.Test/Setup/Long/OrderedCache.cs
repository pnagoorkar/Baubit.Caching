using Microsoft.Extensions.Logging;
using System;

namespace Baubit.Caching.Test.Long
{
    public class OrderedCache<TValue> : OrderedCache<long, TValue>, IOrderedCache<TValue>
    {
        public OrderedCache(Baubit.Caching.Configuration cacheConfiguration,
                            IStore<TValue> l1Store,
                            IStore<TValue> l2Store,
                            IMetadata metadata,
                            ILoggerFactory loggerFactory,
                            Func<CacheEnumeratorCollection> cacheEnumeratorCollectionFactory = null,
                            ICacheAsyncEnumeratorFactory<TValue> enumeratorFactory = null) : base(cacheConfiguration,
                                                                                                  l1Store,
                                                                                                  l2Store,
                                                                                                  metadata,
                                                                                                  loggerFactory,
                                                                                                  cacheEnumeratorCollectionFactory,
                                                                                                  enumeratorFactory)
        {
        }
    }
}
