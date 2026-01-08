# Baubit.Caching

[![CircleCI](https://dl.circleci.com/status-badge/img/circleci/TpM4QUH8Djox7cjDaNpup5/2zTgJzKbD2m3nXCf5LKvqS/tree/master.svg?style=svg)](https://dl.circleci.com/status-badge/redirect/circleci/TpM4QUH8Djox7cjDaNpup5/2zTgJzKbD2m3nXCf5LKvqS/tree/master)
[![codecov](https://codecov.io/gh/pnagoorkar/Baubit.Caching/branch/master/graph/badge.svg)](https://codecov.io/gh/pnagoorkar/Baubit.Caching)<br/>
[![NuGet](https://img.shields.io/nuget/v/Baubit.Caching.svg)](https://www.nuget.org/packages/Baubit.Caching/)
[![NuGet](https://img.shields.io/nuget/dt/Baubit.Caching.svg)](https://www.nuget.org/packages/Baubit.Caching) <br/>
![.NET Standard 2.0](https://img.shields.io/badge/.NET%20Standard-2.0-512BD4?logo=dotnet&logoColor=white)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)<br/>
[![Known Vulnerabilities](https://snyk.io/test/github/pnagoorkar/Baubit.Caching/badge.svg)](https://snyk.io/test/github/pnagoorkar/Baubit.Caching)

**DI Extension**: [Baubit.Caching.DI](https://github.com/pnagoorkar/Baubit.Caching.DI)  
**Extensions for v2025.52+ breaking changes**: [Baubit.Caching.Extensions](https://github.com/pnagoorkar/Baubit.Caching.Extensions)  
**LiteDB persistence**: [Baubit.Caching.LiteDB](https://github.com/pnagoorkar/Baubit.Caching.LiteDB)  
**Distributed cache samples**: [Samples](https://github.com/pnagoorkar/Baubit.Caching.DI/tree/master/Samples)

Thread-safe ordered cache with O(1) lookups, two-tier storage, and async enumeration.

#### **In 30 seconds:** 
`OrderedCache<TId, TValue>` is an append-ordered, time-sortable cache with generic ID support. Each entry uses IDs like GuidV7 (time-ordered) or custom types (int, long). You can:
- fetch any entry by ID in O(1),
- walk entries in chronological order,
- `await foreach` future entries with zero polling,
- safely evict entries once all consumers have passed them.

**Use it for:** event sourcing, CDC pipelines, audit logs, FIFO-ish queues with random access, time-series buffering.  
**Don’t use it for:** generic key/value caching, TTL caches.
## Table of Contents

- [Installation](#installation)
- [Why?](#why)
  - [TL;DR](#tldr)
  - [In-Depth](#in-depth)
- [Core Concepts](#core-concepts)
- [Architecture](#architecture)
- [API Reference](#api-reference)
- [Usage](#usage)
  - [Basic Setup](#basic-setup)
  - [Write Operations](#write-operations)
  - [Read Operations](#read-operations)
  - [Async Enumeration](#async-enumeration)
  - [Multi-Consumer Streaming](#multi-consumer-streaming)
- [Configuration](#configuration)
  - [Adaptive Resizing](#adaptive-resizing)
  - [Eviction](#eviction)
- [Performance](#performance)
- [Thread Safety](#thread-safety)
- [Use Cases](#use-cases)
- [Gotchas / FAQ](#gotchas--faq)
- [Benchmarks](#benchmarks)
- [License](#license)

## Installation

```bash
dotnet add package Baubit.Caching
```

## Why?

### TL;DR

1. **Ordered IDs**: Chronologically sortable identifiers (e.g., GuidV7, int, long) eliminate separate timestamp fields
2. **Transparent tiering**: L1/L2 fallback is invisible to consumers
3. **Deletion-resilient iteration**: Removing entries mid-stream doesn't break enumeration
4. **Memory safety**: Automatic eviction behind slowest consumer prevents leaks
5. **Zero-latency streaming**: Consumers resume instantly when producers add entries (no polling)

### In-Depth

#### 1. Chronologically Ordered Identity Without Dual Fields

Event sourcing and audit logs need explicit time stamps for time-ordering:

```csharp
// ❌ Redundant: Separate ID + Timestamp fields
public record Event(TId Id, DateTime Timestamp, string Data); // TId: Guid, int, long, etc.
```

```csharp
// ✅ Efficient: Single sortable, time-ordered ID
public record Event(TId Id, string Data); // Id is naturally chronological
```

#### 2. Transparent Multi-Tier Cache

Multi-tier caches typically require clients to orchestrate lookups:

```csharp
// ❌ Complex: Client must orchestrate L1/L2 checks
var entry = l1Cache.Get(id) ?? l2Cache.Get(id);
```

`OrderedCache` provides automatic L1→L2 fallback with replenishment:

```csharp
// ✅ Transparent: Single call handles L1 miss + L2 lookup + replenish
cache.GetEntryOrDefault(id, out var entry); // Automatic tier management
```

#### 3. Resilient Iteration Despite Deletions

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

#### 4. Multi-Speed Consumer Memory Management

Multiple consumers reading at different speeds cause memory leaks:

```csharp
// ❌ Problem: Fast consumers read 1000 entries, slow consumer at entry 10
// → Cache needs to retain 990 entries
```

`OrderedCache` tracks all active enumerators and automatically evicts only entries that **all consumers** have passed:

```csharp
// ✅ Automatic: Evicts entries behind slowest consumer
var config = new Configuration { EvictAfterEveryX = 100 };
```

#### 5. Producer-Consumer Coordination

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
var enumerator = cache.GetFutureAsyncEnumerator(cancellationToken);
while (await enumerator.MoveNextAsync()) // yields immediately when producer adds entry
{
    Process(enumerator.Current);
}
```

**Key Benefits:**
- **Zero latency**: Consumers resume instantly when producers add entries
- **Zero CPU waste**: Consumers block efficiently (no spin loops)
- **Adaptive sizing**: Memory usage adjusts to production rate automatically
- **Extensible design**: Pluggable storage backends and metadata implementations (enabling distributed scenarios via `Baubit.Caching.Redis` - work in progress)

## Core Concepts

### Generic ID Support

`OrderedCache<TId, TValue>` supports generic identifier types. `TId` must be a struct implementing `IComparable<TId>` and `IEquatable<TId>`.

**Common ID types**: Guid (GuidV7, time-ordered), int, long, or custom structs for domain-specific ordering.

### Entry

An `IEntry<TId, TValue>` represents a cache entry:
- **Id** (`TId`): Entry identifier (e.g., Guid, int, long)
- **CreatedOnUTC** (`DateTime`): UTC timestamp when entry was added
- **Value** (`TValue`): The cached data

### Head and Tail

- **Head**: The oldest entry (first added)
- **Tail**: The newest entry (last added)

Operations like `GetFirstOrDefault` return the head; `GetLastOrDefault` returns the tail.

### GetNext Semantics

`GetNextOrDefault(id, out var next)` returns the entry **after** the given `id`. If `id` was deleted:
1. The metadata tracks the deleted node's position in the linked list
2. `GetNext` walks forward to find the next valid entry
3. Returns `false` if no valid entry exists after `id`

This ensures iteration continues even when entries are removed out-of-order.

### Enumerator Tracking and Eviction

- Each `IAsyncEnumerable` enumerator registers its current position with metadata
- Eviction (triggered every `EvictAfterEveryX` adds) removes entries **before** the slowest active enumerator
- Abandoned enumerators that are not disposed will pin memory indefinitely

**Rule:** Entries are evicted only when **all active enumerators** have advanced past them.

## Architecture

```text
+-------------------------------------------------------+
|           OrderedCache<TId, TValue>                   |
|                                                       |
|   +----------------+        +-------------------+     |
|   |    L1 Store    |  ───▶  |     L2 Store      |     |
|   |   (Bounded)    |        |   (Unbounded)     |     |
|   +----------------+        +-------------------+     |
|           │                         │                 |
|           └───────────┬─────────────┘                 |
|                       │                               |
|               +-------▲--------+                      |
|               |    Metadata    |                      |
|               |  (LinkedList)  |                      |
|               +----------------+                      |
+-------------------------------------------------------+
```

- **L1 Store**: Optional bounded in-memory cache (hot entries, configurable min/max capacity)
- **L2 Store**: Required **unbounded** backing store (holds all entries, generates GuidV7 IDs)
- **Metadata**: Ordered doubly-linked list of GuidV7 IDs with O(1) head/tail access
- **Concurrency**: `ReaderWriterLockSlim` for concurrent access (multiple readers, single writer)

**Flow:**
1. `Add` generates ID in L2, inserts to L2, then replenishes L1 if space available
2. `GetEntryOrDefault` checks L1 first, falls back to L2 on miss
3. Eviction removes entries from both L1 and L2 based on slowest enumerator position

## API Reference

<details>
<summary><strong>IOrderedCache&lt;TId, TValue&gt;</strong> (click to expand)</summary>

Generic interface supporting custom identifier types. `TId` must be a struct implementing `IComparable<TId>` and `IEquatable<TId>`.

```csharp
public interface IOrderedCache<TId, TValue> : IAsyncEnumerable<IEntry<TId, TValue>>, 
                                               IFutureAsyncEnumerable<IEntry<TId, TValue>>, 
                                               IDisposable 
    where TId : struct, IComparable<TId>, IEquatable<TId>
{
    long Count { get; }
    
    // Write Operations
    bool Add(TValue value, out IEntry<TId, TValue> entry);
    bool Update(TId id, TValue value);
    bool Remove(TId id, out IEntry<TId, TValue> entry);
    bool Clear();
    
    // Synchronous Read Operations
    bool GetEntryOrDefault(TId? id, out IEntry<TId, TValue> entry);
    bool GetNextOrDefault(TId? id, out IEntry<TId, TValue> entry);
    bool GetFirstOrDefault(out IEntry<TId, TValue> entry);
    bool GetFirstIdOrDefault(out TId? id);
    bool GetLastOrDefault(out IEntry<TId, TValue> entry);
    bool GetLastIdOrDefault(out TId? id);
    
    // Asynchronous Operations
    Task<IEntry<TId, TValue>> GetNextAsync(TId? id = null, CancellationToken cancellationToken = default);
    Task<IEntry<TId, TValue>> GetFutureFirstOrDefaultAsync(CancellationToken cancellationToken = default);
    
    // Typed Enumeration Operations
    IAsyncEnumerable<(TId, T)> EnumerateAsync<T>(CancellationToken cancellationToken = default) where T : TValue;
    IAsyncEnumerable<(TId, T)> EnumerateFutureAsync<T>(CancellationToken cancellationToken = default) where T : TValue;
    Task<bool> OnNextAsync<T>(Func<(TId, T), object, CancellationToken, Task<bool>> handler, object state, CancellationToken cancellationToken = default) where T : TValue;
}
```
</details>

<details>
<summary><strong>IEntry&lt;TId, TValue&gt;</strong> (click to expand)</summary>

Represents a cache entry with identifier, timestamp, and value.

```csharp
public interface IEntry<TId, TValue> where TId : struct, IComparable<TId>, IEquatable<TId>
{
    TId Id { get; }
    DateTime CreatedOnUTC { get; }
    TValue Value { get; }
}
```
</details>

<details>
<summary><strong>Configuration</strong> (click to expand)</summary>

Configuration class for cache behavior including adaptive resizing and eviction policies.

```csharp
public class Configuration : Baubit.Configuration.Configuration
{
    bool RunAdaptiveResizing { get; set; } = false;     // Enable L1 dynamic sizing
    int AdaptionWindowMS { get; set; } = 2_000;         // Resize evaluation interval (ms)
    int GrowStep { get; set; } = 64;                    // L1 growth increment
    int ShrinkStep { get; set; } = 32;                  // L1 shrink decrement
    double RoomRateLowerLimit { get; set; } = 1;        // Shrink threshold (entries/sec)
    double RoomRateUpperLimit { get; set; } = 5;        // Grow threshold (entries/sec)
    int EvictAfterEveryX { get; set; } = 100;           // Eviction frequency (adds)
}
```
</details>

## Usage

### Basic Setup

```csharp
using Baubit.Caching;
using Baubit.Caching.InMemory;
using Microsoft.Extensions.Logging;

var config = new Configuration { EvictAfterEveryX = 100 };
using var loggerFactory = LoggerFactory.Create(builder => { });
var identityGenerator = Baubit.Identity.IdentityGenerator.CreateNew();
var metadata = new Baubit.Caching.InMemory.Metadata<Guid>(config, loggerFactory);
var l1Store = new Baubit.Caching.InMemory.Store<Guid, string>(100, 1000, _ => null, loggerFactory); // Min: 100, Max: 1000, no ID gen
var l2Store = new Baubit.Caching.InMemory.Store<Guid, string>(null, null, lastId => 
{
    if (lastId.HasValue) identityGenerator.InitializeFrom(lastId.Value);
    return identityGenerator.GetNext();
}, loggerFactory); // Unbounded, generates GuidV7 IDs

using var cache = new OrderedCache<Guid, string>(
    config, l1Store, l2Store, metadata, loggerFactory
);
```

### Custom ID Types

To use custom identifier types, create Store instances with custom ID generation logic:

```csharp
using Baubit.Caching;
using Baubit.Caching.InMemory;
using Microsoft.Extensions.Logging;

// Usage with integer IDs
var config = new Configuration { EvictAfterEveryX = 100 };
using var loggerFactory = LoggerFactory.Create(builder => { });
var metadata = new Baubit.Caching.InMemory.Metadata<int>(config, loggerFactory);
var l1Store = new Baubit.Caching.InMemory.Store<int, string>(100, 1000, _ => null, loggerFactory); // No ID gen
var l2Store = new Baubit.Caching.InMemory.Store<int, string>(null, null, lastId => lastId.HasValue ? lastId.Value + 1 : 1, loggerFactory); // Sequential IDs

using var cache = new OrderedCache<int, string>(
    config, l1Store, l2Store, metadata, loggerFactory
);

cache.Add("value", out var entry);
Console.WriteLine(entry.Id);  // e.g., 1, 2, 3...
```

### Write Operations

```csharp
// Add entry (appends to tail)
cache.Add("value", out var entry);
Console.WriteLine(entry.Id);  // e.g., 01933c4a-4f2e-7b40-8000-123456789abc

// Update existing entry
cache.Update(entry.Id, "new_value");

// Remove entry (safe during iteration)
cache.Remove(entry.Id, out var removed);

// Clear all entries
cache.Clear();
```

### Read Operations

```csharp
// Direct access by ID (checks L1 → L2)
cache.GetEntryOrDefault(id, out var entry);

// Get head/tail
cache.GetFirstOrDefault(out var first);
cache.GetLastOrDefault(out var last);

// Sequential navigation (handles deleted nodes)
cache.GetNextOrDefault(currentId, out var next);

// Get IDs only (metadata-only operation)
cache.GetFirstIdOrDefault(out var firstId);
cache.GetLastIdOrDefault(out var lastId);
```

### Async Enumeration

```csharp
// Enumerate existing entries (from head to tail)
var enumerator = cache.GetAsyncEnumerator(cancellationToken);
while (await enumerator.MoveNextAsync())
{
    Console.WriteLine($"{enumerator.Current.Id}: {enumerator.Current.Value}");
}

// Wait for future entries (blocks until new entries arrive)
var enumerator = cache.GetFutureAsyncEnumerator(cancellationToken);
while (await enumerator.MoveNextAsync())
{
    Console.WriteLine($"New: {enumerator.Current.Value}");
}

// Named enumerators for tracking and deduplication
var consumer1 = cache.GetAsyncEnumerator("consumer-1");
var consumer2 = cache.GetFutureAsyncEnumerator("consumer-2");
// Duplicate id throws InvalidOperationException
// var duplicate = cache.GetAsyncEnumerator("consumer-1"); // throws!

// Wait for next entry after current position
var next = await cache.GetNextAsync(currentId, cancellationToken);

// Wait for first future entry (after current tail)
var future = await cache.GetFutureFirstOrDefaultAsync(cancellationToken);
```

### Multi-Consumer Streaming

```csharp
// Producer task
var producerCts = new CancellationTokenSource();
_ = Task.Run(async () =>
{
    while (!producerCts.Token.IsCancellationRequested)
    {
        cache.Add($"Event-{DateTime.UtcNow.Ticks}", out _);
        await Task.Delay(100);
    }
});

// Consumer 1 (fast)
var consumer1Cts = new CancellationTokenSource();
_ = Task.Run(async () =>
{
    var enumerator = cache.GetFutureAsyncEnumerator(consumer1Cts.Token);
    while (await enumerator.MoveNextAsync())
    {
        Console.WriteLine($"[Fast] {enumerator.Current.Value}");
        await Task.Delay(50); // Fast processing
    }
});

// Consumer 2 (slow)
var consumer2Cts = new CancellationTokenSource();
_ = Task.Run(async () =>
{
    var enumerator = cache.GetFutureAsyncEnumerator(consumer2Cts.Token);
    while (await enumerator.MoveNextAsync())
    {
        Console.WriteLine($"[Slow] {enumerator.Current.Value}");
        await Task.Delay(500); // Slow processing
    }
});

// Eviction will keep entries until consumer2 (slowest) has processed them
await Task.Delay(10_000);

// Cleanup: Cancel all tokens to dispose enumerators
consumer1Cts.Cancel();
consumer2Cts.Cancel();
producerCts.Cancel();
```

## Configuration

### Adaptive Resizing

When enabled, L1 capacity dynamically adjusts based on production rate:

```csharp
var config = new Configuration
{
    RunAdaptiveResizing = true,
    AdaptionWindowMS = 2_000,        // Sample every 2 seconds
    RoomRateUpperLimit = 5,          // Grow if >5 entries/sec
    RoomRateLowerLimit = 1,          // Shrink if <1 entry/sec
    GrowStep = 64,                   // L1 growth increment
    ShrinkStep = 32                  // L1 shrink decrement
};
```

**Behavior:**
- Measures entries added per second over `AdaptionWindowMS` intervals
- Grows L1 when rate exceeds `RoomRateUpperLimit`
- Shrinks L1 when rate falls below `RoomRateLowerLimit`
- Automatically replenishes L1 from L2 after shrink

### Eviction

Entries are evicted based on active enumerator positions:

```csharp
var config = new Configuration { EvictAfterEveryX = 100 };
```

**Behavior:**
- Every `EvictAfterEveryX` `Add` operations triggers eviction logic
- **With active enumerators**: Evicts entries **before** the slowest active enumerator's current position
- **Without active enumerators**: Evicts **all entries** up to and including the tail
- Prevents unbounded memory growth when consumers lag behind producers
- Enumerators that are not properly disposed will pin memory indefinitely

**Important**: Always dispose enumerators when done (use `using` statements or cancel `CancellationToken`) to allow eviction to proceed. Abandoned enumerators prevent memory cleanup.

**Example - Multiple Consumer Speeds:**
```csharp
// Fast consumer at entry 100, slow consumer at entry 10
// Eviction will only remove entries before entry 10
// This ensures all active consumers can still read their data
```

**Example - No Active Consumers:**
```csharp
// Cache with no active enumerators
cache.Add("item-1", out _);
// ... add 99 more items ...
cache.Add("item-100", out _); // Triggers eviction at threshold
// All 100 entries evicted since no consumers are reading
Assert.Equal(0, cache.Count);
```

**Configuration Options:**
```csharp
public class Configuration : Baubit.Configuration.Configuration
{
    bool RunAdaptiveResizing { get; set; } = false;  // Enable L1 dynamic sizing
    int AdaptionWindowMS { get; set; } = 2_000;      // Resize evaluation interval
    int GrowStep { get; set; } = 64;                 // L1 growth increment
    int ShrinkStep { get; set; } = 32;               // L1 shrink decrement
    double RoomRateLowerLimit { get; set; } = 1;     // Shrink threshold (entries/sec)
    double RoomRateUpperLimit { get; set; } = 5;     // Grow threshold (entries/sec)
    int EvictAfterEveryX { get; set; } = 100;        // Eviction frequency (adds)
}
```

## Performance

**System:** Intel Core Ultra 9 185H @ 2.50GHz, .NET 9.0.11  
**Date:** Nov 27, 2025

| Operation | Latency | Throughput | Allocations |
|-----------|---------|------------|-------------|
| `GetFirstOrDefault` | 68-74 ns | **13.4-14.6M ops/sec** | 0 B |
| `GetEntryOrDefault` | 98-124 ns | **8.1-10.2M ops/sec** | 0 B |
| `GetNextOrDefault` | 193-216 ns | **4.6-5.2M ops/sec** | 0 B |
| `Update` | 416-435 ns | **2.3-2.4M ops/sec** | 155 B |
| `Add` | 1,093-1,128 ns | **886K-915K ops/sec** | 256-288 B |

### Workload Performance

| Workload | Throughput |
|----------|------------|
| Read-Only | 4.6-14.6M ops/sec | 
| Write-Only (Update) | 2.3-2.4M ops/sec | 
| Write-Only (Add) | 886K-915K ops/sec | 
| Mixed (50/50 R/W) | 677K-742K ops/sec | 
| Mixed (80/20 R/W) | 461K-548K ops/sec | 

### vs. FusionCache

| Operation | Baubit | FusionCache | Winner |
|-----------|--------|-------------|--------|
| Read | 100-137 ns | 297-299 ns | **Baubit 2.2-3.0x faster** |
| Update | 129-134 ns | 432-459 ns | **Baubit 3.3-3.4x faster** |
| Add | 662-792 ns | 813-864 ns | **Baubit 1.0-1.3x faster** |
| Mixed 80/20 | 1,467-1,775 ns | 2,002-2,227 ns | **Baubit 1.3-1.4x faster** |
| Mixed 50/50 | 943-1,058 ns | 1,398-1,403 ns | **Baubit 1.3-1.5x faster** |

**Characteristics:**
- Zero allocations on reads
- O(1) lookups and head/tail access
- Faster than FusionCache across all comparable operations

See [Baubit.Caching.Benchmark/RESULTS.md](Baubit.Caching.Benchmark/RESULTS.md) for detailed benchmark data.

## Thread Safety

`OrderedCache<T>` is designed for concurrent access:
- Multiple readers can access the cache simultaneously.
- Writers (add/update/remove) are synchronized to prevent conflicting changes.

**Caveats:**
- Operations on the same entry (e.g., update) are not atomic. Consumers must handle potential conflicts.
- Enumerating events (`IAsyncEnumerable`) is safe even if new events are added concurrently.

## Use Cases

| Scenario | Why OrderedCache |
|----------|------------------|
| **Event Sourcing** | Maintains insertion order, async iteration |
| **Message Queues** | FIFO semantics with random access by ID |
| **Audit Logs** | Time-ordered entries with fast lookup |
| **Time-Series Cache** | GuidV7 provides chronological ordering |
| **Change Data Capture** | Stream processing with position tracking |

## Gotchas / FAQ

### Q: Can slow enumerators cause memory leaks?

**A:** Yes. Enumerators that are not disposed will pin memory indefinitely, preventing eviction of entries they haven't processed. Always:
- Use `using` with enumerators or ensure `DisposeAsync()` is called
- Cancel `CancellationToken` when consumers shut down
- Set appropriate `EvictAfterEveryX` to limit growth

**Note**: With no active enumerators, the cache automatically evicts all entries at the eviction threshold. This means if you're using the cache without enumerators (e.g., only `Add`/`Get`/`Remove` operations), entries will be cleared periodically unless you disable eviction by setting `EvictAfterEveryX` to a very high value (e.g., `int.MaxValue`).

### Q: Is it safe to remove entries during iteration?

**A:** Yes. `GetNextOrDefault` and `GetNextAsync` skip deleted nodes. If the current ID is removed, the next call finds the next valid entry. This is safe even with concurrent removals across multiple threads.

### Q: What happens if I remove an entry while an enumerator is at that position?

**A:** The metadata retains the deleted node's position in the linked list temporarily. `GetNext` walks forward to find the next valid entry. Once all enumerators advance past the deleted node, it's eligible for cleanup.

### Q: Can I use this as a distributed cache?

**A:** Not directly. `OrderedCache` is single-process. Future extensions planned to extend this project for distributed caching.

### Q: Why is L2 unbounded?

**A:** L2 is the source of truth for all entries. Bounding it would require eviction logic that conflicts with the guarantee that all entries are accessible by ID. Use eviction policies (via enumerator tracking) to manage memory instead.

### Q: What's the difference between `GetNextAsync` and `GetFutureAsyncEnumerator`?

**A:** 
- `GetNextAsync(id)`: Waits for the next entry after `id`. Returns immediately if it exists, blocks otherwise.
- `GetFutureAsyncEnumerator()`: Returns an `IAsyncEnumerable` starting from the current tail, yielding all future entries as they're added.

### Q: What are enumerator IDs and when should I use them?

**A:** Each enumerator has a unique `Id` property (auto-generated GUID by default). You can provide custom ids:
```csharp
var enumerator = cache.GetAsyncEnumerator("my-consumer");
```

**Benefits:**
- Prevents duplicate enumerators (throws `InvalidOperationException` if id already active)
- Enables tracking which consumer is processing which entries
- Useful for debugging and monitoring multiple consumers

**Note:** Enumerator ids are only unique while the enumerator is active. After disposal, the id can be reused.

## Benchmarks

```bash
cd Baubit.Caching.Benchmark
dotnet run -c Release
```

Results saved to `RESULTS.md` with ops/sec metrics for read/write/mixed scenarios.

## License

MIT License
