using Baubit.Identity;
using Microsoft.Extensions.Logging;
using System;

namespace Baubit.Caching.Test.Guid7.InMemory
{
    /// <summary>
    /// In-memory implementation of <see cref="IStore{TValue}"/> using a dictionary for storage.
    /// Thread-safe for concurrent readers/writers when used with external synchronization.
    /// </summary>
    /// <typeparam name="TValue">The type of values stored in this store.</typeparam>
    public class Store<TValue> : Caching.InMemory.Store<Guid, TValue>, IStore<TValue>
    {
        private readonly IIdentityGenerator identityGenerator;

        private ILogger<Store<TValue>> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="Store{TValue}"/> class with capacity bounds.
        /// </summary>
        /// <param name="minCap">Minimum capacity for the store.</param>
        /// <param name="maxCap">Maximum capacity for the store.</param>
        /// <param name="identityGenerator">Optional identity generator for auto-generating entry IDs.</param>
        /// <param name="loggerFactory">Factory for creating loggers.</param>
        public Store(long? minCap,
                     long? maxCap,
                     IIdentityGenerator identityGenerator,
                     ILoggerFactory loggerFactory) : base(minCap, maxCap, lastGenId => GenerateNextId(lastGenId, identityGenerator), loggerFactory)
        {
            this.identityGenerator = identityGenerator;
            logger = loggerFactory.CreateLogger<Store<TValue>>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Store{TValue}"/> class without capacity bounds (uncapped).
        /// </summary>
        /// <param name="identityGenerator">Optional identity generator for auto-generating entry IDs.</param>
        /// <param name="loggerFactory">Factory for creating loggers.</param>
        public Store(IIdentityGenerator identityGenerator, ILoggerFactory loggerFactory) : this(null, null, identityGenerator, loggerFactory)
        {

        }

        private static Guid? GenerateNextId(Guid? lastGeneratedId, IIdentityGenerator identityGenerator)
        {
            if (identityGenerator == null) return null;
            // Initialize from last generated ID if available to ensure monotonicity
            if (lastGeneratedId.HasValue)
            {
                identityGenerator.InitializeFrom(lastGeneratedId.Value);
            }

            return identityGenerator.GetNext();
        }
    }
}
