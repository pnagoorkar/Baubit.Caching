using Baubit.Caching.InMemory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;

namespace Baubit.Caching.Test.CacheFutureAsyncEnumerator
{
    /// <summary>
    /// Tests for <see cref="CacheFutureAsyncEnumerator{TValue}"/>
    /// </summary>
    public class Test
    {
        private readonly ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;

        private OrderedCache<Guid, string> CreateTestCache()
        {
            var config = new Caching.Configuration();
            var identityGenerator = Baubit.Identity.IdentityGenerator.CreateNew();
            var metadata = new Baubit.Caching.InMemory.Metadata<Guid>(config, NullLoggerFactory.Instance);
            var l2Store = new Baubit.Caching.InMemory.Store<Guid, string>(null, null, lastId => 
            {
                if (lastId.HasValue) identityGenerator.InitializeFrom(lastId.Value);
                return identityGenerator.GetNext();
            }, _loggerFactory);
            return new OrderedCache<Guid, string>(config, null, l2Store, metadata, _loggerFactory);
        }

        [Fact]
        public async Task CacheFutureAsyncEnumerator_SkipsExistingEntries()
        {
            // Arrange
            using var cache = CreateTestCache();
            cache.Add("existing1", out _);
            cache.Add("existing2", out _);

            // Act
            var enumerator = (CacheFutureAsyncEnumerator<Guid, string>)cache.GetFutureAsyncEnumerator();

            // Add new entry after enumerator creation
            await Task.Delay(50);
            cache.Add("new entry", out var newEntry);

            using var cts = new CancellationTokenSource(1000);
            var moved = await enumerator.MoveNextAsync().AsTask().WaitAsync(cts.Token);

            // Assert
            Assert.True(moved);
            Assert.NotNull(enumerator.Current);
            Assert.Equal(newEntry.Id, enumerator.Current!.Id);

            await enumerator.DisposeAsync();
        }

        [Fact]
        public async Task CacheFutureAsyncEnumerator_WaitsForNewEntries()
        {
            // Arrange
            using var cache = CreateTestCache();

            // Act
            var enumerator = (CacheFutureAsyncEnumerator<Guid, string>)cache.GetFutureAsyncEnumerator();
            var moveTask = enumerator.MoveNextAsync().AsTask();

            await Task.Delay(50);
            cache.Add("future entry", out var futureEntry);

            using var cts = new CancellationTokenSource(1000);
            var moved = await moveTask.WaitAsync(cts.Token);

            // Assert
            Assert.True(moved);
            Assert.NotNull(enumerator.Current);
            Assert.Equal(futureEntry.Id, enumerator.Current!.Id);

            await enumerator.DisposeAsync();
        }

        [Fact]
        public async Task CacheFutureAsyncEnumerator_CurrentId_ReturnsLastIdInitially()
        {
            // Arrange
            using var cache = CreateTestCache();
            cache.Add("existing", out var existing);

            // Act
            var enumerator = (CacheFutureAsyncEnumerator<Guid, string>)cache.GetFutureAsyncEnumerator();
            var initialCurrentId = enumerator.CurrentId;

            // Assert
            Assert.NotNull(initialCurrentId);
            Assert.Equal(existing.Id, initialCurrentId);

            await enumerator.DisposeAsync();
        }

        [Fact]
        public async Task CacheFutureAsyncEnumerator_WithCancellation_StopsWhenCancelled()
        {
            // Arrange
            using var cache = CreateTestCache();
            var cts = new CancellationTokenSource();

            // Act
            var enumerator = (CacheFutureAsyncEnumerator<Guid, string>)cache.GetFutureAsyncEnumerator(null, cts.Token);
            var moveTask = enumerator.MoveNextAsync().AsTask();

            await Task.Delay(50);
            cts.Cancel();

            // MoveNextAsync should complete with false when cancelled
            var moved = await moveTask;

            // Assert
            Assert.False(moved);

            await enumerator.DisposeAsync();
        }

        [Fact]
        public async Task CacheFutureAsyncEnumerator_MultipleNewEntries_EnumeratesAll()
        {
            // Arrange
            using var cache = CreateTestCache();
            cache.Add("existing", out _);

            // Act
            var enumerator = (CacheFutureAsyncEnumerator<Guid, string>)cache.GetFutureAsyncEnumerator();

            var entries = new List<IEntry<Guid, string>>();

            // Add entries and enumerate them
            cache.Add("future1", out _);
            if (await enumerator.MoveNextAsync())
                entries.Add(enumerator.Current!);

            cache.Add("future2", out _);
            if (await enumerator.MoveNextAsync())
                entries.Add(enumerator.Current!);

            cache.Add("future3", out _);
            if (await enumerator.MoveNextAsync())
                entries.Add(enumerator.Current!);

            // Assert
            Assert.Equal(3, entries.Count);
            Assert.Equal("future1", entries[0].Value);
            Assert.Equal("future2", entries[1].Value);
            Assert.Equal("future3", entries[2].Value);

            await enumerator.DisposeAsync();
        }

        [Fact]
        public async Task CacheFutureAsyncEnumerator_DisposeAsync_CompletesSuccessfully()
        {
            // Arrange
            using var cache = CreateTestCache();
            var enumerator = cache.GetFutureAsyncEnumerator();

            // Act
            await enumerator.DisposeAsync();

            // Assert - No exception thrown
        }

        [Fact]
        public void CacheFutureAsyncEnumerator_Id_WithProvidedId_ReturnsProvidedId()
        {
            // Arrange
            using var cache = CreateTestCache();
            var expectedId = "FutureEnumerator456";

            // Act
            var enumerator = new CacheFutureAsyncEnumerator<Guid, string>(
                cache, 
                null, 
                expectedId);

            // Assert
            Assert.Equal(expectedId, enumerator.Id);
        }

        [Fact]
        public void CacheFutureAsyncEnumerator_Id_WithoutProvidedId_ReturnsGuid()
        {
            // Arrange
            using var cache = CreateTestCache();

            // Act
            var enumerator = new CacheFutureAsyncEnumerator<Guid, string>(
                cache, 
                null);

            // Assert
            Assert.NotNull(enumerator.Id);
            Assert.NotEmpty(enumerator.Id);
            // Verify it's a valid GUID string
            Assert.True(Guid.TryParse(enumerator.Id, out _));
        }

        [Fact]
        public void CacheFutureAsyncEnumerator_Id_WithEmptyString_ReturnsEmptyString()
        {
            // Arrange
            using var cache = CreateTestCache();

            // Act
            var enumerator = new CacheFutureAsyncEnumerator<Guid, string>(
                cache, 
                null, 
                "");

            // Assert
            // Empty string should be treated as provided id (not null), so it should be returned as-is
            Assert.Equal("", enumerator.Id);
        }

        [Fact]
        public void CacheFutureAsyncEnumerator_Id_ThroughCache_WithProvidedId_ReturnsProvidedId()
        {
            // Arrange
            using var cache = CreateTestCache();
            var expectedId = "FutureCacheLevelEnumerator";

            // Act
            var enumerator = (CacheFutureAsyncEnumerator<Guid, string>)cache.GetFutureAsyncEnumerator(expectedId);

            // Assert
            Assert.Equal(expectedId, enumerator.Id);
        }

        [Fact]
        public void CacheFutureAsyncEnumerator_Id_ThroughCache_WithoutProvidedId_ReturnsGuid()
        {
            // Arrange
            using var cache = CreateTestCache();

            // Act
            var enumerator = (CacheFutureAsyncEnumerator<Guid, string>)cache.GetFutureAsyncEnumerator();

            // Assert
            Assert.NotNull(enumerator.Id);
            Assert.NotEmpty(enumerator.Id);
            // Verify it's a valid GUID string
            Assert.True(Guid.TryParse(enumerator.Id, out _));
        }

        [Fact]
        public void CacheFutureAsyncEnumerator_DuplicateId_ThrowsInvalidOperationException()
        {
            // Arrange
            using var cache = CreateTestCache();
            var duplicateId = "duplicate-future-enumerator";

            // Act - Create first enumerator with the id
            var enumerator1 = cache.GetFutureAsyncEnumerator(duplicateId);

            // Assert - Attempting to create second enumerator with same id throws
            var exception = Assert.Throws<InvalidOperationException>(() => 
                cache.GetFutureAsyncEnumerator(duplicateId));
            Assert.Contains(duplicateId, exception.Message);
        }

        [Fact]
        public async Task CacheFutureAsyncEnumerator_DuplicateId_AfterDispose_AllowsReuse()
        {
            // Arrange
            using var cache = CreateTestCache();
            var reuseId = "reusable-future-enumerator";

            // Act - Create, use, and dispose first enumerator
            var enumerator1 = cache.GetFutureAsyncEnumerator(reuseId);
            await enumerator1.DisposeAsync();

            // Assert - Can create new enumerator with same id after disposal
            var enumerator2 = cache.GetFutureAsyncEnumerator(reuseId);
            Assert.Equal(reuseId, ((ICacheEnumerator<Guid>)enumerator2).Id);
            await enumerator2.DisposeAsync();
        }
    }
}