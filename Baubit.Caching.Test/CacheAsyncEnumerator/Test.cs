using Baubit.Caching.InMemory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;

namespace Baubit.Caching.Test.CacheAsyncEnumerator
{
    /// <summary>
    /// Tests for <see cref="CacheAsyncEnumerator{TValue}"/>
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
        public async Task CacheAsyncEnumerator_MoveNextAsync_EnumeratesEntries()
        {
            // Arrange
            using var cache = CreateTestCache();
            cache.Add("first", out _);
            cache.Add("second", out _);
            cache.Add("third", out _);

            // Act
            var entries = new List<IEntry<Guid, string>>();
            await using var enumerator = cache.GetAsyncEnumerator();

            for (int i = 0; i < 3; i++)
            {
                if (await enumerator.MoveNextAsync())
                {
                    entries.Add(enumerator.Current!);
                }
            }

            // Assert
            Assert.Equal(3, entries.Count);
            Assert.Equal("first", entries[0].Value);
            Assert.Equal("second", entries[1].Value);
            Assert.Equal("third", entries[2].Value);
        }

        [Fact]
        public async Task CacheAsyncEnumerator_Current_ReturnsCurrentEntry()
        {
            // Arrange
            using var cache = CreateTestCache();
            cache.Add("test", out var added);

            // Act
            await using var enumerator = cache.GetAsyncEnumerator();
            await enumerator.MoveNextAsync();
            var current = enumerator.Current;

            // Assert
            Assert.NotNull(current);
            Assert.Equal(added.Id, current.Id);
        }

        [Fact]
        public async Task CacheAsyncEnumerator_CurrentId_ReturnsCurrentId()
        {
            // Arrange
            using var cache = CreateTestCache();
            cache.Add("test", out var added);

            // Act
            var enumerator = (Caching.CacheAsyncEnumerator<Guid, string>)cache.GetAsyncEnumerator();
            await enumerator.MoveNextAsync();
            var currentId = enumerator.CurrentId;

            // Assert
            Assert.NotNull(currentId);
            Assert.Equal(added.Id, currentId);

            await enumerator.DisposeAsync();
        }

        [Fact]
        public async Task CacheAsyncEnumerator_WithCancellation_StopsWhenCancelled()
        {
            // Arrange
            using var cache = CreateTestCache();
            cache.Add("first", out _);
            var cts = new CancellationTokenSource();

            // Act
            await using var enumerator = cache.GetAsyncEnumerator(null, cts.Token);
            await enumerator.MoveNextAsync();
            cts.Cancel();
            var moveResult = await enumerator.MoveNextAsync();

            // Assert
            Assert.False(moveResult);
        }

        [Fact]
        public async Task CacheAsyncEnumerator_EmptyCache_WaitsForEntries()
        {
            // Arrange
            using var cache = CreateTestCache();
            var cts = new CancellationTokenSource(200);

            // Act
            await using var enumerator = cache.GetAsyncEnumerator(null, cts.Token);

            // Should wait since cache is empty
            var moveTask = enumerator.MoveNextAsync();
            await Task.Delay(50);
            cache.Add("new entry", out _);

            var moved = await moveTask;

            // Assert
            Assert.True(moved);
            Assert.NotNull(enumerator.Current);
        }

        [Fact]
        public async Task CacheAsyncEnumerator_DisposeAsync_CompletesSuccessfully()
        {
            // Arrange
            using var cache = CreateTestCache();
            cache.Add("test", out _);
            var enumerator = cache.GetAsyncEnumerator();

            // Act
            await enumerator.DisposeAsync();

            // Assert - No exception thrown
        }

        [Fact]
        public void CacheAsyncEnumerator_Id_WithProvidedId_ReturnsProvidedId()
        {
            // Arrange
            using var cache = CreateTestCache();
            var expectedId = "TestEnumerator123";

            // Act
            var enumerator = new Caching.CacheAsyncEnumerator<Guid, string>(
                cache, 
                null, 
                expectedId);

            // Assert
            Assert.Equal(expectedId, enumerator.Id);
        }

        [Fact]
        public void CacheAsyncEnumerator_Id_WithoutProvidedId_ReturnsGuid()
        {
            // Arrange
            using var cache = CreateTestCache();

            // Act
            var enumerator = new Caching.CacheAsyncEnumerator<Guid, string>(
                cache, 
                null);

            // Assert
            Assert.NotNull(enumerator.Id);
            Assert.NotEmpty(enumerator.Id);
            // Verify it's a valid GUID string
            Assert.True(Guid.TryParse(enumerator.Id, out _));
        }

        [Fact]
        public void CacheAsyncEnumerator_Id_WithEmptyString_ReturnsEmptyString()
        {
            // Arrange
            using var cache = CreateTestCache();

            // Act
            var enumerator = new Caching.CacheAsyncEnumerator<Guid, string>(
                cache, 
                null, 
                "");

            // Assert
            // Empty string is treated as a provided id (not null), so it should be returned as-is
            Assert.Equal("", enumerator.Id);
        }

        [Fact]
        public void CacheAsyncEnumerator_Id_ThroughCache_WithProvidedId_ReturnsProvidedId()
        {
            // Arrange
            using var cache = CreateTestCache();
            var expectedId = "CacheLevelEnumerator";

            // Act
            var enumerator = (Caching.CacheAsyncEnumerator<Guid, string>)cache.GetAsyncEnumerator(expectedId);

            // Assert
            Assert.Equal(expectedId, enumerator.Id);
        }

        [Fact]
        public void CacheAsyncEnumerator_Id_ThroughCache_WithoutProvidedId_ReturnsGuid()
        {
            // Arrange
            using var cache = CreateTestCache();

            // Act
            var enumerator = (Caching.CacheAsyncEnumerator<Guid, string>)cache.GetAsyncEnumerator();

            // Assert
            Assert.NotNull(enumerator.Id);
            Assert.NotEmpty(enumerator.Id);
            // Verify it's a valid GUID string
            Assert.True(Guid.TryParse(enumerator.Id, out _));
        }

        [Fact]
        public void CacheAsyncEnumerator_DuplicateId_ThrowsInvalidOperationException()
        {
            // Arrange
            using var cache = CreateTestCache();
            var duplicateId = "duplicate-enumerator";

            // Act - Create first enumerator with the id
            var enumerator1 = cache.GetAsyncEnumerator(duplicateId);

            // Assert - Attempting to create second enumerator with same id throws
            var exception = Assert.Throws<InvalidOperationException>(() => 
                cache.GetAsyncEnumerator(duplicateId));
            Assert.Contains(duplicateId, exception.Message);
        }

        [Fact]
        public async Task CacheAsyncEnumerator_DuplicateId_AfterDispose_AllowsReuse()
        {
            // Arrange
            using var cache = CreateTestCache();
            var reuseId = "reusable-enumerator";

            // Act - Create, use, and dispose first enumerator
            var enumerator1 = cache.GetAsyncEnumerator(reuseId);
            await enumerator1.DisposeAsync();

            // Assert - Can create new enumerator with same id after disposal
            var enumerator2 = cache.GetAsyncEnumerator(reuseId);
            Assert.Equal(reuseId, ((ICacheEnumerator<Guid>)enumerator2).Id);
            await enumerator2.DisposeAsync();
        }
    }
}