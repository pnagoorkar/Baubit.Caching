using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Baubit.Caching
{
    public interface IMetadata : IDisposable
    {
        long Count { get; }
        Guid? HeadId { get; }
        Guid? TailId { get; }

        long ResetRoomCount();
        bool AddTail(Guid id);
        bool ContainsKey(Guid id);
        bool GetNextId(Guid? id, out Guid? nextId);
        bool GenerateNextId(out Guid nextId);
        Task<Guid> GetNextIdAsync(Guid? id, CancellationToken cancellationToken);
        bool GetIdsThrough(Guid id, out IEnumerable<Guid> ids);
        bool Remove(Guid id);
    }
}