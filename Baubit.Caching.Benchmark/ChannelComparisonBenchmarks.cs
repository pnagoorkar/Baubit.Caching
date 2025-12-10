using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using Baubit.Caching.InMemory;
using Microsoft.Extensions.Logging.Abstractions;
using System.Threading.Channels;

namespace Baubit.Caching.Benchmark;

/// <summary>
/// Benchmarks comparing System.Threading.Channels.Channel&lt;T&gt; with OrderedCache&lt;T&gt;.
/// Focuses on producer-consumer scenarios: writing, reading, and combined throughput.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10, invocationCount: 10000)]
[RankColumn]
[CategoriesColumn]
public class ChannelComparisonBenchmarks
{
    private OrderedCache<string>? _cache;
    private Channel<string>? _channel;
    private readonly List<Guid> _cacheEntryIds = new();
    private int _readIndex = 0;
    private int _writeCounter = 0;
    private Guid? _sequentialReadCurrentId = null;

    [Params(1_000, 10_000)]
    public int ItemCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        // Setup OrderedCache
        var config = new Configuration
        {
            RunAdaptiveResizing = false,
            EvictAfterEveryX = int.MaxValue // Disable eviction for fair comparison
        };

        var metadata = new Metadata { Configuration = config };
        var l1Store = new Store<string>(ItemCount / 10, ItemCount / 10, NullLoggerFactory.Instance);
        var l2Store = new Store<string>(NullLoggerFactory.Instance);

        _cache = new OrderedCache<string>(
            config,
            l1Store,
            l2Store,
            metadata,
            NullLoggerFactory.Instance
        );

        // Setup Channel with bounded capacity matching cache size
        _channel = Channel.CreateBounded<string>(new BoundedChannelOptions(ItemCount * 2)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

        // Pre-populate both for read benchmarks
        _cacheEntryIds.Clear();
        for (int i = 0; i < ItemCount; i++)
        {
            var value = $"Value_{i}";
            
            // Populate cache
            if (_cache.Add(value, out var entry))
            {
                _cacheEntryIds.Add(entry.Id);
            }

            // Populate channel
            _channel.Writer.TryWrite(value);
        }
    }

    // ==================== WRITE BENCHMARKS ====================

    [Benchmark(Description = "OrderedCache: Write")]
    [BenchmarkCategory("Write")]
    public void OrderedCache_Write()
    {
        _cache!.Add($"Value_{_writeCounter++}", out _);
    }

    [Benchmark(Description = "Channel: Write")]
    [BenchmarkCategory("Write")]
    public void Channel_Write()
    {
        _channel!.Writer.TryWrite($"Value_{_writeCounter++}");
    }

    // ==================== READ BENCHMARKS ====================

    [Benchmark(Description = "OrderedCache: Read by ID")]
    [BenchmarkCategory("Read")]
    public void OrderedCache_Read()
    {
        var id = _cacheEntryIds[_readIndex++ % _cacheEntryIds.Count];
        _cache!.GetEntryOrDefault(id, out _);
    }

    [Benchmark(Description = "Channel: Read")]
    [BenchmarkCategory("Read")]
    public void Channel_Read()
    {
        if (_channel!.Reader.TryRead(out var value))
        {
            // Successfully read, write it back to maintain queue size
            _channel.Writer.TryWrite(value);
        }
    }

    // ==================== SEQUENTIAL READ BENCHMARKS ====================

    [Benchmark(Description = "OrderedCache: Sequential Read")]
    [BenchmarkCategory("Sequential")]
    public void OrderedCache_SequentialRead()
    {
        // Sequential forward iteration through the cache
        if (_sequentialReadCurrentId == null)
        {
            // Start from the first entry
            if (_cache!.GetFirstOrDefault(out var firstEntry) && firstEntry != null)
            {
                _sequentialReadCurrentId = firstEntry.Id;
            }
        }
        else
        {
            // Get next entry from current position
            if (_cache!.GetNextOrDefault(_sequentialReadCurrentId, out var nextEntry) && nextEntry != null)
            {
                _sequentialReadCurrentId = nextEntry.Id;
            }
            else
            {
                // Reached end, wrap around to start
                _sequentialReadCurrentId = null;
            }
        }
    }

    // ==================== MIXED WORKLOAD BENCHMARKS ====================

    [Benchmark(Description = "OrderedCache: 50% Read, 50% Write")]
    [BenchmarkCategory("Mixed")]
    public void OrderedCache_Mixed_50Read_50Write()
    {
        // 1 read
        var id = _cacheEntryIds[_readIndex++ % _cacheEntryIds.Count];
        _cache!.GetEntryOrDefault(id, out _);

        // 1 write
        _cache!.Add($"Mixed_{_writeCounter++}", out _);
    }

    [Benchmark(Description = "Channel: 50% Read, 50% Write")]
    [BenchmarkCategory("Mixed")]
    public void Channel_Mixed_50Read_50Write()
    {
        // 1 read - write back to maintain queue size for fair comparison
        if (_channel!.Reader.TryRead(out var value))
        {
            _channel.Writer.TryWrite(value);
        }

        // 1 write
        _channel!.Writer.TryWrite($"Mixed_{_writeCounter++}");
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _cache?.Dispose();
        _channel?.Writer.Complete();
    }
}
