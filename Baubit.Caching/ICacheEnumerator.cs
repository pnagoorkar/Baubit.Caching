using System;

namespace Baubit.Caching
{
    /// <summary>
    /// Enumerates over cache entries, exposing the current entry's identifier.
    /// </summary>
    public interface ICacheEnumerator<TId> where TId : struct, IComparable<TId>, IEquatable<TId>
    {
        /// <summary>
        /// Gets the identifier of the current entry in the enumeration, or <c>null</c> if not positioned.
        /// </summary>
        TId? CurrentId { get; }

        /// <summary>
        /// Gets the identifier of this enumerator.
        /// </summary>
        string Id { get; }
    }
}
