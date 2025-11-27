using BenchmarkDotNet.Running;

namespace Baubit.Caching.Benchmark;

class Program
{
    static void Main(string[] args)
    {
        // Run both benchmark suites
        BenchmarkRunner.Run(new[] 
        { 
            typeof(OrderedCacheBenchmarks),
            typeof(FusionCacheComparisonBenchmarks)
        }, args: args);
    }
}
