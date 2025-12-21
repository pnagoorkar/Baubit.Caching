using System;

namespace Baubit.Caching.InMemory
{
    /// <summary>
    /// In-memory implementation of <see cref="IEntry{TValue}"/> representing a cache entry.
    /// </summary>
    /// <typeparam name="TValue">The type of value stored in the entry.</typeparam>
    public class Entry<TValue> : IEntry<TValue>
    {
        /// <inheritdoc/>
        public Guid Id { get; set; }
        /// <inheritdoc/>
        public DateTime CreatedOnUTC { get; set; } = DateTime.UtcNow;
        /// <inheritdoc/>
        public TValue Value { get; set; }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="Entry{TValue}"/> class.
        /// </summary>
        /// <param name="id">The unique identifier for this entry.</param>
        /// <param name="value">The value to store.</param>
        public Entry(Guid id, TValue value)
        {
            Id = id;
            Value = value;
        }
    }
}