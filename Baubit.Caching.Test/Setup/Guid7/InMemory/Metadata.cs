using Microsoft.Extensions.Logging;
using System;

namespace Baubit.Caching.Test.Guid7.InMemory
{
    public class Metadata : Caching.InMemory.Metadata<Guid>, IMetadata
    {
        public Metadata(Baubit.Caching.Configuration configuration, ILoggerFactory loggerFactory) : base(configuration, loggerFactory)
        {
        }
    }
}
