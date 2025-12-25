using System;

namespace Baubit.Caching
{
    public interface IEntry<TId, TValue> where TId : struct, IComparable<TId>, IEquatable<TId>
    {
        /// <summary>
        /// Gets the unique identifier for this entry.
        /// </summary>
        TId Id { get; }
        /// <summary>
        /// Gets the UTC timestamp when this entry was created.
        /// </summary>
        DateTime CreatedOnUTC { get; }
        /// <summary>
        /// Gets the value stored in this entry.
        /// </summary>
        TValue Value { get; }
    }
    /// <summary>
    /// Represents a cache entry with a Guid identifier (GuidV7, time-ordered).
    /// Specialization of <see cref="IEntry{TId, TValue}"/> with Guid as the identifier type.
    /// </summary>
    /// <typeparam name="TValue">The type of value stored in the entry.</typeparam>
    public interface IEntry<TValue> : IEntry<Guid, TValue>
    {
    }
}