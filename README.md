# Baubit.Caching

[![CircleCI](https://dl.circleci.com/status-badge/img/circleci/TpM4QUH8Djox7cjDaNpup5/2zTgJzKbD2m3nXCf5LKvqS/tree/master.svg?style=svg)](https://dl.circleci.com/status-badge/redirect/circleci/TpM4QUH8Djox7cjDaNpup5/2zTgJzKbD2m3nXCf5LKvqS/tree/master)
[![codecov](https://codecov.io/gh/pnagoorkar/Baubit.Caching/branch/master/graph/badge.svg)](https://codecov.io/gh/pnagoorkar/Baubit.Caching)
[![NuGet](https://img.shields.io/nuget/v/Baubit.Caching.svg)](https://www.nuget.org/packages/Baubit.Caching/)

Thread-safe ordered cache for .NET 9 with O(1) lookups, two-tier storage, and async enumeration.

## Installation

```bash
dotnet add package Baubit.Caching
```

## Why?

### 1. Time-Ordered Identity Without Dual Fields

Event sourcing and audit logs need both unique IDs and timestamps:

```csharp
// ❌ Redundant: Separate ID + Timestamp fields
public record Event(Guid Id, DateTime Timestamp, string Data);
```

`OrderedCache` uses GuidV7 which embeds time-ordering in the ID itself:

```csharp
// ✅ Efficient: Single sortable, time-ordered ID
public record Event(Guid Id, string Data); // Id is naturally chronological
```

### 2. Transparent Multi-Tier Cache

Multi-tier caches typically require clients to check multiple stores:

```csharp
// ❌ Complex: Client must orchestrate L1/L2 checks
var entry = l1Cache.Get(id) ?? l2Cache.Get(id);
```

`OrderedCache` provides automatic L1→L2 fallback with replenishment:

```csharp
// ✅ Transparent: Single call handles L1 miss + L2 lookup + replenish
cache.GetEntryOrDefault(id, out var entry); // Automatic tier management
```

### 3. Resilient Iteration Despite Deletions

Traditional ordered collections break when entries are deleted during iteration:

```csharp
// ❌ Problem: Entry deleted mid-iteration → enumerator crashes or skips data
```

`OrderedCache` handles out-of-order deletions gracefully by finding the next valid entry:

```csharp
// ✅ Resilient: Deletion doesn't break enumeration
cache.Remove(currentId, out _);
cache.GetNextOrDefault(currentId, out var next); // Finds next valid entry
```

### 4. Multi-Speed Consumer Memory Management

Multiple consumers reading at different speeds cause memory leaks:

```csharp
// ❌ Problem: Fast consumers read 1000 entries, slow consumer at entry 10
// → Cache holds 990 unneeded entries
```

`OrderedCache` tracks all active enumerators and automatically evicts only entries that **all consumers** have passed:

```csharp
// ✅ Automatic: Evicts entries behind slowest consumer
var config = new Configuration { EvictAfterEveryX = 100 };
```

### 5. Producer-Consumer Coordination

Traditional caches require polling to detect new entries:

```csharp
// ❌ Inefficient: Polling loop
while (true)
{
    if (cache.TryGet(nextId, out var entry))
    {
        Process(entry);
        nextId = entry.NextId;
    }
    else
    {
        await Task.Delay(100); // Wasted CPU, added latency
    }
}
```

`OrderedCache` eliminates polling with `IAsyncEnumerable`:

```csharp
// ✅ Efficient: Await future entries
await foreach (var entry in cache.GetFutureAsyncEnumerator(cancellationToken))
{
    Process(entry); // Executes immediately when producer adds entry
}
```

### Key Benefits Summary

1. **Time-Ordered IDs**: GuidV7 provides sortable, collision-free identifiers
2. **Transparent Tiering**: L1/L2 fallback invisible to consumers
3. **Deletion Resilient**: Iteration continues despite out-of-order removals
4. **Memory Safety**: Automatic eviction behind slowest consumer prevents leaks
5. **Zero Latency**: Consumers resume instantly when producers add entries (no polling delay)
6. **Zero CPU Waste**: Consumers block efficiently (no spin loops)
7. **Adaptive Sizing**: Memory usage adjusts to production rate automatically
8. **Extensible Design**: Pluggable storage backends (L2 can be backed by persistent storage) and metadata implementations (enabling distributed cache scenarios via `Baubit.Caching.Redis` - work in progress)

## Architecture

```
+-------------------------------------------------------+
|                  OrderedCache<TValue>                 |
|                                                       |
|   +----------------+        +----------------+        |
|   |    L1 Store    |  ───▶  |    L2 Store    |        |
|   |   (Bounded)    |        |   (Bounded)    |        |
|   +----------------+        +----------------+        |
|           │                         │                 |
|           └───────────┬─────────────┘                 |
|                       │                               |
|               +-------▼--------+                      |
|               |    Metadata    |                      |
|               |  (LinkedList)  |                      |
|               +----------------+                      |
+-------------------------------------------------------+

```

- **L1 Store**: Optional bounded in-memory cache (hot entries)
- **L2 Store**: Required unbounded backing store (all entries)
- **Metadata**: Ordered linked list of GuidV7 IDs with O(1) head/tail access
- **Locking**: `ReaderWriterLockSlim` for concurrent access

## API Reference

### IOrderedCache&lt;TValue&gt;

```csharp
public interface IOrderedCache<TValue> : IAsyncEnumerable<IEntry<TValue>>, IDisposable
{
    long Count { get; }
    
    // Write Operations
    bool Add(TValue value, out IEntry<TValue> entry);
    bool Update(Guid id, TValue value);
    bool Remove(Guid id, out IEntry<TValue>? entry);
    bool Clear();
    
    // Synchronous Read Operations
    bool GetEntryOrDefault(Guid? id, out IEntry<TValue>? entry);
    bool GetNextOrDefault(Guid? id, out IEntry<TValue>? entry);
    bool GetFirstOrDefault(out IEntry<TValue>? entry);
    bool GetFirstIdOrDefault(out Guid? id);
    bool GetLastOrDefault(out IEntry<TValue>? entry);
    bool GetLastIdOrDefault(out Guid? id);
    
    // Asynchronous Operations
    Task<IEntry<TValue>> GetNextAsync(Guid? id = null, CancellationToken ct = default);
    Task<IEntry<TValue>> GetFutureFirstOrDefaultAsync(CancellationToken ct = default);
}
```

### IEntry&lt;TValue&gt;

```csharp
public interface IEntry<TValue>
{
    Guid Id { get; }              // GuidV7 (time-ordered)
    DateTime CreatedOnUTC { get; }
    TValue Value { get; }
}
```

### Configuration

```csharp
public record Configuration
{
    bool RunAdaptiveResizing { get; init; } = false;  // Enable L1 dynamic sizing
    int AdaptionWindowMS { get; init; } = 2_000;      // Resize evaluation interval
    int GrowStep { get; init; } = 64;                 // L1 growth increment
    int ShrinkStep { get; init; } = 32;               // L1 shrink decrement
    double RoomRateLowerLimit { get; init; } = 1;     // Shrink threshold (entries/sec)
    double RoomRateUpperLimit { get; init; } = 5;     // Grow threshold (entries/sec)
    int EvictAfterEveryX { get; init; } = 100;        // Eviction frequency (adds)
}
```

## Usage

### Basic Setup

```csharp
using Baubit.Caching;
using Baubit.Caching.InMemory;
using Microsoft.Extensions.Logging;

var config = new Configuration { EvictAfterEveryX = 100 };
var metadata = new Metadata { Configuration = config };
var l1Store = new Store<string>(100, 1000, loggerFactory); // Min: 100, Max: 1000
var l2Store = new Store<string>(loggerFactory);            // Uncapped

using var cache = new OrderedCache<string>(
    config, l1Store, l2Store, metadata, loggerFactory
);
```

### Write Operations

```csharp
// Add entry (appends to tail)
cache.Add("value", out var entry);
Console.WriteLine(entry.Id);  // e.g., 01933c4a-4f2e-7b40-8000-123456789abc

// Update existing entry
cache.Update(entry.Id, "new_value");

// Remove entry
cache.Remove(entry.Id, out var removed);
```

### Read Operations

```csharp
// Direct access by ID
cache.GetEntryOrDefault(id, out var entry);

// Get head/tail
cache.GetFirstOrDefault(out var first);
cache.GetLastOrDefault(out var last);

// Sequential navigation
cache.GetNextOrDefault(currentId, out var next);
```

### Async Enumeration

```csharp
// Enumerate existing entries
await foreach (var entry in cache)
{
    Console.WriteLine($"{entry.Id}: {entry.Value}");
}

// Wait for future entries (blocks until new entries arrive)
await foreach (var entry in cache.GetFutureAsyncEnumerator())
{
    Console.WriteLine($"New: {entry.Value}");
}
```

### Async Waiting

```csharp
// Wait for next entry after current position
var next = await cache.GetNextAsync(currentId, cancellationToken);

// Wait for first future entry (after current tail)
var future = await cache.GetFutureFirstOrDefaultAsync(cancellationToken);
```

## Performance

**System:** Intel Core Ultra 9 185H @ 2.50GHz, .NET 9.0.11

| Operation | Latency | Throughput | Allocations |
|-----------|---------|------------|-------------|
| `GetFirstOrDefault` | 66 ns | 15.19M ops/sec | 0 B |
| `GetEntryOrDefault` | 101 ns | 9.87M ops/sec | 0 B |
| `GetNextOrDefault` | 184 ns | 5.44M ops/sec | 40 B |
| `Update` | 431 ns | 2.32M ops/sec | 208 B |
| `Add` | 1,013 ns | 987K ops/sec | 216 B |

**Workload Scenarios:**

| Workload | Throughput | Description |
|----------|------------|-------------|
| Read-Only | 7.6-15M ops/sec | `GetEntry`/`GetFirst` operations |
| Write-Only (Add) | ~1M ops/sec | Append-only logging |
| Write-Only (Update) | ~2.3M ops/sec | In-place modifications |
| Mixed (80/20 R/W) | 500-580K ops/sec | Read-heavy web apps |
| Mixed (50/50 R/W) | 700-830K ops/sec | Balanced workloads |

**Characteristics:**
- Zero allocations on read operations (`GetFirst`, `GetEntry`)
- Read operations 10-100x faster than writes
- Consistent performance scaling (1K → 10K entries)
- No lock contention in single-threaded scenarios

See [Baubit.Caching.Benchmark/RESULTS.md](Baubit.Caching.Benchmark/RESULTS.md) for detailed analysis.

## Adaptive Resizing

When enabled, L1 capacity dynamically adjusts based on production rate:

```csharp
var config = new Configuration
{
    RunAdaptiveResizing = true,
    AdaptionWindowMS = 2_000,        // Sample every 2 seconds
    RoomRateUpperLimit = 5,          // Grow if >5 entries/sec
    RoomRateLowerLimit = 1,          // Shrink if <1 entry/sec
    GrowStep = 64,
    ShrinkStep = 32
};
```

**Behavior:**
- Measures entries added per second
- Grows L1 when rate exceeds `RoomRateUpperLimit`
- Shrinks L1 when rate falls below `RoomRateLowerLimit`
- Automatically replenishes L1 from L2 after shrink

## Eviction

Entries are evicted based on active enumerator positions:

```csharp
var config = new Configuration { EvictAfterEveryX = 100 };
```

- Every 100 `Add` operations, evicts entries before the slowest active enumerator
- Prevents memory growth when consumers lag behind producers
- Disabled by setting `EvictAfterEveryX = int.MaxValue`

## Thread Safety

- **Concurrent Reads**: Multiple threads can read simultaneously (read lock)
- **Writes Block All**: Single writer blocks all readers/writers (write lock)
- **Lock Implementation**: `ReaderWriterLockSlim` with recursive support
- **Deadlock Prevention**: Always acquire locks in consistent order

## Use Cases

| Scenario | Why OrderedCache |
|----------|------------------|
| **Event Sourcing** | Maintains insertion order, async iteration |
| **Message Queues** | FIFO semantics with random access by ID |
| **Audit Logs** | Time-ordered entries with fast lookup |
| **Time-Series Cache** | GuidV7 provides chronological ordering |
| **Change Data Capture** | Stream processing with position tracking |

## Limitations

- **Not**: Random-access key-value store (use `Dictionary<,>`)
- **Not**: Distributed cache (single-process only)
- **Not**: Persistent storage (use database for durability)
- **L1 Bounded**: May not contain all entries (check L2 on miss)
- **Write Latency**: ~1μs per `Add` due to locking + metadata updates

## Benchmarks

```bash
cd Baubit.Caching.Benchmark
dotnet run -c Release
```

Results saved to `RESULTS.md` with ops/sec metrics for read/write/mixed scenarios.


## License

MIT License
