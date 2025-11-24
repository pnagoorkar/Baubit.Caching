# Baubit.Caching Benchmarks

Performance benchmarks for `OrderedCache<TValue>` using [BenchmarkDotNet](https://benchmarkdotnet.org/).

## Focus: Throughput (Operations per Second)

This benchmark measures real-world throughput for:

1. **Read-Only Workloads**
   - `GetEntryOrDefault` - Direct access by ID
   - `GetFirstOrDefault` - Head access
   - `GetNextOrDefault` - Sequential navigation

2. **Write-Only Workloads**
   - `Add` - Adding new entries
   - `Update` - Updating existing entries

3. **Mixed Workloads**
   - `80% Read / 20% Write` - Read-heavy scenario
   - `50% Read / 50% Write` - Balanced scenario

## Running Benchmarks

```bash
cd Baubit.Caching.Benchmark
dotnet run -c Release
```

Results are saved to `BenchmarkDotNet.Artifacts/results/` in multiple formats (Markdown, HTML, CSV).

## Understanding Results

### Key Metrics

- **Mean**: Average time per operation
- **Ops/sec**: Operations per second (1,000,000,000 ns / Mean ns)
- **Allocated**: Memory allocated per operation
- **Gen0**: GC Generation 0 collections per 1000 operations

### Interpreting Throughput

- **> 10M ops/sec**: Excellent - suitable for high-frequency trading, real-time systems
- **1M - 10M ops/sec**: Very Good - suitable for most high-performance scenarios
- **100K - 1M ops/sec**: Good - suitable for standard applications
- **< 100K ops/sec**: Consider optimization

## Parameters

- **CacheSize**: 1,000 and 10,000 entries
- **L1 Capacity**: 10% of cache size (auto-configured)
- **Adaptive Resizing**: Disabled for consistent measurements
- **Eviction**: Disabled for pure performance testing

## Best Practices

1. **Close background applications** - Minimize system noise
2. **Run in Release mode** - Debug builds are 10-100x slower
3. **Run multiple times** - Verify consistency (5-10% variance is normal)
4. **Use production-like data** - Match your actual usage patterns

## Quick Commands

```bash
# Default run (all scenarios)
dotnet run -c Release

# Export to markdown only
dotnet run -c Release --exporters markdown

# Quick test (shorter run)
dotnet run -c Release --job short
