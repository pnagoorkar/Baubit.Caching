using Microsoft.Extensions.Logging;
using System;

namespace Baubit.Caching.Benchmark.Setup.Long.InMemory
{
    public class Store<TValue> : Caching.InMemory.Store<long, TValue>, IStore<TValue>
    {
        private ILogger<Store<TValue>> logger;

        public Store(long? minCap,
                     long? maxCap,
                     ILoggerFactory loggerFactory) : base(minCap, maxCap, GenerateNextId, loggerFactory)
        {
            logger = loggerFactory.CreateLogger<Store<TValue>>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Store{TValue}"/> class without capacity bounds (uncapped).
        /// </summary>
        /// <param name="identityGenerator">Optional identity generator for auto-generating entry IDs.</param>
        /// <param name="loggerFactory">Factory for creating loggers.</param>
        public Store(ILoggerFactory loggerFactory) : this(null, null, loggerFactory)
        {

        }

        private static long? GenerateNextId(long? lastGeneratedId)
        {
            return lastGeneratedId.HasValue ? lastGeneratedId.Value + 1 : 1;
        }
    }
}
