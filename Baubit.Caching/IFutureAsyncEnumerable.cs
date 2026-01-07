using System.Collections.Generic;
using System.Threading;

namespace Baubit.Caching
{
    /// <summary>
    /// An asynchronous enumerable that supports enumerating future entries not yet present in the collection.
    /// </summary>
    /// <typeparam name="T">The type of elements returned by the enumerator.</typeparam>
    public interface IFutureAsyncEnumerable<T> : IAsyncEnumerable<T>
    {
        /// <summary>
        /// Gets an asynchronous enumerator that can wait for future elements.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the asynchronous enumeration.</param>
        /// <returns>An asynchronous enumerator for future elements.</returns>
        IAsyncEnumerator<T> GetFutureAsyncEnumerator(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a named asynchronous enumerator that can wait for future elements.
        /// </summary>
        /// <param name="name">The name of the enumerator. If not provided, a new GUID will be generated.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous enumeration.</param>
        /// <returns>An asynchronous enumerator for future elements.</returns>
        IAsyncEnumerator<T> GetFutureAsyncEnumerator(string name, CancellationToken cancellationToken = default);
    }
}
