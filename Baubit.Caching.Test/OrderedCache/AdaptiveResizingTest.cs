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
                AdaptionWindowMS = 200,
                RoomRateUpperLimit = 2, // Grow if >2 entries/sec
                GrowStep = 10,
                EvictAfterEveryX = int.MaxValue
            };
            using var cache = CreateTestCache(config: config, l1MinCap: 10, l1MaxCap: 100);

            // Act - Add items rapidly to trigger growth
            for (int i = 0; i < 10; i++)
            {
                cache.Add($"item-{i}", out _);
            }

            // Wait for adaptive resizing to run
            await Task.Delay(500);

            // Add more items
            for (int i = 10; i < 20; i++)
            {
                cache.Add($"item-{i}", out _);
            }

            // Assert - Cache should continue working
            Assert.Equal(20, cache.Count);
        }

        [Fact]
        public async Task OrderedCache_AdaptiveResizing_Enabled_ShrinksL1()
        {
            // Arrange
            var config = new Caching.Configuration
            {
                RunAdaptiveResizing = true,
                AdaptionWindowMS = 200,
                RoomRateLowerLimit = 10, // Shrink if <10 entries/sec (will be below this)
                ShrinkStep = 5,
                EvictAfterEveryX = int.MaxValue
            };
            using var cache = CreateTestCache(config: config, l1MinCap: 20, l1MaxCap: 100);

            // Act - Add a few items
            for (int i = 0; i < 5; i++)
            {
                cache.Add($"item-{i}", out _);
            }

            // Wait for adaptive resizing to potentially shrink
            await Task.Delay(500);

            // Add more items to ensure cache still works
            for (int i = 5; i < 10; i++)
            {
                cache.Add($"item-{i}", out _);
            }

            // Assert - Cache should continue working
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
                AdaptionWindowMS = 150,
                RoomRateUpperLimit = 5,
                GrowStep = 10,
                EvictAfterEveryX = int.MaxValue
            };
            using var cache = CreateTestCache(config: config, l1MinCap: 20, l1MaxCap: 200);

            var tasks = new List<Task>();

            // Act - Concurrent adds while adaptive resizing is running
            for (int i = 0; i < 5; i++)
            {
                int threadId = i;
                tasks.Add(Task.Run(async () =>
                {
                    for (int j = 0; j < 20; j++)
                    {
                        cache.Add($"thread-{threadId}-item-{j}", out _);
                        await Task.Delay(10);
                    }
                }));
            }

            await Task.WhenAll(tasks);

            // Assert
            Assert.Equal(100, cache.Count);
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
        public void OrderedCache_AdaptiveResizing_Disabled_WorksNormally()
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
    }
}
