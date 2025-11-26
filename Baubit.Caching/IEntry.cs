using System;

namespace Baubit.Caching
{
    public interface IEntry<TValue>
    {
        Guid Id { get; }
        DateTime CreatedOnUTC { get; }
        TValue Value { get; }
    }
}