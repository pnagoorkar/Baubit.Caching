using BenchmarkDotNet.Attributes;
using Baubit.Caching.InMemory;
using Microsoft.Extensions.Logging.Abstractions;

namespace Baubit.Caching.Benchmark;

/// <summary>
/// Benchmarks OrderedCache throughput for read-only, write-only, and mixed workloads.
/// Reports operations per second for practical performance assessment.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10, invocationCount: 10000)]
public class OrderedCacheBenchmarks
{
    private OrderedCache<string>? _cache;
    private readonly List<Guid> _entryIds = new();
    private int _readIndex = 0;

    [Params(1_000, 10_000)]
    public int CacheSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var config = new Configuration
        {
            RunAdaptiveResizing = false,
            EvictAfterEveryX = int.MaxValue
        };

        var metadata = new Metadata { Configuration = config };
        var l1Store = new Store<string>(CacheSize / 10, CacheSize / 10, NullLoggerFactory.Instance);
        var l2Store = new Store<string>(NullLoggerFactory.Instance);

        _cache = new OrderedCache<string>(
            config,
            l1Store,
            l2Store,
            metadata,
            NullLoggerFactory.Instance
        );

        // Pre-populate for read tests
        _entryIds.Clear();
        for (int i = 0; i < CacheSize; i++)
        {
            if (_cache.Add($"Value_{i}", out var entry))
            {
                _entryIds.Add(entry.Id);
            }
        }
    }

    [Benchmark(Description = "Read-Only: GetEntryOrDefault")]
    public void ReadOnly_Get()
    {
        var id = _entryIds[_readIndex++ % _entryIds.Count];
        _cache!.GetEntryOrDefault(id, out _);
    }

    [Benchmark(Description = "Read-Only: GetFirstOrDefault")]
    public void ReadOnly_GetFirst()
    {
        _cache!.GetFirstOrDefault(out _);
    }

    [Benchmark(Description = "Read-Only: GetNextOrDefault")]
    public void ReadOnly_GetNext()
    {
        var id = _entryIds[_readIndex++ % _entryIds.Count];
        _cache!.GetNextOrDefault(id, out _);
    }

    [Benchmark(Description = "Write-Only: Add")]
    public void WriteOnly_Add()
    {
        _cache!.Add($"NewValue_{Guid.NewGuid()}", out _);
    }

    [Benchmark(Description = "Write-Only: Update")]
    public void WriteOnly_Update()
    {
        var id = _entryIds[_readIndex++ % _entryIds.Count];
        _cache!.Update(id, $"Updated_{Guid.NewGuid()}");
    }

    [Benchmark(Description = "Mixed: 80% Read, 20% Write")]
    public void Mixed_80Read_20Write()
    {
        // 4 reads
        for (int i = 0; i < 4; i++)
        {
            var id = _entryIds[_readIndex++ % _entryIds.Count];
            _cache!.GetEntryOrDefault(id, out _);
        }

        // 1 write
        _cache!.Add($"Mixed_{Guid.NewGuid()}", out _);
    }

    [Benchmark(Description = "Mixed: 50% Read, 50% Write")]
    public void Mixed_50Read_50Write()
    {
        // 1 read
        var id = _entryIds[_readIndex++ % _entryIds.Count];
        _cache!.GetEntryOrDefault(id, out _);

        // 1 write
        _cache!.Add($"Mixed_{Guid.NewGuid()}", out _);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _cache?.Dispose();
    }
}
