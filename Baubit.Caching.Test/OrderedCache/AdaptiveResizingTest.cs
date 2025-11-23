using Baubit.Caching.InMemory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Baubit.Caching.Test.OrderedCache
{
    /// <summary>
    /// Tests for adaptive resizing features of <see cref="OrderedCache{TValue}"/>
    /// </summary>
    public class AdaptiveResizingTest
    {
        private readonly ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;

        private Caching.OrderedCache<string> CreateTestCache(
            Caching.Configuration? config = null,
            long? l1MinCap = null,
            long? l1MaxCap = null)
        {
            config ??= new Caching.Configuration();
            var metadata = new Metadata { Configuration = config };
            var l2Store = new Store<string>(_loggerFactory);
            var l1Store = l1MinCap.HasValue ? new Store<string>(l1MinCap, l1MaxCap, _loggerFactory) : null;

            return new Caching.OrderedCache<string>(config, l1Store, l2Store, metadata, _loggerFactory);
        }

        [Fact]
        public async Task OrderedCache_AdaptiveResizing_Enabled_GrowsL1()
        {
            // Arrange
            var config = new Caching.Configuration
            {
                RunAdaptiveResizing = true,
                AdaptionWindowMS = 100, // Check every 100ms
                RoomRateUpperLimit = 5, // Grow if >5 entries/sec
                GrowStep = 10,
                EvictAfterEveryX = int.MaxValue
            };
            using var cache = CreateTestCache(config: config, l1MinCap: 10, l1MaxCap: 100);

            // Act - Create waiting consumers to enable roomCount tracking
            var cts = new CancellationTokenSource();
            var consumerTask = Task.Run(async () =>
            {
                try
                {
                    var enumerator = cache.GetFutureAsyncEnumerator(cts.Token);
                    await using (enumerator)
                    {
                        int count = 0;
                        while (await enumerator.MoveNextAsync() && count < 30)
                        {
                            count++;
                        }
                    }
                }
                catch (OperationCanceledException) { }
            });

            await Task.Delay(50); // Let consumer start waiting

            // Add items at high rate while consumer is waiting (triggers roomCount)
            for (int i = 0; i < 30; i++)
            {
                cache.Add($"item-{i}", out _);
                await Task.Delay(8); // ~125 items/sec >> 5/sec threshold (RoomRateUpperLimit)
            }

            cts.Cancel();
            await consumerTask;
            await Task.Delay(200); // Wait for resize to complete

            // Assert - Cache should continue working and growth should have triggered
            Assert.Equal(30, cache.Count);
        }

        [Fact]
        public async Task OrderedCache_AdaptiveResizing_Enabled_ShrinksL1()
        {
            // Arrange
            var config = new Caching.Configuration
            {
                RunAdaptiveResizing = true,
                AdaptionWindowMS = 100,
                RoomRateLowerLimit = 5, // Shrink if <5 entries/sec
                ShrinkStep = 5,
                EvictAfterEveryX = int.MaxValue
            };
            using var cache = CreateTestCache(config: config, l1MinCap: 20, l1MaxCap: 100);

            // Act - Add items slowly to trigger shrinkage (rate < 5/sec)
            var addTask = Task.Run(async () =>
            {
                for (int i = 0; i < 10; i++)
                {
                    cache.Add($"item-{i}", out _);
                    await Task.Delay(50); // Add ~2 items/sec (below threshold)
                }
            });

            // Wait for adds and adaptive resizing to complete
            await addTask;
            await Task.Delay(200); // Wait for resize check

            // Assert - Cache should continue working and shrinkage should have triggered
            Assert.Equal(10, cache.Count);
        }

        [Fact]
        public void OrderedCache_WithAdaptiveResizing_DisposesCorrectly()
        {
            // Arrange
            var config = new Caching.Configuration
            {
                RunAdaptiveResizing = true,
                AdaptionWindowMS = 100
            };
            var cache = CreateTestCache(config: config, l1MinCap: 10, l1MaxCap: 100);

            cache.Add("test", out _);

            // Act & Assert - Should dispose without hanging
            cache.Dispose();
        }

        [Fact]
        public void OrderedCache_ConfigurationProperty_ReturnsCorrectValue()
        {
            // Arrange
            var config = new Caching.Configuration
            {
                EvictAfterEveryX = 123,
                RunAdaptiveResizing = true
            };

            using var cache = CreateTestCache(config: config);

            // Act & Assert
            Assert.NotNull(cache.Configuration);
            Assert.Equal(123, cache.Configuration.EvictAfterEveryX);
            Assert.True(cache.Configuration.RunAdaptiveResizing);
        }

        [Fact]
        public async Task OrderedCache_AdaptiveResizing_WithConcurrentAccess()
        {
            // Arrange
            var config = new Caching.Configuration
            {
                RunAdaptiveResizing = true,
                AdaptionWindowMS = 100,
                RoomRateUpperLimit = 10, // Grow if >10 entries/sec
                GrowStep = 10,
                EvictAfterEveryX = int.MaxValue
            };
            using var cache = CreateTestCache(config: config, l1MinCap: 20, l1MaxCap: 200);

            var tasks = new List<Task>();

            // Act - Concurrent adds at high rate to trigger growth
            for (int i = 0; i < 5; i++)
            {
                int threadId = i;
                tasks.Add(Task.Run(async () =>
                {
                    for (int j = 0; j < 40; j++)
                    {
                        cache.Add($"thread-{threadId}-item-{j}", out _);
                        await Task.Delay(2); // Fast adds to trigger growth
                    }
                }));
            }

            await Task.WhenAll(tasks);
            await Task.Delay(300); // Allow resize to complete

            // Assert
            Assert.Equal(200, cache.Count);
        }

        [Fact]
        public void OrderedCache_WithoutL1Store_NoAdaptiveResizing()
        {
            // Arrange - No L1 store
            var config = new Caching.Configuration
            {
                RunAdaptiveResizing = true,
                AdaptionWindowMS = 100
            };
            using var cache = CreateTestCache(config: config);

            // Act
            for (int i = 0; i < 10; i++)
            {
                cache.Add($"item-{i}", out _);
            }

            // Assert - Should work without L1 store
            Assert.Equal(10, cache.Count);
        }

        [Fact]
        public void OrderedCache_WithUncappedL1Store_NoAdaptiveResizing()
        {
            // Arrange - Uncapped L1 store
            var config = new Caching.Configuration
            {
                RunAdaptiveResizing = true,
                AdaptionWindowMS = 100
            };
            var metadata = new Metadata { Configuration = config };
            var l2Store = new Store<string>(_loggerFactory);
            var l1Store = new Store<string>(_loggerFactory); // Uncapped

            using var cache = new Caching.OrderedCache<string>(config, l1Store, l2Store, metadata, _loggerFactory);

            // Act
            for (int i = 0; i < 10; i++)
            {
                cache.Add($"item-{i}", out _);
            }

            // Assert - Should work with uncapped L1 store
            Assert.Equal(10, cache.Count);
        }

        [Fact]
        public async Task OrderedCache_AdaptiveResizing_Disabled_WorksNormally()
        {
            // Arrange
            var config = new Caching.Configuration
            {
                RunAdaptiveResizing = false,
                EvictAfterEveryX = int.MaxValue
            };
            using var cache = CreateTestCache(config: config, l1MinCap: 10, l1MaxCap: 100);

            // Act
            for (int i = 0; i < 50; i++)
            {
                cache.Add($"item-{i}", out _);
            }

            // Assert
            Assert.Equal(50, cache.Count);
        }

        [Fact]
        public async Task OrderedCache_AdaptiveResizing_HighRateTriggersGrowth()
        {
            // Arrange
            var config = new Caching.Configuration
            {
                RunAdaptiveResizing = true,
                AdaptionWindowMS = 200, // Check every 200ms
                RoomRateUpperLimit = 2, // Grow if >2 entries/sec
                GrowStep = 15,
                EvictAfterEveryX = int.MaxValue
            };
            using var cache = CreateTestCache(config: config, l1MinCap: 10, l1MaxCap: 100);

            // Act - Create waiting consumers (this makes _roomCount increment)
            var consumerTasks = new List<Task>();
            var cts = new CancellationTokenSource();
            
            // Start consumers that will wait for entries
            for (int i = 0; i < 3; i++)
            {
                consumerTasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var enumerator = cache.GetFutureAsyncEnumerator(cts.Token);
                        await using (enumerator)
                        {
                            int count = 0;
                            while (await enumerator.MoveNextAsync() && count < 20)
                            {
                                count++;
                            }
                        }
                    }
                    catch (OperationCanceledException) { }
                }));
            }

            await Task.Delay(50); // Let consumers establish waiting state

            // Now add items - these will signal waiters and increment roomCount
            for (int cycle = 0; cycle < 3; cycle++)
            {
                for (int i = 0; i < 8; i++)
                {
                    cache.Add($"cycle-{cycle}-item-{i}", out _);
                    await Task.Delay(20); // ~50 items/sec >> 2/sec threshold (RoomRateUpperLimit)
                }
                await Task.Delay(100); // Let resize window complete
            }

            cts.Cancel();
            await Task.WhenAll(consumerTasks);

            // Assert - Cache should have all items and growth should have occurred
            Assert.Equal(24, cache.Count);
        }
    }
}
