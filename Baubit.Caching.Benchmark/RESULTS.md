# OrderedCache Performance Results

**System:** Intel Core Ultra 9 185H @ 2.50GHz, Windows 11 (10.0.26200.7171)  
**Runtime:** .NET 9.0.11, X64 RyuJIT x86-64-v3  
**Date:** Nov 27, 2025

---

## FusionCache Comparison

### Read Operations (Direct Lookup by ID/Key)

| Library | Cache Size | Ops/Sec | Rank |
|---------|------------|---------|------|
| **Baubit** | 1,000 | **10.00M** | **1** |
| FusionCache | 1,000 | 3.34M | 3 |
| **Baubit** | 10,000 | **7.30M** | **2** |
| FusionCache | 10,000 | 3.37M | 3 |

**Baubit 2.2-3.0x faster**

### Update Operations (Modify Existing Entry)

| Library | Cache Size | Ops/Sec | Rank |
|---------|------------|---------|------|
| **Baubit** | 1,000 | **7.75M** | **2** |
| FusionCache | 1,000 | 2.31M | 4 |
| **Baubit** | 10,000 | **7.48M** | **2** |
| FusionCache | 10,000 | 2.18M | 4 |

**Baubit 3.3-3.4x faster**

### Write/Add Operations (New Entry Creation)

| Library | Cache Size | Ops/Sec | Rank |
|---------|------------|---------|------|
| **Baubit** | 1,000 | **1.51M** | **5** |
| FusionCache | 1,000 | 1.16M | 5 |
| **Baubit** | 10,000 | **1.26M** | **5** |
| FusionCache | 10,000 | 1.23M | 5 |

**Baubit 1.0-1.3x faster**

### Mixed Workload: 80% Read / 20% Write

| Library | Cache Size | Ops/Sec | Rank |
|---------|------------|---------|------|
| **Baubit** | 1,000 | **682K** | **6** |
| FusionCache | 1,000 | 500K | 7 |
| **Baubit** | 10,000 | **563K** | **7** |
| FusionCache | 10,000 | 449K | 7 |

**Baubit 1.3-1.4x faster**

### Mixed Workload: 50% Read / 50% Write

| Library | Cache Size | Ops/Sec | Rank |
|---------|------------|---------|------|
| **Baubit** | 1,000 | **1.06M** | **5** |
| FusionCache | 1,000 | 713K | 6 |
| **Baubit** | 10,000 | **945K** | **5** |
| FusionCache | 10,000 | 715K | 6 |

**Baubit 1.3-1.5x faster**

---

## Channel<T> Comparison

### Read Operations

| Library | Item Count | Ops/Sec | Rank |
|---------|-----------|---------|------|
| **Channel** | 1,000 | **20.63M** | **1** |
| OrderedCache | 1,000 | 6.01M | 4 |
| **Channel** | 10,000 | **20.78M** | **1** |
| OrderedCache | 10,000 | 5.24M | 5 |

**Channel 3.4-3.9x faster** - Channel<T> is optimized for sequential access in producer-consumer scenarios.

### Write Operations

| Library | Item Count | Ops/Sec | Rank |
|---------|-----------|---------|------|
| **Channel** | 1,000 | **14.22M** | **2** |
| OrderedCache | 1,000 | 364K | 7 |
| **Channel** | 10,000 | **14.47M** | **2** |
| OrderedCache | 10,000 | 392K | 7 |

**Channel 36.2-39.1x faster** - Channel<T> has minimal overhead for simple enqueue operations.

### Mixed Workload: 50% Read / 50% Write

| Library | Item Count | Ops/Sec | Rank |
|---------|-----------|---------|------|
| **Channel** | 1,000 | **7.96M** | **3** |
| OrderedCache | 1,000 | 319K | 7 |
| **Channel** | 10,000 | **7.51M** | **3** |
| OrderedCache | 10,000 | 308K | 7 |

**Channel 23.5-24.9x faster** - Channel<T> excels in pure producer-consumer patterns.

### Performance Trade-offs

**When to use Channel<T>:**
- Pure producer-consumer scenarios (FIFO queue)
- No need for random access by ID
- Minimal memory overhead is critical
- Sequential-only access pattern

**When to use OrderedCache<T>:**
- Need random access by ID (O(1) lookups)
- Time-ordered GuidV7 identifiers required
- Multi-consumer with different speeds
- Persistent storage and two-tier caching
- Deletion-resilient iteration

**Key Insight:** Channel<T> is 24-39x faster for sequential producer-consumer patterns, but OrderedCache<T> provides rich features like random access, ordered enumeration, and persistent storage that justify its overhead for event sourcing, audit logs, and CDC pipelines.

---

## OrderedCache Standalone Performance

### Read-Only Workloads

| Operation | Cache Size | Ops/Sec | Rank |
|-----------|------------|---------|------|
| GetFirstOrDefault | 1,000 | **14.60M** | **1** |
| GetFirstOrDefault | 10,000 | **13.44M** | **1** |
| GetEntryOrDefault | 1,000 | **10.24M** | **2** |
| GetEntryOrDefault | 10,000 | **8.08M** | **3** |
| GetNextOrDefault | 1,000 | **5.17M** | **4** |
| GetNextOrDefault | 10,000 | **4.63M** | **4** |

### Write-Only Workloads

| Operation | Cache Size | Ops/Sec | Rank |
|-----------|------------|---------|------|
| Update | 1,000 | **2.40M** | **5** |
| Update | 10,000 | **2.30M** | **5** |
| Add | 1,000 | **915K** | **6** |
| Add | 10,000 | **886K** | **6** |

### Mixed Workloads

| Workload | Cache Size | Ops/Sec | Rank |
|----------|------------|---------|------|
| 50% Read / 50% Write | 1,000 | **742K** | **7** |
| 50% Read / 50% Write | 10,000 | **677K** | **7** |
| 80% Read / 20% Write | 1,000 | **548K** | **8** |
| 80% Read / 20% Write | 10,000 | **461K** | **9** |

---

## Raw Benchmark Data

### vs. FusionCache

```
BenchmarkDotNet v0.15.6, Windows 11 (10.0.26200.7171)
Intel Core Ultra 9 185H 2.50GHz, 1 CPU, 22 logical and 16 physical cores
.NET SDK 10.0.100, .NET 9.0.11 (9.0.11, 9.0.1125.51716), X64 RyuJIT x86-64-v3

| Method                             | Categories | CacheSize | Mean        | Rank | Allocated |
|----------------------------------- |----------- |---------- |------------:|-----:|----------:|
| 'Baubit: Read by ID'               | Read       | 1000      |    99.98 ns |    1 |         - |
| 'FusionCache: Read by Key'         | Read       | 1000      |   299.45 ns |    3 |         - |
| 'Baubit: Read by ID'               | Read       | 10000     |   137.07 ns |    2 |         - |
| 'FusionCache: Read by Key'         | Read       | 10000     |   296.98 ns |    3 |         - |
| 'Baubit: Update Existing'          | Update     | 1000      |   129.05 ns |    2 |      99 B |
| 'FusionCache: Update Existing'     | Update     | 1000      |   432.28 ns |    4 |     224 B |
| 'Baubit: Update Existing'          | Update     | 10000     |   133.64 ns |    2 |      99 B |
| 'FusionCache: Update Existing'     | Update     | 10000     |   458.83 ns |    4 |     224 B |
| 'Baubit: Add New Entry'            | Write      | 1000      |   662.07 ns |    5 |     224 B |
| 'FusionCache: Set New Entry'       | Write      | 1000      |   863.58 ns |    5 |     328 B |
| 'Baubit: Add New Entry'            | Write      | 10000     |   791.91 ns |    5 |     224 B |
| 'FusionCache: Set New Entry'       | Write      | 10000     |   813.14 ns |    5 |     328 B |
| 'Baubit: 80% Read, 20% Write'      | Mixed      | 1000      | 1,466.29 ns |    6 |     192 B |
| 'FusionCache: 80% Read, 20% Write' | Mixed      | 1000      | 2,001.61 ns |    7 |     320 B |
| 'Baubit: 50% Read, 50% Write'      | Mixed      | 1000      |   943.41 ns |    5 |     192 B |
| 'FusionCache: 50% Read, 50% Write' | Mixed      | 1000      | 1,403.34 ns |    6 |     320 B |
| 'Baubit: 80% Read, 20% Write'      | Mixed      | 10000     | 1,774.83 ns |    7 |     216 B |
| 'FusionCache: 80% Read, 20% Write' | Mixed      | 10000     | 2,227.46 ns |    7 |     320 B |
| 'Baubit: 50% Read, 50% Write'      | Mixed      | 10000     | 1,058.04 ns |    5 |     192 B |
| 'FusionCache: 50% Read, 50% Write' | Mixed      | 10000     | 1,398.59 ns |    6 |     320 B |
```

