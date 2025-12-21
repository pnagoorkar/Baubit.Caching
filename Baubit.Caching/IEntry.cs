using System;

namespace Baubit.Caching
{
    /// <summary>
    /// Represents a cache entry with an identifier, creation timestamp, and value.
    /// </summary>
    /// <typeparam name="TValue">The type of value stored in the entry.</typeparam>
    public interface IEntry<TValue>
    {
        /// <summary>
        /// Gets the unique identifier for this entry.
        /// </summary>
        Guid Id { get; }
        /// <summary>
        /// Gets the UTC timestamp when this entry was created.
        /// </summary>
        DateTime CreatedOnUTC { get; }
        /// <summary>
        /// Gets the value stored in this entry.
        /// </summary>
        TValue Value { get; }
    }
}