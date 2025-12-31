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
            await using var enumerator = cache.GetAsyncEnumerator(cts.Token);
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
            await using var enumerator = cache.GetAsyncEnumerator(cts.Token);

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
    }
}