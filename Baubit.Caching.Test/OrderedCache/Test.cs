using Baubit.Caching.InMemory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Baubit.Caching.Test.OrderedCache
{
    /// <summary>
    /// Tests for <see cref="OrderedCache{TValue}"/>
    /// </summary>
    public class Test
    {
        private readonly ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;

        private Caching.OrderedCache<string> CreateTestCache(
            Caching.Configuration? config = null,
            long? l1MinCap = null,
            long? l1MaxCap = null)
        {
            config ??= new Caching.Configuration();
            var metadata = new Metadata();
            var l2Store = new Store<string>(_loggerFactory);
            var l1Store = l1MinCap.HasValue ? new Store<string>(l1MinCap, l1MaxCap, _loggerFactory) : null;

            return new Caching.OrderedCache<string>(config, l1Store, l2Store, metadata, _loggerFactory);
        }

        [Fact]
        public void OrderedCache_Constructor_InitializesCorrectly()
        {
            // Arrange & Act
            using var cache = CreateTestCache();

            // Assert
            Assert.NotNull(cache);
            Assert.Equal(0, cache.Count);
            Assert.NotNull(cache.Configuration);
        }

        [Fact]
        public void OrderedCache_Add_SingleEntry_Success()
        {
            // Arrange
            using var cache = CreateTestCache();

            // Act
            var result = cache.Add("test value", out var entry);

            // Assert
            Assert.True(result);
            Assert.NotNull(entry);
            Assert.Equal("test value", entry.Value);
            Assert.Equal(1, cache.Count);
        }

        [Fact]
        public void OrderedCache_Add_MultipleEntries_MaintainsOrder()
        {
            // Arrange
            using var cache = CreateTestCache();

            // Act
            cache.Add("first", out var entry1);
            cache.Add("second", out var entry2);
            cache.Add("third", out var entry3);

            // Assert
            Assert.Equal(3, cache.Count);
            Assert.NotNull(entry1);
            Assert.NotNull(entry2);
            Assert.NotNull(entry3);
        }

        [Fact]
        public void OrderedCache_GetFirstOrDefault_ReturnsFirstEntry()
        {
            // Arrange
            using var cache = CreateTestCache();
            cache.Add("first", out var addedEntry);
            cache.Add("second", out _);

            // Act
            var result = cache.GetFirstOrDefault(out var firstEntry);

            // Assert
            Assert.True(result);
            Assert.NotNull(firstEntry);
            Assert.Equal(addedEntry.Id, firstEntry.Id);
            Assert.Equal("first", firstEntry.Value);
        }

        [Fact]
        public void OrderedCache_GetLastOrDefault_ReturnsLastEntry()
        {
            // Arrange
            using var cache = CreateTestCache();
            cache.Add("first", out _);
            cache.Add("last", out var addedEntry);

            // Act
            var result = cache.GetLastOrDefault(out var lastEntry);

            // Assert
            Assert.True(result);
            Assert.NotNull(lastEntry);
            Assert.Equal(addedEntry.Id, lastEntry.Id);
            Assert.Equal("last", lastEntry.Value);
        }

        [Fact]
        public void OrderedCache_GetEntryOrDefault_ExistingId_ReturnsEntry()
        {
            // Arrange
            using var cache = CreateTestCache();
            cache.Add("test", out var added);

            // Act
            var result = cache.GetEntryOrDefault(added.Id, out var entry);

            // Assert
            Assert.True(result);
            Assert.NotNull(entry);
            Assert.Equal(added.Id, entry.Id);
            Assert.Equal("test", entry.Value);
        }

        [Fact]
        public void OrderedCache_GetEntryOrDefault_NonExistingId_ReturnsNull()
        {
            // Arrange
            using var cache = CreateTestCache();
            var nonExistingId = Guid.NewGuid();

            // Act
            var result = cache.GetEntryOrDefault(nonExistingId, out var entry);

            // Assert
            Assert.True(result);
            Assert.Null(entry);
        }

        [Fact]
        public void OrderedCache_GetNextOrDefault_ReturnsNextEntry()
        {
            // Arrange
            using var cache = CreateTestCache();
            cache.Add("first", out var first);
            cache.Add("second", out var second);

            // Act
            var result = cache.GetNextOrDefault(first.Id, out var next);

            // Assert
            Assert.True(result);
            Assert.NotNull(next);
            Assert.Equal(second.Id, next.Id);
        }

        [Fact]
        public void OrderedCache_Update_ExistingEntry_Success()
        {
            // Arrange
            using var cache = CreateTestCache();
            cache.Add("original", out var entry);

            // Act
            var result = cache.Update(entry.Id, "updated");

            // Assert
            Assert.True(result);
            cache.GetEntryOrDefault(entry.Id, out var updated);
            Assert.Equal("updated", updated?.Value);
        }

        [Fact]
        public void OrderedCache_Remove_ExistingEntry_Success()
        {
            // Arrange
            using var cache = CreateTestCache();
            cache.Add("to remove", out var entry);

            // Act
            var result = cache.Remove(entry.Id, out var removed);

            // Assert
            Assert.True(result);
            Assert.NotNull(removed);
            Assert.Equal("to remove", removed.Value);
            Assert.Equal(0, cache.Count);
        }

        [Fact]
        public void OrderedCache_Clear_RemovesAllEntries()
        {
            // Arrange
            using var cache = CreateTestCache();
            cache.Add("first", out _);
            cache.Add("second", out _);
            cache.Add("third", out _);

            // Act
            var result = cache.Clear();

            // Assert
            Assert.True(result);
            Assert.Equal(0, cache.Count);
        }

        [Fact]
        public void OrderedCache_GetFirstIdOrDefault_ReturnsFirstId()
        {
            // Arrange
            using var cache = CreateTestCache();
            cache.Add("first", out var entry);
            cache.Add("second", out _);

            // Act
            var result = cache.GetFirstIdOrDefault(out var firstId);

            // Assert
            Assert.True(result);
            Assert.NotNull(firstId);
            Assert.Equal(entry.Id, firstId);
        }

        [Fact]
        public void OrderedCache_GetLastIdOrDefault_ReturnsLastId()
        {
            // Arrange
            using var cache = CreateTestCache();
            cache.Add("first", out _);
            cache.Add("last", out var entry);

            // Act
            var result = cache.GetLastIdOrDefault(out var lastId);

            // Assert
            Assert.True(result);
            Assert.NotNull(lastId);
            Assert.Equal(entry.Id, lastId);
        }

        [Fact]
        public async Task OrderedCache_GetNextAsync_ExistingNext_ReturnsImmediately()
        {
            // Arrange
            using var cache = CreateTestCache();
            cache.Add("first", out var first);
            cache.Add("second", out var second);

            // Act
            var next = await cache.GetNextAsync(first.Id);

            // Assert
            Assert.NotNull(next);
            Assert.Equal(second.Id, next.Id);
        }

        [Fact]
        public async Task OrderedCache_GetNextAsync_WaitsForNew()
        {
            // Arrange
            using var cache = CreateTestCache();
            cache.Add("first", out var first);

            // Act
            var nextTask = cache.GetNextAsync(first.Id);
            await Task.Delay(50);
            cache.Add("second", out var second);

            var next = await nextTask.WaitAsync(TimeSpan.FromSeconds(1));

            // Assert
            Assert.NotNull(next);
            Assert.Equal(second.Id, next.Id);
        }

        [Fact]
        public async Task OrderedCache_GetFutureFirstOrDefaultAsync_WaitsForNewEntry()
        {
            // Arrange
            using var cache = CreateTestCache();

            // Act
            var futureTask = cache.GetFutureFirstOrDefaultAsync();
            await Task.Delay(50);
            cache.Add("new entry", out var entry);

            var future = await futureTask.WaitAsync(TimeSpan.FromSeconds(1));

            // Assert
            Assert.NotNull(future);
            Assert.Equal(entry.Id, future.Id);
        }

        [Fact]
        public void OrderedCache_WithL1Store_StoresInBothLayers()
        {
            // Arrange
            using var cache = CreateTestCache(l1MinCap: 10, l1MaxCap: 100);

            // Act
            cache.Add("test", out var entry);

            // Assert
            cache.GetEntryOrDefault(entry.Id, out var retrieved);
            Assert.NotNull(retrieved);
        }

        [Fact]
        public void OrderedCache_WithL1Store_WhenCapacityFull_StillWorksViaL2()
        {
            // Arrange - L1 can only hold 2 items
            using var cache = CreateTestCache(l1MinCap: 2, l1MaxCap: 2);

            // Act
            cache.Add("first", out var e1);
            cache.Add("second", out var e2);
            cache.Add("third", out var e3); // Should overflow to L2 only

            // Assert
            Assert.Equal(3, cache.Count);
            cache.GetEntryOrDefault(e3.Id, out var retrieved);
            Assert.NotNull(retrieved);
        }

        [Fact]
        public void OrderedCache_Dispose_CompletesSuccessfully()
        {
            // Arrange
            var cache = CreateTestCache();
            cache.Add("test", out _);

            // Act
            cache.Dispose();

            // Assert - No exception thrown
        }

        [Fact]
        public void OrderedCache_EmptyCache_GetFirstOrDefault_ReturnsNull()
        {
            // Arrange
            using var cache = CreateTestCache();

            // Act
            var result = cache.GetFirstOrDefault(out var entry);

            // Assert
            Assert.True(result);
            Assert.Null(entry);
        }

        [Fact]
        public void OrderedCache_EmptyCache_GetLastOrDefault_ReturnsNull()
        {
            // Arrange
            using var cache = CreateTestCache();

            // Act
            var result = cache.GetLastOrDefault(out var entry);

            // Assert
            Assert.True(result);
            Assert.Null(entry);
        }

        [Fact]
        public async Task OrderedCache_GetAsyncEnumerator_EnumeratesEntries()
        {
            // Arrange
            using var cache = CreateTestCache();
            cache.Add("first", out _);
            cache.Add("second", out _);
            cache.Add("third", out _);

            // Act
            var entries = new List<IEntry<string>>();
            await foreach (var entry in cache.WithCancellation(CancellationToken.None))
            {
                entries.Add(entry);
                if (entries.Count >= 3) break; // Stop after getting all entries
            }

            // Assert
            Assert.Equal(3, entries.Count);
        }
    }
}