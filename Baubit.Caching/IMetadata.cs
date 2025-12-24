using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Baubit.Caching
{
    /// <summary>
    /// Provides metadata and ordering information for cache entries.
    /// </summary>
    public interface IMetadata<TId> : IDisposable where TId : struct, IComparable<TId>, IEquatable<TId>
    {
        /// <summary>
        /// Gets the number of entries currently tracked by the metadata.
        /// </summary>
        long Count { get; }
        /// <summary>
        /// Gets the identifier of the first (head) entry.
        /// </summary>
        TId? HeadId { get; }
        /// <summary>
        /// Gets the identifier of the last (tail) entry.
        /// </summary>
        TId? TailId { get; }
        /// <summary>
        /// Resets and returns the room count used for adaptive resizing.
        /// </summary>
        /// <returns>The previous room count value.</returns>
        long ResetRoomCount();
        /// <summary>
        /// Adds the specified identifier as the new tail entry.
        /// </summary>
        /// <param name="id">The identifier to add as tail.</param>
        /// <returns><c>true</c> if the operation succeeded; otherwise <c>false</c>.</returns>
        bool AddTail(TId id);
        /// <summary>
        /// Determines whether the specified identifier is present in the metadata.
        /// </summary>
        /// <param name="id">The identifier to check.</param>
        /// <returns><c>true</c> if the identifier exists; otherwise <c>false</c>.</returns>
        bool ContainsKey(TId id);
        /// <summary>
        /// Gets the next identifier after the specified one.
        /// </summary>
        /// <param name="id">The current identifier.</param>
        /// <param name="nextId">On success, the next identifier; otherwise <c>null</c>.</param>
        /// <returns><c>true</c> if the lookup succeeded; otherwise <c>false</c>.</returns>
        bool GetNextId(TId? id, out TId? nextId);
        /// <summary>
        /// Asynchronously gets the next identifier after the specified one.
        /// </summary>
        /// <param name="id">The current identifier.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task that completes with the next identifier.</returns>
        Task<TId> GetNextIdAsync(TId? id, CancellationToken cancellationToken);
        /// <summary>
        /// Gets all identifiers from the head through the specified identifier, inclusive.
        /// </summary>
        /// <param name="id">The end identifier.</param>
        /// <param name="ids">On success, the sequence of identifiers.</param>
        /// <returns><c>true</c> if the operation succeeded; otherwise <c>false</c>.</returns>
        bool GetIdsThrough(TId id, out IEnumerable<TId> ids);
        /// <summary>
        /// Removes the specified identifier from the metadata.
        /// </summary>
        /// <param name="id">The identifier to remove.</param>
        /// <returns><c>true</c> if the identifier was removed; otherwise <c>false</c>.</returns>
        bool Remove(TId id);
    }
    public interface IMetadata : IMetadata<Guid>, IDisposable
    {
    }
}