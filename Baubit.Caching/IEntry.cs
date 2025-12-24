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
    public interface IEntry<TValue> : IEntry<Guid, TValue>
    {
    }
}