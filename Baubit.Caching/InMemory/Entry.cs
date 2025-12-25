using System;

namespace Baubit.Caching.InMemory
{
    /// <summary>
    /// In-memory cache entry with a generic identifier.
    /// Simple implementation of <see cref="IEntry{TId, TValue}"/> for use with in-memory stores.
    /// </summary>
    /// <typeparam name="TId">The type of the entry identifier. Must be a struct implementing IComparable&lt;TId&gt; and IEquatable&lt;TId&gt;.</typeparam>
    /// <typeparam name="TValue">The type of value stored in the entry.</typeparam>
    public class Entry<TId, TValue> : IEntry<TId, TValue> where TId : struct, IComparable<TId>, IEquatable<TId>
    {
        /// <inheritdoc/>
        public TId Id { get; set; }
        /// <inheritdoc/>
        public DateTime CreatedOnUTC { get; set; } = DateTime.UtcNow;
        /// <inheritdoc/>
        public TValue Value { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Entry{TId, TValue}"/> class.
        /// </summary>
        /// <param name="id">The unique identifier for this entry.</param>
        /// <param name="value">The value to store.</param>
        public Entry(TId id, TValue value)
        {
            Id = id;
            Value = value;
        }
    }

    /// <summary>
    /// In-memory cache entry with a Guid identifier (GuidV7, time-ordered).
    /// Specialization of <see cref="Entry{TId, TValue}"/> with Guid as the identifier type.
    /// </summary>
    /// <typeparam name="TValue">The type of value stored in the entry.</typeparam>
    public class Entry<TValue> : Entry<Guid, TValue>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Entry{TValue}"/> class.
        /// </summary>
        /// <param name="id">The unique Guid identifier for this entry.</param>
        /// <param name="value">The value to store.</param>
        public Entry(Guid id, TValue value) : base(id, value)
        {
            Id = id;
            Value = value;
        }
    }
}