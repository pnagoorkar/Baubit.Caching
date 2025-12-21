using Baubit.Tasks;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Baubit.Caching
{
    /// <summary>
    /// Coordinates concurrent awaiters (guests) waiting for a single result value.
    /// When a result is set, all waiting guests are notified.
    /// </summary>
    /// <typeparam name="TValue">The type of value awaited by guests.</typeparam>
    public class WaitingRoom<TValue> : IDisposable
    {
        /// <summary>
        /// Gets whether there are any guests currently waiting for a result.
        /// </summary>
        public bool HasGuests { get => numOfGuests > 0; }

        private TaskCompletionSource<TValue> tcs = new TaskCompletionSource<TValue>(TaskCreationOptions.RunContinuationsAsynchronously);

        private volatile int numOfGuests = 0;

        private bool disposedValue;

        /// <summary>
        /// Allows a caller to join the waiting room and await a result.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the wait.</param>
        /// <returns>A task that completes when a result is set or cancellation is requested.</returns>
        public async Task<TValue> Join(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                return await Task.FromCanceled<TValue>(cancellationToken);

            Interlocked.Increment(ref numOfGuests);
            try
            {
                return await tcs.Task.WaitAsync(cancellationToken);
            }
            finally
            {
                Interlocked.Decrement(ref numOfGuests); // keep HasGuests accurate
            }
        }

        /// <summary>
        /// Sets the result value, completing all waiting tasks.
        /// </summary>
        /// <param name="value">The result value to provide to all guests.</param>
        /// <returns><c>true</c> if the result was successfully set; otherwise <c>false</c>.</returns>
        public bool TrySetResult(TValue value)
        {
            return tcs.TrySetResult(value);
        }

        /// <summary>
        /// Cancels all waiting tasks.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token to associate with the cancellation.</param>
        /// <returns><c>true</c> if cancellation was successfully applied; otherwise <c>false</c>.</returns>
        public bool TrySetCanceled(CancellationToken cancellationToken = default)
        {
            return tcs.TrySetCanceled(cancellationToken);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    TrySetCanceled();
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}