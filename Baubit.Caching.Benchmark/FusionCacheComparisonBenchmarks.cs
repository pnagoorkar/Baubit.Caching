using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Configs;
using Baubit.Caching.InMemory;
using Microsoft.Extensions.Logging.Abstractions;
using ZiggyCreatures.Caching.Fusion;

namespace Baubit.Caching.Benchmark;

/// <summary>
/// Benchmarks comparing Baubit.Caching OrderedCache with ZiggyCreatures FusionCache.
/// Tests comparable operations for read, write, and mixed workloads.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10, invocationCount: 10000)]
[RankColumn]
[CategoriesColumn]
public class FusionCacheComparisonBenchmarks
{
    private OrderedCache<string>? _baubitCache;
    private FusionCache? _fusionCache;
    private readonly List<Guid> _baubitEntryIds = new();
    private readonly List<string> _fusionCacheKeys = new();
    private int _readIndex = 0;
    private int _writeCounter = 0;

    [Params(1_000, 10_000)]
    public int CacheSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        // Setup Baubit.Caching OrderedCache
        var config = new Configuration
        {
            RunAdaptiveResizing = false,
            EvictAfterEveryX = int.MaxValue
        };

        var metadata = new Metadata(config, Baubit.Identity.IdentityGenerator.CreateNew(), NullLoggerFactory.Instance);
        var l1Store = new InMemory.Store<string>(CacheSize / 10, CacheSize / 10, NullLoggerFactory.Instance);
        var l2Store = new InMemory.Store<string>(NullLoggerFactory.Instance);

        _baubitCache = new OrderedCache<string>(
            config,
            l1Store,
            l2Store,
            metadata,
            NullLoggerFactory.Instance
        );

        // Setup FusionCache
        _fusionCache = new FusionCache(new FusionCacheOptions
        {
            CacheName = "benchmark-cache",
            DefaultEntryOptions = new FusionCacheEntryOptions
            {
                Duration = TimeSpan.FromHours(1),
                IsFailSafeEnabled = false
            }
        });

        // Pre-populate both caches
        _baubitEntryIds.Clear();
        _fusionCacheKeys.Clear();

        for (int i = 0; i < CacheSize; i++)
        {
            var value = $"Value_{i}";
            var key = $"key_{i}";

            // Populate Baubit cache
            if (_baubitCache.Add(value, out var entry))
            {
                _baubitEntryIds.Add(entry.Id);
            }

            // Populate FusionCache
            _fusionCache.Set(key, value);
            _fusionCacheKeys.Add(key);
        }
    }

    // ==================== READ BENCHMARKS ====================

    [Benchmark(Description = "Baubit: Read by ID")]
    [BenchmarkCategory("Read")]
    public void Baubit_Read()
    {
        var id = _baubitEntryIds[_readIndex++ % _baubitEntryIds.Count];
        _baubitCache!.GetEntryOrDefault(id, out _);
    }

    [Benchmark(Description = "FusionCache: Read by Key")]
    [BenchmarkCategory("Read")]
    public void FusionCache_Read()
    {
        var key = _fusionCacheKeys[_readIndex++ % _fusionCacheKeys.Count];
        _fusionCache!.TryGet<string>(key);
    }

    // ==================== WRITE BENCHMARKS ====================

    [Benchmark(Description = "Baubit: Add New Entry")]
    [BenchmarkCategory("Write")]
    public void Baubit_Write()
    {
        _baubitCache!.Add($"NewValue_{_writeCounter++}", out _);
    }

    [Benchmark(Description = "FusionCache: Set New Entry")]
    [BenchmarkCategory("Write")]
    public void FusionCache_Write()
    {
        var counter = _writeCounter++;
        _fusionCache!.Set($"new_key_{counter}", $"NewValue_{counter}");
    }

    // ==================== UPDATE BENCHMARKS ====================

    [Benchmark(Description = "Baubit: Update Existing")]
    [BenchmarkCategory("Update")]
    public void Baubit_Update()
    {
        var id = _baubitEntryIds[_readIndex++ % _baubitEntryIds.Count];
        _baubitCache!.Update(id, $"Updated_{_writeCounter++}");
    }

    [Benchmark(Description = "FusionCache: Update Existing")]
    [BenchmarkCategory("Update")]
    public void FusionCache_Update()
    {
        var key = _fusionCacheKeys[_readIndex++ % _fusionCacheKeys.Count];
        _fusionCache!.Set(key, $"Updated_{_writeCounter++}");
    }

    // ==================== MIXED WORKLOAD BENCHMARKS ====================

    [Benchmark(Description = "Baubit: 80% Read, 20% Write")]
    [BenchmarkCategory("Mixed")]
    public void Baubit_Mixed_80Read_20Write()
    {
        // 4 reads
        for (int i = 0; i < 4; i++)
        {
            var id = _baubitEntryIds[_readIndex++ % _baubitEntryIds.Count];
            _baubitCache!.GetEntryOrDefault(id, out _);
        }

        // 1 write
        _baubitCache!.Add($"Mixed_{_writeCounter++}", out _);
    }

    [Benchmark(Description = "FusionCache: 80% Read, 20% Write")]
    [BenchmarkCategory("Mixed")]
    public void FusionCache_Mixed_80Read_20Write()
    {
        // 4 reads
        for (int i = 0; i < 4; i++)
        {
            var key = _fusionCacheKeys[_readIndex++ % _fusionCacheKeys.Count];
            _fusionCache!.TryGet<string>(key);
        }

        // 1 write
        var counter = _writeCounter++;
        _fusionCache!.Set($"mixed_key_{counter}", $"Mixed_{counter}");
    }

    [Benchmark(Description = "Baubit: 50% Read, 50% Write")]
    [BenchmarkCategory("Mixed")]
    public void Baubit_Mixed_50Read_50Write()
    {
        // 1 read
        var id = _baubitEntryIds[_readIndex++ % _baubitEntryIds.Count];
        _baubitCache!.GetEntryOrDefault(id, out _);

        // 1 write
        _baubitCache!.Add($"Mixed_{_writeCounter++}", out _);
    }

    [Benchmark(Description = "FusionCache: 50% Read, 50% Write")]
    [BenchmarkCategory("Mixed")]
    public void FusionCache_Mixed_50Read_50Write()
    {
        // 1 read
        var key = _fusionCacheKeys[_readIndex++ % _fusionCacheKeys.Count];
        _fusionCache!.TryGet<string>(key);

        // 1 write
        var counter50 = _writeCounter++;
        _fusionCache!.Set($"mixed_key_{counter50}", $"Mixed_{counter50}");
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _baubitCache?.Dispose();
        _fusionCache?.Dispose();
    }
}