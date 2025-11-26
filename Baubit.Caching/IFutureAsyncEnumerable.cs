using System.Collections.Generic;
using System.Threading;

namespace Baubit.Caching
{
    public interface IFutureAsyncEnumerable<T> : IAsyncEnumerable<T>
    {
        IAsyncEnumerator<T> GetFutureAsyncEnumerator(CancellationToken cancellationToken = default);
    }
}
