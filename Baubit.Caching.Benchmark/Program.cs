using BenchmarkDotNet.Running;

namespace Baubit.Caching.Benchmark;

class Program
{
    static void Main(string[] args)
    {
        // Run all benchmark suites
        BenchmarkRunner.Run(new[]
        {
            typeof(OrderedCacheBenchmarks),
            typeof(FusionCacheComparisonBenchmarks),
            typeof(ChannelComparisonBenchmarks)
        }, args: args);
    }
}