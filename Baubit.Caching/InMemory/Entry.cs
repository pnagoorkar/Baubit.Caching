using System;

namespace Baubit.Caching.InMemory
{
    public class Entry<TId, TValue> : IEntry<TId, TValue> where TId : struct, IComparable<TId>, IEquatable<TId>
    {
        /// <inheritdoc/>
        public TId Id { get; set; }
        /// <inheritdoc/>
        public DateTime CreatedOnUTC { get; set; } = DateTime.UtcNow;
        /// <inheritdoc/>
        public TValue Value { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Entry{TValue}"/> class.
        /// </summary>
        /// <param name="id">The unique identifier for this entry.</param>
        /// <param name="value">The value to store.</param>
        public Entry(TId id, TValue value)
        {
            Id = id;
            Value = value;
        }
    }

    public class Entry<TValue> : Entry<Guid, TValue>
    {
        public Entry(Guid id, TValue value) : base(id, value)
        {
            Id = id;
            Value = value;
        }
    }
}