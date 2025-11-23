using BenchmarkDotNet.Running;

namespace Baubit.Caching.Benchmark;

class Program
{
    static void Main(string[] args)
    {
        BenchmarkRunner.Run<OrderedCacheBenchmarks>(args: args);
    }
}
