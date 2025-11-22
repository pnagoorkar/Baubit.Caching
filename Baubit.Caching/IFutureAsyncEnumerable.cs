namespace Baubit.Caching
{
    public interface IFutureAsyncEnumerable<T> : IAsyncEnumerable<T>
    {
        IAsyncEnumerator<T> GetFutureAsyncEnumerator(CancellationToken cancellationToken = default);
        IAsyncEnumerator<T> IAsyncEnumerable<T>.GetAsyncEnumerator(CancellationToken cancellationToken) => GetFutureAsyncEnumerator(cancellationToken);
    }
}
