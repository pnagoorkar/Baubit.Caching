using System;

namespace Baubit.Caching
{
    /// <summary>
    /// Enumerates over cache entries, exposing the current entry's identifier.
    /// </summary>
    public interface ICacheEnumerator
    {
        /// <summary>
        /// Gets the identifier of the current entry in the enumeration, or <c>null</c> if not positioned.
        /// </summary>
        Guid? CurrentId { get; }
    }
}
