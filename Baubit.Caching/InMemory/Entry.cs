using System;

namespace Baubit.Caching.InMemory
{
    public class Entry<TValue> : IEntry<TValue>
    {
        public Guid Id { get; set; }
        public DateTime CreatedOnUTC { get; set; } = DateTime.UtcNow;
        public TValue Value { get; set; }
        public Entry(Guid id, TValue value)
        {
            Id = id;
            Value = value;
        }
    }
}