using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Baubit.Caching
{
    public interface IOrderedCache<TId, TValue> : IAsyncEnumerable<IEntry<TId, TValue>>, IFutureAsyncEnumerable<IEntry<TId, TValue>>, IDisposable where TId : struct, IComparable<TId>, IEquatable<TId>
    {
        /// <summary>
        /// The number of entries currently present.
        /// </summary>
        long Count { get; }

        /// <summary>
        /// Gets a named asynchronous enumerator that iterates through cache entries from the current head.
        /// </summary>
        /// <param name="name">The name of the enumerator. If not provided, a new GUID will be generated.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous enumeration.</param>
        /// <returns>An asynchronous enumerator for the cache entries.</returns>
        IAsyncEnumerator<IEntry<TId, TValue>> GetAsyncEnumerator(string name, CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds a new value to the tail of the cache.
        /// </summary>
        /// <param name="value">The value to add.</param>
        /// <param name="entry">When the method returns <c>true</c>, contains the created entry and its assigned id.</param>
        /// <returns><c>true</c> if the value was added; otherwise <c>false</c>.</returns>
        bool Add(TValue value, out IEntry<TId, TValue> entry);

        /// <summary>
        /// Updates an existing entry's value identified by <paramref name="id"/>.
        /// </summary>
        /// <param name="id">The entry identifier.</param>
        /// <param name="value">The updated value.</param>
        /// <returns><c>true</c> if the entry was updated; otherwise <c>false</c>.</returns>
        bool Update(TId id, TValue value);

        /// <summary>
        /// Gets the entry with the specified identifier if it exists.
        /// </summary>
        /// <param name="id">The identifier to look up.</param>
        /// <param name="entry">On success, the located entry; otherwise <c>null</c>.</param>
        /// <returns><c>true</c> if the lookup succeeded (even when not found); otherwise <c>false</c>.</returns>
        bool GetEntryOrDefault(TId? id, out IEntry<TId, TValue> entry);

        /// <summary>
        /// Gets the next entry after <paramref name="id"/>, or the head entry when <paramref name="id"/> is <c>null</c>.
        /// </summary>
        /// <param name="id">The current id, or <c>null</c> to start from the head.</param>
        /// <param name="entry">On success, the next entry; otherwise <c>null</c>.</param>
        /// <returns><c>true</c> if the lookup succeeded (even when not found); otherwise <c>false</c>.</returns>
        bool GetNextOrDefault(TId? id, out IEntry<TId, TValue> entry);

        /// <summary>
        /// Tries to retrieve the first (head) entry.
        /// </summary>
        /// <param name="entry">On success, the first entry; otherwise <c>null</c>.</param>
        /// <returns><c>true</c> if the lookup succeeded (even when not found); otherwise <c>false</c>.</returns>
        bool GetFirstOrDefault(out IEntry<TId, TValue> entry);

        /// <summary>
        /// Returns the id of the first (head) entry.
        /// </summary>
        /// <param name="id">On success, id of the first entry; otherwise <c>null</c></param>
        /// <returns><c>true</c> if the lookup succeeded (even when not found); otherwise <c>false</c>.</returns>
        bool GetFirstIdOrDefault(out TId? id);

        /// <summary>
        /// Tries to retrieve the last (tail) entry.
        /// </summary>
        /// <param name="entry">On success, the last entry; otherwise <c>null</c>.</param>
        /// <returns><c>true</c> if the lookup succeeded (even when not found); otherwise <c>false</c>.</returns>
        bool GetLastOrDefault(out IEntry<TId, TValue> entry);

        /// <summary>
        /// Returns the id of the last (tail) entry.
        /// </summary>
        /// <param name="id">On success, id of the last entry; otherwise <c>null</c></param>
        /// <returns><c>true</c> if the lookup succeeded (even when not found); otherwise <c>false</c>.</returns>
        bool GetLastIdOrDefault(out TId? id);

        /// <summary>
        /// Asynchronously waits for and returns the next entry after <paramref name="id"/>.
        /// When <paramref name="id"/> is <c>null</c> and the cache is non-empty, the head is returned immediately.
        /// Otherwise, the task completes when a new entry is appended.
        /// </summary>
        /// <param name="id">The id to advance from, or <c>null</c> to start from the head.</param>
        /// <param name="cancellationToken">A token to cancel the wait.</param>
        /// <returns>A task that completes with the next entry.</returns>
        Task<IEntry<TId, TValue>> GetNextAsync(TId? id = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously waits for and returns the next entry to be added to the cache.
        /// Unlike <see cref="GetNextAsync"/>, this method always waits for a new entry regardless of current cache state.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the wait.</param>
        /// <returns>A task that completes with the next entry added after this method is called.</returns>
        Task<IEntry<TId, TValue>> GetFutureFirstOrDefaultAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously enumerates existing cache entries from the current head, yielding only entries of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The specific type of values to enumerate. Must be assignable from <typeparamref name="TValue"/>.</typeparam>
        /// <param name="cancellationToken">A token to cancel the enumeration.</param>
        /// <returns>An async enumerable of tuples containing entry IDs and values of type <typeparamref name="T"/>.</returns>
        IAsyncEnumerable<(TId, T)> EnumerateAsync<T>(CancellationToken cancellationToken = default) where T: TValue;

        /// <summary>
        /// Asynchronously enumerates future cache entries starting from the current tail, yielding only entries of type <typeparamref name="T"/>.
        /// This method waits for new entries to be added to the cache.
        /// </summary>
        /// <typeparam name="T">The specific type of values to enumerate. Must be assignable from <typeparamref name="TValue"/>.</typeparam>
        /// <param name="cancellationToken">A token to cancel the enumeration.</param>
        /// <returns>An async enumerable of tuples containing entry IDs and values of type <typeparamref name="T"/>.</returns>
        IAsyncEnumerable<(TId, T)> EnumerateFutureAsync<T>(CancellationToken cancellationToken = default) where T : TValue;

        /// <summary>
        /// Asynchronously processes future cache entries with a handler function, starting from the current tail.
        /// This method waits for new entries to be added and invokes the handler for each entry of type <typeparamref name="T"/>.
        /// The handler receives the cancellation token to support cooperative cancellation within the processing logic.
        /// </summary>
        /// <typeparam name="T">The specific type of values to process. Must be assignable from <typeparamref name="TValue"/>.</typeparam>
        /// <param name="handler">A function that processes each entry tuple, receives state and cancellation token, and returns whether to continue processing.</param>
        /// <param name="state">State object passed to the handler function.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task that completes with <c>true</c> when processing finishes normally.</returns>
        Task<bool> OnNextAsync<T>(Func<(TId, T), object, CancellationToken, Task<bool>> handler, object state, CancellationToken cancellationToken = default) where T : TValue;

        /// <summary>
        /// Removes the entry with the specified identifier.
        /// </summary>
        /// <param name="id">The identifier to remove.</param>
        /// <param name="entry">On success, the removed entry.</param>
        /// <returns><c>true</c> if an entry was removed; otherwise <c>false</c>.</returns>
        bool Remove(TId id, out IEntry<TId, TValue> entry);

        /// <summary>
        /// Removes all entries from the cache.
        /// </summary>
        /// <returns><c>true</c> on success; otherwise <c>false</c>.</returns>
        bool Clear();
    }
}