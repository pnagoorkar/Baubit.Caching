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
`OrderedCache<T>` is an append-ordered, time-sortable cache. Each entry gets a GuidV7 (time-ordered ID) by default, or you can use custom ID types. You can:
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

**Built-in specialization**: `OrderedCache<TValue>` uses Guid (GuidV7, time-ordered) as the identifier type.

**Custom ID types**: Implement `OrderedCache<TId, TValue>` with int, long, or custom structs for domain-specific ordering.

### Entry

An `IEntry<TValue>` (or `IEntry<TId, TValue>`) represents a cache entry:
- **Id** (`Guid` by default, or custom `TId`): Entry identifier
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
|                  OrderedCache<TValue>                 |
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
public interface IOrderedCache<TId, TValue> : IAsyncEnumerable<IEntry<TId, TValue>>, IFutureAsyncEnumerable<IEntry<TId, TValue>>, IDisposable 
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
<summary><strong>IStore&lt;TId, TValue&gt;</strong> (click to expand)</summary>

Storage backend interface for cache entries. Implementations provide L1/L2 storage layers.

```csharp
public interface IStore<TId, TValue> : IDisposable where TId : struct, IComparable<TId>, IEquatable<TId>
{
    // Capacity Management
    bool Uncapped { get; }
    long? CurrentCapacity { get; }
    bool HasCapacity { get; }
    long? MaxCapacity { get; set; }
    long? MinCapacity { get; set; }
    long? TargetCapacity { get; }
    TId? LastAddedId { get; }
    
    // Write Operations
    bool Add(IEntry<TId, TValue> entry);
    bool Add(TId id, TValue value, out IEntry<TId, TValue> entry);
    bool Add(TValue value, out IEntry<TId, TValue> entry);
    bool Update(IEntry<TId, TValue> entry);
    bool Update(TId id, TValue value);
    bool Remove(TId id, out IEntry<TId, TValue> entry);
    
    // Read Operations
    bool GetEntryOrDefault(TId? id, out IEntry<TId, TValue> entry);
    bool GetValueOrDefault(TId? id, out TValue value);
    bool GetCount(out long count);
    
    // Capacity Operations
    bool AddCapacity(int additionalCapacity);
    bool CutCapacity(int cap);
}
```
</details>

<details>
<summary><strong>IMetadata&lt;TId&gt;</strong> (click to expand)</summary>

Metadata interface tracking entry ordering and linked list navigation.

```csharp
public interface IMetadata<TId> : IDisposable where TId : struct, IComparable<TId>, IEquatable<TId>
{
    long Count { get; }
    TId? HeadId { get; }
    TId? TailId { get; }
    
    // Write Operations
    bool AddTail(TId id);
    bool Remove(TId id);
    long ResetRoomCount();
    
    // Read Operations
    bool ContainsKey(TId id);
    bool GetNextId(TId? id, out TId? nextId);
    bool GetIdsThrough(TId id, out IEnumerable<TId> ids);
    
    // Asynchronous Operations
    Task<TId> GetNextIdAsync(TId? id, CancellationToken cancellationToken);
}
```
</details>

<details>
<summary><strong>IFutureAsyncEnumerable&lt;T&gt;</strong> (click to expand)</summary>

Extends `IAsyncEnumerable<T>` to support waiting for future entries.

```csharp
public interface IFutureAsyncEnumerable<T> : IAsyncEnumerable<T>
{
    IAsyncEnumerator<T> GetFutureAsyncEnumerator(CancellationToken cancellationToken = default);
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
