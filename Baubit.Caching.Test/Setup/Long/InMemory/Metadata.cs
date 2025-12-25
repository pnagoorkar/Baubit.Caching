using Microsoft.Extensions.Logging;
using System;

namespace Baubit.Caching.Test.Long.InMemory
{
    public class Metadata : Caching.InMemory.Metadata<long>, IMetadata
    {
        public Metadata(Baubit.Caching.Configuration configuration, ILoggerFactory loggerFactory) : base(configuration, loggerFactory)
        {
        }
    }
}
