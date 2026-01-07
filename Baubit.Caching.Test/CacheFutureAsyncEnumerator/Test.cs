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
            var enumerator = (CacheFutureAsyncEnumerator<Guid, string>)cache.GetFutureAsyncEnumerator(cts.Token);
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
        public void CacheFutureAsyncEnumerator_Name_WithProvidedName_ReturnsProvidedName()
        {
            // Arrange
            using var cache = CreateTestCache();
            var expectedName = "FutureEnumerator456";

            // Act
            var enumerator = new CacheFutureAsyncEnumerator<Guid, string>(
                cache, 
                null, 
                default, 
                expectedName);

            // Assert
            Assert.Equal(expectedName, enumerator.Name);
        }

        [Fact]
        public void CacheFutureAsyncEnumerator_Name_WithoutProvidedName_ReturnsGuid()
        {
            // Arrange
            using var cache = CreateTestCache();

            // Act
            var enumerator = new CacheFutureAsyncEnumerator<Guid, string>(
                cache, 
                null, 
                default, 
                null);

            // Assert
            Assert.NotNull(enumerator.Name);
            Assert.NotEmpty(enumerator.Name);
            // Verify it's a valid GUID string
            Assert.True(Guid.TryParse(enumerator.Name, out _));
        }

        [Fact]
        public void CacheFutureAsyncEnumerator_Name_WithEmptyString_ReturnsEmptyString()
        {
            // Arrange
            using var cache = CreateTestCache();

            // Act
            var enumerator = new CacheFutureAsyncEnumerator<Guid, string>(
                cache, 
                null, 
                default, 
                "");

            // Assert
            // Empty string should be treated as provided name (not null), so it should be returned as-is
            Assert.Equal("", enumerator.Name);
        }

        [Fact]
        public void CacheFutureAsyncEnumerator_Name_ThroughCache_WithProvidedName_ReturnsProvidedName()
        {
            // Arrange
            using var cache = CreateTestCache();
            var expectedName = "FutureCacheLevelEnumerator";

            // Act
            var enumerator = (CacheFutureAsyncEnumerator<Guid, string>)cache.GetFutureAsyncEnumerator(expectedName);

            // Assert
            Assert.Equal(expectedName, enumerator.Name);
        }

        [Fact]
        public void CacheFutureAsyncEnumerator_Name_ThroughCache_WithoutProvidedName_ReturnsGuid()
        {
            // Arrange
            using var cache = CreateTestCache();

            // Act
            var enumerator = (CacheFutureAsyncEnumerator<Guid, string>)cache.GetFutureAsyncEnumerator();

            // Assert
            Assert.NotNull(enumerator.Name);
            Assert.NotEmpty(enumerator.Name);
            // Verify it's a valid GUID string
            Assert.True(Guid.TryParse(enumerator.Name, out _));
        }
    }
}