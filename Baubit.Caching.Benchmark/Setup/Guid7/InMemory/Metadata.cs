using Microsoft.Extensions.Logging;
using System;

namespace Baubit.Caching.Benchmark.Setup.Guid7.InMemory
{
    public class Metadata : Caching.InMemory.Metadata<Guid>, IMetadata
    {
        public Metadata(Configuration configuration, ILoggerFactory loggerFactory) : base(configuration, loggerFactory)
        {
        }
    }
}
