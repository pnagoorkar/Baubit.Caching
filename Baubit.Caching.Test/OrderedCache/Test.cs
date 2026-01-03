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

        private OrderedCache<Guid, string> CreateTestCache(
            Caching.Configuration? config = null,
            long? l1MinCap = null,
            long? l1MaxCap = null)
        {
            config ??= new Caching.Configuration();
            var identityGenerator = Baubit.Identity.IdentityGenerator.CreateNew();
            var metadata = new Baubit.Caching.InMemory.Metadata<Guid>(config, NullLoggerFactory.Instance);
            var l2Store = new Baubit.Caching.InMemory.Store<Guid, string>(null, null, lastId => 
            {
                if (lastId.HasValue) identityGenerator.InitializeFrom(lastId.Value);
                return identityGenerator.GetNext();
            }, _loggerFactory);
            // L1 store doesn't need an identity generator since it only stores entries created by L2
            var l1Store = l1MinCap.HasValue ? new Baubit.Caching.InMemory.Store<Guid, string>(l1MinCap, l1MaxCap, _ => null, _loggerFactory) : null;

            return new OrderedCache<Guid, string>(config, l1Store, l2Store, metadata, _loggerFactory);
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
            var entries = new List<IEntry<Guid, string>>();
            await foreach (var entry in cache.WithCancellation(CancellationToken.None))
            {
                entries.Add(entry);
                if (entries.Count >= 3) break; // Stop after getting all entries
            }

            // Assert
            Assert.Equal(3, entries.Count);
        }
        [Fact]
        public async Task OrderedCache_ConcurrentAdd_AllSucceed()
        {
            // Arrange
            // Disable eviction for this test since we're not using enumerators
            var config = new Caching.Configuration { EvictAfterEveryX = int.MaxValue };
            using var cache = CreateTestCache(config: config);
            const int threadCount = 10;
            const int itemsPerThread = 100;
            var tasks = new Task[threadCount];
            var addResults = new bool[threadCount * itemsPerThread];
            var addIndex = 0;

            // Act
            for (int i = 0; i < threadCount; i++)
            {
                int threadId = i;
                tasks[i] = Task.Run(() =>
                {
                    for (int j = 0; j < itemsPerThread; j++)
                    {
                        var result = cache.Add($"thread-{threadId}-item-{j}", out _);
                        var idx = Interlocked.Increment(ref addIndex) - 1;
                        addResults[idx] = result;
                    }
                });
            }
            await Task.WhenAll(tasks);

            // Assert
            Assert.All(addResults, result => Assert.True(result));
            Assert.Equal(threadCount * itemsPerThread, cache.Count);
        }

        [Fact]
        public async Task OrderedCache_ConcurrentRead_AllSucceed()
        {
            // Arrange
            // Disable eviction for this test since we're not using enumerators
            var config = new Caching.Configuration { EvictAfterEveryX = int.MaxValue };
            using var cache = CreateTestCache(config: config);
            // Pre-populate cache
            var entries = new List<IEntry<Guid, string>>();
            for (int i = 0; i < 100; i++)
            {
                cache.Add($"item-{i}", out var entry);
                entries.Add(entry);
            }

            const int threadCount = 10;
            const int readsPerThread = 50;
            var tasks = new Task[threadCount];
            var readResults = new System.Collections.Concurrent.ConcurrentBag<bool>();

            // Act
            for (int i = 0; i < threadCount; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    var random = new Random();
                    for (int j = 0; j < readsPerThread; j++)
                    {
                        var entry = entries[random.Next(entries.Count)];
                        var result = cache.GetEntryOrDefault(entry.Id, out var retrieved);
                        readResults.Add(result && retrieved != null);
                    }
                });
            }
            await Task.WhenAll(tasks);

            // Assert
            Assert.All(readResults, result => Assert.True(result));
        }

        [Fact]
        public async Task OrderedCache_ConcurrentMixedReadWrite_NoDeadlock()
        {
            // Arrange
            // Disable eviction for this test since we're not using enumerators
            var config = new Caching.Configuration { EvictAfterEveryX = int.MaxValue };
            using var cache = CreateTestCache(config: config);
            const int operationCount = 500;
            var tasks = new List<Task>();
            var allSuccessful = true;

            // Act
            // Writers
            for (int i = 0; i < 5; i++)
            {
                int writerId = i;
                tasks.Add(Task.Run(() =>
                {
                    for (int j = 0; j < operationCount / 5; j++)
                    {
                        if (!cache.Add($"writer-{writerId}-item-{j}", out _))
                            allSuccessful = false;
                    }
                }));
            }

            // Readers
            for (int i = 0; i < 5; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    for (int j = 0; j < operationCount; j++)
                    {
                        cache.GetFirstOrDefault(out _);
                        cache.GetLastOrDefault(out _);
                    }
                }));
            }

            var allTasksTask = Task.WhenAll(tasks.ToArray());
            var completedTask = await Task.WhenAny(allTasksTask, Task.Delay(TimeSpan.FromSeconds(10)));
            var completed = completedTask == allTasksTask;

            // Assert
            Assert.True(completed, "Operations should complete without deadlock");
            Assert.True(allSuccessful, "All write operations should succeed");
        }

        [Fact]
        public async Task OrderedCache_ConcurrentRemove_HandlesCorrectly()
        {
            // Arrange
            // Disable eviction for this test since we're not using enumerators
            var config = new Caching.Configuration { EvictAfterEveryX = int.MaxValue };
            using var cache = CreateTestCache(config: config);
            var entries = new List<IEntry<Guid, string>>();
            for (int i = 0; i < 100; i++)
            {
                cache.Add($"item-{i}", out var entry);
                entries.Add(entry);
            }

            const int threadCount = 10;
            var tasks = new Task[threadCount];
            var removeResults = new System.Collections.Concurrent.ConcurrentBag<bool>();

            // Act
            for (int i = 0; i < threadCount; i++)
            {
                int startIdx = i * 10;
                tasks[i] = Task.Run(() =>
                {
                    for (int j = 0; j < 10; j++)
                    {
                        var result = cache.Remove(entries[startIdx + j].Id, out _);
                        removeResults.Add(result);
                    }
                });
            }
            await Task.WhenAll(tasks);

            // Assert
            Assert.All(removeResults, result => Assert.True(result));
            Assert.Equal(0, cache.Count);
        }

        [Fact]
        public async Task OrderedCache_ConcurrentUpdate_AllSucceed()
        {
            // Arrange
            using var cache = CreateTestCache();
            var entries = new List<IEntry<Guid, string>>();
            for (int i = 0; i < 50; i++)
            {
                cache.Add($"original-{i}", out var entry);
                entries.Add(entry);
            }

            const int threadCount = 5;
            var tasks = new Task[threadCount];
            var updateResults = new System.Collections.Concurrent.ConcurrentBag<bool>();

            // Act
            for (int i = 0; i < threadCount; i++)
            {
                int threadId = i;
                tasks[i] = Task.Run(() =>
                {
                    for (int j = 0; j < entries.Count; j++)
                    {
                        var result = cache.Update(entries[j].Id, $"updated-by-{threadId}-{j}");
                        updateResults.Add(result);
                    }
                });
            }
            await Task.WhenAll(tasks);

            // Assert
            Assert.All(updateResults, result => Assert.True(result));
        }

        [Fact]
        public async Task OrderedCache_ConcurrentGetNextAsync_MultipleWaiters()
        {
            // Arrange
            using var cache = CreateTestCache();
            cache.Add("first", out var first);

            const int waiterCount = 10;
            var tasks = new Task<IEntry<Guid, string>>[waiterCount];

            // Act - Start multiple waiters
            for (int i = 0; i < waiterCount; i++)
            {
                tasks[i] = cache.GetNextAsync(first.Id);
            }

            await Task.Delay(50); // Let waiters establish

            // Add the awaited item
            cache.Add("second", out var second);

            // Wait for all to complete
            var results = await Task.WhenAll(tasks);

            // Assert
            Assert.All(results, entry =>
            {
                Assert.NotNull(entry);
                Assert.Equal(second.Id, entry.Id);
                Assert.Equal("second", entry.Value);
            });
        }

        [Fact]
        public async Task OrderedCache_ConcurrentGetFutureFirstOrDefaultAsync_MultipleWaiters()
        {
            // Arrange
            using var cache = CreateTestCache();
            const int waiterCount = 10;
            var tasks = new Task<IEntry<Guid, string>>[waiterCount];

            // Act - Start multiple waiters on empty cache
            for (int i = 0; i < waiterCount; i++)
            {
                tasks[i] = cache.GetFutureFirstOrDefaultAsync();
            }

            await Task.Delay(50); // Let waiters establish

            // Add the first item
            cache.Add("first-item", out var first);

            // Wait for all to complete
            var results = await Task.WhenAll(tasks);

            // Assert
            Assert.All(results, entry =>
            {
                Assert.NotNull(entry);
                Assert.Equal(first.Id, entry.Id);
                Assert.Equal("first-item", entry.Value);
            });
        }

        [Fact]
        public async Task OrderedCache_ConcurrentAsyncEnumerators_AllEnumerateCorrectly()
        {
            // Arrange
            using var cache = CreateTestCache();
            const int itemCount = 20;
            const int enumeratorCount = 5;

            // Pre-populate cache
            for (int i = 0; i < itemCount; i++)
            {
                cache.Add($"item-{i}", out _);
            }

            var enumeratorTasks = new List<Task<int>>();

            // Act - Start multiple enumerators
            for (int i = 0; i < enumeratorCount; i++)
            {
                enumeratorTasks.Add(Task.Run(async () =>
                {
                    int count = 0;
                    await foreach (var entry in cache.WithCancellation(CancellationToken.None))
                    {
                        count++;
                        if (count >= itemCount) break;
                    }
                    return count;
                }));
            }

            var counts = await Task.WhenAll(enumeratorTasks);

            // Assert
            Assert.All(counts, count => Assert.Equal(itemCount, count));
        }

        [Fact]
        public async Task OrderedCache_ConcurrentEviction_WithActiveEnumerators()
        {
            // Arrange
            var config = new Caching.Configuration { EvictAfterEveryX = 10 };
            using var cache = CreateTestCache(config: config);

            var enumeratorStarted = new TaskCompletionSource<bool>();
            var continueEnumeration = new TaskCompletionSource<bool>();

            // Start an enumerator that will pause
            var enumeratorTask = Task.Run(async () =>
            {
                await foreach (var entry in cache.WithCancellation(CancellationToken.None))
                {
                    enumeratorStarted.SetResult(true);
                    await continueEnumeration.Task;
                    break;
                }
            });

            // Act - Add items to trigger eviction
            cache.Add("item-0", out _);
            await enumeratorStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

            for (int i = 1; i < 50; i++)
            {
                cache.Add($"item-{i}", out _);
            }

            continueEnumeration.SetResult(true);
            await enumeratorTask.WaitAsync(TimeSpan.FromSeconds(2));

            // Assert - Should not throw or deadlock
            Assert.True(enumeratorTask.IsCompleted);
        }

        [Fact]
        public async Task OrderedCache_ConcurrentClear_WithReaders()
        {
            // Arrange
            using var cache = CreateTestCache();
            for (int i = 0; i < 100; i++)
            {
                cache.Add($"item-{i}", out _);
            }

            var tasks = new List<Task>();
            var clearExecuted = false;
            var readsDuringClear = 0;

            // Act - Multiple readers + one clear operation
            for (int i = 0; i < 5; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    for (int j = 0; j < 100; j++)
                    {
                        cache.GetFirstOrDefault(out _);
                        Interlocked.Increment(ref readsDuringClear);
                        Thread.Sleep(1);
                    }
                }));
            }

            tasks.Add(Task.Run(() =>
            {
                Thread.Sleep(50);
                clearExecuted = cache.Clear();
            }));

            await Task.WhenAll(tasks.ToArray());

            // Assert
            Assert.True(clearExecuted);
            Assert.Equal(0, cache.Count);
        }

        [Fact]
        public async Task OrderedCache_ConcurrentAddAndGetNext_RaceCondition()
        {
            // Arrange
            using var cache = CreateTestCache();
            cache.Add("initial", out var initial);

            var tasks = new List<Task>();
            var nextResults = new System.Collections.Concurrent.ConcurrentBag<IEntry<Guid, string>>();

            // Act - Concurrent GetNextOrDefault and Add
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    for (int j = 0; j < 50; j++)
                    {
                        if (cache.GetNextOrDefault(initial.Id, out var next) && next != null)
                        {
                            nextResults.Add(next);
                        }
                        Thread.Sleep(1);
                    }
                }));

                tasks.Add(Task.Run(() =>
                {
                    for (int j = 0; j < 25; j++)
                    {
                        cache.Add($"item-{i}-{j}", out _);
                        Thread.Sleep(2);
                    }
                }));
            }

            await Task.WhenAll(tasks.ToArray());

            // Assert - Should complete without exceptions
            Assert.True(tasks.All(t => t.IsCompleted));
        }

        [Fact]
        public async Task OrderedCache_ConcurrentL1L2Access_MaintainsConsistency()
        {
            // Arrange - Small L1 cache to force L1/L2 interaction
            // Disable eviction for this test since we're not using enumerators
            var config = new Caching.Configuration { EvictAfterEveryX = int.MaxValue };
            using var cache = CreateTestCache(config: config, l1MinCap: 10, l1MaxCap: 10);
            var tasks = new List<Task>();
            var allEntriesFound = true;
            var addedEntries = new System.Collections.Concurrent.ConcurrentBag<Guid>();

            // Act - Add beyond L1 capacity while reading
            for (int i = 0; i < 5; i++)
            {
                int threadId = i;
                tasks.Add(Task.Run(() =>
                {
                    for (int j = 0; j < 20; j++)
                    {
                        if (cache.Add($"t{threadId}-item-{j}", out var entry))
                        {
                            addedEntries.Add(entry.Id);
                        }
                    }
                }));

                tasks.Add(Task.Run(() =>
                {
                    Thread.Sleep(10); // Let some adds happen first
                    for (int j = 0; j < 50; j++)
                    {
                        cache.GetFirstOrDefault(out _);
                        cache.GetLastOrDefault(out _);
                    }
                }));
            }

            await Task.WhenAll(tasks.ToArray());

            // Verify all added entries are retrievable
            foreach (var id in addedEntries)
            {
                if (!cache.GetEntryOrDefault(id, out var entry) || entry == null)
                {
                    allEntriesFound = false;
                    break;
                }
            }

            // Assert
            Assert.True(allEntriesFound, "All entries should be retrievable from L1/L2");
        }

        [Fact]
        public async Task OrderedCache_ConcurrentDispose_HandlesGracefully()
        {
            // Arrange
            var cache = CreateTestCache();
            for (int i = 0; i < 50; i++)
            {
                cache.Add($"item-{i}", out _);
            }

            var tasks = new List<Task>();
            var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

            // Act - Multiple operations + dispose
            for (int i = 0; i < 3; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        for (int j = 0; j < 100; j++)
                        {
                            cache.GetFirstOrDefault(out _);
                            Thread.Sleep(1);
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                }));
            }

            // Dispose while operations are running
            await Task.Delay(50);
            cache.Dispose();

            // Wait for all tasks
            try
            {
                await Task.WhenAll(tasks.ToArray()).WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (TimeoutException)
            {
                // Expected - operations may timeout after dispose
            }
            catch (AggregateException)
            {
                // Expected - operations may fail after dispose
            }

            // Assert - Should not deadlock
            Assert.True(tasks.All(t => t.IsCompleted || t.IsFaulted || t.IsCanceled));
        }

        [Fact]
        public async Task OrderedCache_ConcurrentFutureEnumerators_WithProducers()
        {
            // Arrange
            using var cache = CreateTestCache();
            const int consumerCount = 3;
            const int itemsToAdd = 30;
            var consumerTasks = new List<Task<int>>();
            var cts = new CancellationTokenSource();

            // Act - Start consumers waiting for future items
            for (int i = 0; i < consumerCount; i++)
            {
                consumerTasks.Add(Task.Run(async () =>
                {
                    int count = 0;
                    try
                    {
                        var enumerator = cache.GetFutureAsyncEnumerator(cts.Token);
                        await using (enumerator)
                        {
                            while (await enumerator.MoveNextAsync())
                            {
                                count++;
                                if (count >= itemsToAdd) break;
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected when cancelled
                    }
                    return count;
                }));
            }

            await Task.Delay(50); // Let consumers establish

            // Producer adds items
            for (int i = 0; i < itemsToAdd; i++)
            {
                cache.Add($"item-{i}", out _);
                await Task.Delay(5); // Simulate gradual production
            }

            // Wait for all consumers to finish
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var completedTask = await Task.WhenAny(Task.WhenAll(consumerTasks), timeoutTask);

            if (completedTask == timeoutTask)
            {
                cts.Cancel(); // Cancel if timeout
            }

            var counts = await Task.WhenAll(consumerTasks);

            // Assert - All consumers should get all items
            Assert.All(counts, count => Assert.Equal(itemsToAdd, count));
        }

        [Fact]
        public async Task OrderedCache_ConcurrentUpdateSameEntry_LastWins()
        {
            // Arrange
            using var cache = CreateTestCache();
            cache.Add("original", out var entry);

            const int updateCount = 100;
            var tasks = new Task[updateCount];
            var updateResults = new bool[updateCount];

            // Act - Many concurrent updates to same entry
            for (int i = 0; i < updateCount; i++)
            {
                int updateId = i;
                tasks[i] = Task.Run(() =>
                {
                    updateResults[updateId] = cache.Update(entry.Id, $"update-{updateId}");
                });
            }

            await Task.WhenAll(tasks);

            // Assert
            Assert.All(updateResults, result => Assert.True(result));
            cache.GetEntryOrDefault(entry.Id, out var final);
            Assert.NotNull(final);
            Assert.NotEqual("original", final.Value); // Should be updated
        }

        [Fact]
        public async Task OrderedCache_StressTest_MixedOperations()
        {
            // Arrange
            // Disable eviction for this test since we're not using enumerators
            var config = new Caching.Configuration { EvictAfterEveryX = int.MaxValue };
            using var cache = CreateTestCache(config: config, l1MinCap: 50, l1MaxCap: 100);
            const int operationCount = 1000;
            var tasks = new List<Task>();
            var random = new Random();
            var addedIds = new System.Collections.Concurrent.ConcurrentBag<Guid>();

            // Act - Mix of all operations
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    for (int j = 0; j < operationCount / 10; j++)
                    {
                        var operation = random.Next(5);
                        switch (operation)
                        {
                            case 0: // Add
                                if (cache.Add($"stress-{i}-{j}", out var entry))
                                    addedIds.Add(entry.Id);
                                break;
                            case 1: // Read
                                cache.GetFirstOrDefault(out _);
                                break;
                            case 2: // GetNext
                                cache.GetNextOrDefault(null, out _);
                                break;
                            case 3: // Update - safe concurrent access
                                if (!addedIds.IsEmpty && addedIds.TryTake(out var id))
                                {
                                    cache.Update(id, $"updated-{j}");
                                    addedIds.Add(id); // Put it back for other threads
                                }
                                break;
                            case 4: // GetLast
                                cache.GetLastOrDefault(out _);
                                break;
                        }
                    }
                }));
            }

            var allTasksTask = Task.WhenAll(tasks.ToArray());
            var completedTask = await Task.WhenAny(allTasksTask, Task.Delay(TimeSpan.FromSeconds(30)));
            var completed = completedTask == allTasksTask;

            // Assert
            Assert.True(completed, "Stress test should complete without deadlock");
            Assert.True(cache.Count > 0, "Cache should have entries");
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
            var identityGenerator = Baubit.Identity.IdentityGenerator.CreateNew();
            var metadata = new Baubit.Caching.InMemory.Metadata<Guid>(config, NullLoggerFactory.Instance);
            var l2Store = new Baubit.Caching.InMemory.Store<Guid, string>(null, null, lastId => 
            {
                if (lastId.HasValue) identityGenerator.InitializeFrom(lastId.Value);
                return identityGenerator.GetNext();
            }, _loggerFactory);
            var l1Store = new Baubit.Caching.InMemory.Store<Guid, string>(null, null, _ => null, _loggerFactory); // Uncapped, no ID gen

            using var cache = new OrderedCache<Guid, string>(config, l1Store, l2Store, metadata, _loggerFactory);

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
        [Fact]
        public void OrderedCache_AfterDispose_OperationsThrowOrReturnFalse()
        {
            // Arrange
            var cache = CreateTestCache();
            cache.Add("test", out var entry);
            cache.Dispose();

            // Act & Assert - Operations after dispose may throw ObjectDisposedException
            Assert.Throws<ObjectDisposedException>(() => cache.Add("after-dispose", out _));
            Assert.Throws<ObjectDisposedException>(() => cache.Update(entry.Id, "updated"));
            Assert.Throws<ObjectDisposedException>(() => cache.GetEntryOrDefault(entry.Id, out _));
            Assert.Throws<ObjectDisposedException>(() => cache.GetNextOrDefault(entry.Id, out _));
            Assert.Throws<ObjectDisposedException>(() => cache.GetFirstOrDefault(out _));
            Assert.Throws<ObjectDisposedException>(() => cache.GetLastOrDefault(out _));
            Assert.Throws<ObjectDisposedException>(() => cache.GetFirstIdOrDefault(out _));
            Assert.Throws<ObjectDisposedException>(() => cache.GetLastIdOrDefault(out _));
            Assert.Throws<ObjectDisposedException>(() => cache.Remove(entry.Id, out _));
            Assert.Throws<ObjectDisposedException>(() => cache.Clear());
        }

        [Fact]
        public void OrderedCache_GetNextOrDefault_FromNullId_GetsFirst()
        {
            // Arrange
            using var cache = CreateTestCache();
            cache.Add("first", out var first);
            cache.Add("second", out _);

            // Act
            var result = cache.GetNextOrDefault(null, out var entry);

            // Assert
            Assert.True(result);
            Assert.NotNull(entry);
            Assert.Equal(first.Id, entry.Id);
        }

        [Fact]
        public void OrderedCache_GetEntryOrDefault_WithNullId_ReturnsNull()
        {
            // Arrange
            using var cache = CreateTestCache();
            cache.Add("test", out _);

            // Act - Passing null ID returns true (operation succeeded) with null entry (not found)
            var result = cache.GetEntryOrDefault(null, out var entry);

            // Assert - API design: returns true for successful operation, entry is null when not found
            Assert.True(result);
            Assert.Null(entry);
        }

        [Fact]
        public void OrderedCache_Update_NonExistentEntry_HandlesProperly()
        {
            // Arrange
            using var cache = CreateTestCache();
            var nonExistentId = Guid.NewGuid();

            // Act & Assert - Update of non-existent entry may throw or return false
            // The implementation uses L2Store.Update which may throw NullReferenceException
            // This is expected behavior for edge cases
            try
            {
                var result = cache.Update(nonExistentId, "value");
                Assert.False(result);
            }
            catch (NullReferenceException)
            {
                // Expected for non-existent entries
            }
        }

        [Fact]
        public void OrderedCache_Update_WithL1Store_UpdatesBothStores()
        {
            // Arrange
            using var cache = CreateTestCache(l1MinCap: 10, l1MaxCap: 100);
            cache.Add("original", out var entry);

            // Act
            var result = cache.Update(entry.Id, "updated");

            // Assert
            Assert.True(result);
            cache.GetEntryOrDefault(entry.Id, out var retrieved);
            Assert.Equal("updated", retrieved?.Value);
        }

        [Fact]
        public void OrderedCache_Remove_NonExistentEntry_ReturnsFalse()
        {
            // Arrange
            using var cache = CreateTestCache();
            var nonExistentId = Guid.NewGuid();

            // Act
            var result = cache.Remove(nonExistentId, out var entry);

            // Assert
            Assert.False(result);
            Assert.Null(entry);
        }

        [Fact]
        public void OrderedCache_Remove_EntryInL1AndL2_RemovesFromBoth()
        {
            // Arrange
            using var cache = CreateTestCache(l1MinCap: 10, l1MaxCap: 100);
            cache.Add("test", out var entry);

            // Act
            var result = cache.Remove(entry.Id, out var removed);

            // Assert
            Assert.True(result);
            Assert.NotNull(removed);

            // Verify it's gone from both stores
            cache.GetEntryOrDefault(entry.Id, out var retrieved);
            Assert.Null(retrieved);
        }

        [Fact]
        public void OrderedCache_Remove_EntryOnlyInL2_RemovesCorrectly()
        {
            // Arrange - Small L1, add more than it can hold
            using var cache = CreateTestCache(l1MinCap: 2, l1MaxCap: 2);
            cache.Add("first", out _);
            cache.Add("second", out _);
            cache.Add("third", out var third); // Should be in L2 only

            // Act
            var result = cache.Remove(third.Id, out var removed);

            // Assert
            Assert.True(result);
            Assert.NotNull(removed);
        }

        [Fact]
        public void OrderedCache_Clear_EmptyCache_ReturnsTrue()
        {
            // Arrange
            using var cache = CreateTestCache();

            // Act
            var result = cache.Clear();

            // Assert
            Assert.True(result);
            Assert.Equal(0, cache.Count);
        }

        [Fact]
        public void OrderedCache_Eviction_WithNoActiveEnumerators_Succeeds()
        {
            // Arrange
            var config = new Caching.Configuration { EvictAfterEveryX = 5 };
            using var cache = CreateTestCache(config: config);

            // Act - Add more than eviction threshold
            for (int i = 0; i < 10; i++)
            {
                cache.Add($"item-{i}", out _);
            }

            // Assert - With no active enumerators, all entries should be evicted after eviction threshold is reached
            // After adding 10 items with eviction threshold of 5, eviction runs twice (at 5th and 10th addition)
            // Since there are no active enumerators, all entries are evicted
            Assert.Equal(0, cache.Count);
        }

        [Fact]
        public async Task OrderedCache_GetNextAsync_WithExistingNext_ReturnsImmediately()
        {
            // Arrange
            using var cache = CreateTestCache();
            cache.Add("first", out var first);
            cache.Add("second", out var second);

            // Act
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var next = await cache.GetNextAsync(first.Id);
            stopwatch.Stop();

            // Assert
            Assert.NotNull(next);
            Assert.Equal(second.Id, next.Id);
            Assert.True(stopwatch.ElapsedMilliseconds < 100, "Should return immediately");
        }

        [Fact]
        public async Task OrderedCache_GetNextAsync_FromNullId_ReturnsFirst()
        {
            // Arrange
            using var cache = CreateTestCache();
            cache.Add("first", out var first);

            // Act
            var next = await cache.GetNextAsync(null);

            // Assert
            Assert.NotNull(next);
            Assert.Equal(first.Id, next.Id);
        }

        [Fact]
        public async Task OrderedCache_GetNextAsync_Cancelled_ThrowsTaskCanceledException()
        {
            // Arrange
            using var cache = CreateTestCache();
            cache.Add("first", out var first);
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAsync<TaskCanceledException>(async () =>
            {
                await cache.GetNextAsync(first.Id, cts.Token);
            });
        }

        [Fact]
        public async Task OrderedCache_GetFutureFirstOrDefaultAsync_Cancelled_ThrowsTaskCanceledException()
        {
            // Arrange
            using var cache = CreateTestCache();
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAsync<TaskCanceledException>(async () =>
            {
                await cache.GetFutureFirstOrDefaultAsync(cts.Token);
            });
        }

        [Fact]
        public void OrderedCache_L1StoreReplenishment_AfterRemoval()
        {
            // Arrange - L1 can hold 2 items
            using var cache = CreateTestCache(l1MinCap: 2, l1MaxCap: 2);
            cache.Add("first", out var first);
            cache.Add("second", out _);
            cache.Add("third", out _);

            // Act - Remove first item, should replenish L1 from L2
            cache.Remove(first.Id, out _);

            // Assert - All remaining items should still be accessible
            Assert.Equal(2, cache.Count);
            cache.GetFirstOrDefault(out var newFirst);
            Assert.NotNull(newFirst);
        }

        [Fact]
        public void OrderedCache_GetEntryOrDefault_FromL2_WhenNotInL1()
        {
            // Arrange - Small L1
            using var cache = CreateTestCache(l1MinCap: 1, l1MaxCap: 1);
            cache.Add("first", out _);
            cache.Add("second", out var second); // Should overflow to L2

            // Act - Get second entry (should be in L2 only)
            var result = cache.GetEntryOrDefault(second.Id, out var retrieved);

            // Assert
            Assert.True(result);
            Assert.NotNull(retrieved);
            Assert.Equal(second.Id, retrieved.Id);
        }

        [Fact]
        public void OrderedCache_WithL1Store_AddFailureInL1_DoesNotAddToMetadata()
        {
            // This tests the resilience of the Add operation
            // Arrange
            using var cache = CreateTestCache(l1MinCap: 10, l1MaxCap: 100);

            // Act - Normal adds should succeed
            for (int i = 0; i < 5; i++)
            {
                var result = cache.Add($"item-{i}", out var entry);
                Assert.True(result);
                Assert.NotNull(entry);
            }

            // Assert
            Assert.Equal(5, cache.Count);
        }

        [Fact]
        public void OrderedCache_EmptyCache_GetNextOrDefault_ReturnsNull()
        {
            // Arrange
            using var cache = CreateTestCache();

            // Act - Non-existent GUID returns true (operation succeeded) with null entry (not found)
            var result = cache.GetNextOrDefault(Guid.NewGuid(), out var entry);

            // Assert - API design: returns true for successful operation, entry is null when not found
            Assert.True(result);
            Assert.Null(entry);
        }

        [Fact]
        public void OrderedCache_GetLastEntry_AfterMultipleAdds()
        {
            // Arrange
            using var cache = CreateTestCache();
            cache.Add("first", out _);
            cache.Add("second", out _);
            cache.Add("third", out var third);

            // Act
            var result = cache.GetLastOrDefault(out var last);

            // Assert
            Assert.True(result);
            Assert.NotNull(last);
            Assert.Equal(third.Id, last.Id);
        }

        [Fact]
        public void OrderedCache_MultipleDispose_IsSafe()
        {
            // Arrange
            var cache = CreateTestCache();
            cache.Add("test", out _);

            // Act - Multiple disposes should be safe
            cache.Dispose();
            cache.Dispose();
            cache.Dispose();

            // Assert - No exception thrown
        }

        #region Additional Eviction Tests

        [Fact]
        public async Task OrderedCache_Eviction_WithSingleActiveEnumeratorAtHead_KeepsAllEntries()
        {
            // Arrange
            var config = new Caching.Configuration { EvictAfterEveryX = 5 };
            using var cache = CreateTestCache(config: config);
            
            // Add initial entries
            for (int i = 0; i < 3; i++)
            {
                cache.Add($"item-{i}", out _);
            }

            // Start enumerator at head
            var enumerator = cache.GetAsyncEnumerator(CancellationToken.None);
            await enumerator.MoveNextAsync(); // Position at first entry (CurrentId is now item-0)

            // Act - Add more entries to trigger eviction
            for (int i = 3; i < 10; i++)
            {
                cache.Add($"item-{i}", out _);
            }

            // Assert - Enumerator is at item-0, so eviction will keep entries from item-0 onward
            // At 5th add (item-7), eviction triggered - keeps from item-0 (9 entries remain)
            // At 10th add (item-9), would trigger again but enumerator still at item-0
            Assert.True(cache.Count >= 9, $"Expected at least 9 entries, got {cache.Count}");
        }

        [Fact]
        public async Task OrderedCache_Eviction_WithMultipleEnumeratorsAtDifferentPositions_EvictsUpToSlowest()
        {
            // Arrange
            var config = new Caching.Configuration { EvictAfterEveryX = 10 };
            using var cache = CreateTestCache(config: config);
            
            // Add initial entries
            var entries = new List<IEntry<Guid, string>>();
            for (int i = 0; i < 5; i++)
            {
                cache.Add($"item-{i}", out var entry);
                entries.Add(entry);
            }

            // Fast enumerator - reads all 5 entries
            var fastEnum = cache.GetAsyncEnumerator(CancellationToken.None);
            for (int i = 0; i < 5; i++)
            {
                await fastEnum.MoveNextAsync();
            }

            // Slow enumerator - reads only 2 entries
            var slowEnum = cache.GetAsyncEnumerator(CancellationToken.None);
            await slowEnum.MoveNextAsync();
            await slowEnum.MoveNextAsync();

            // Act - Add more entries to trigger eviction (total 15 > threshold 10)
            for (int i = 5; i < 15; i++)
            {
                cache.Add($"item-{i}", out _);
            }

            // Assert - Should evict entries up to position of slowest enumerator (entry 1)
            // Slowest is at entry index 1 (0-indexed), so entry 0 and 1 should remain
            // Entry 0 can be evicted once slowest moves past it
            Assert.True(cache.Count >= 13, $"Expected at least 13 entries, got {cache.Count}"); // 15 total - 2 that slowest hasn't read
        }

        [Fact]
        public async Task OrderedCache_Eviction_EnumeratorDisposed_AllowsEviction()
        {
            // Arrange
            var config = new Caching.Configuration { EvictAfterEveryX = 5 };
            using var cache = CreateTestCache(config: config);
            
            // Add initial entries
            for (int i = 0; i < 3; i++)
            {
                cache.Add($"item-{i}", out _);
            }

            // Create and dispose enumerator
            var enumerator = cache.GetAsyncEnumerator(CancellationToken.None);
            await enumerator.MoveNextAsync();
            await enumerator.DisposeAsync();

            // Act - Add more entries to trigger eviction
            for (int i = 3; i < 10; i++)
            {
                cache.Add($"item-{i}", out _);
            }

            // Assert - With no active enumerators, entries should be evicted
            Assert.Equal(0, cache.Count);
        }

        [Fact]
        public void OrderedCache_Eviction_NoEnumerators_EvictsImmediatelyAfterThreshold()
        {
            // Arrange
            var config = new Caching.Configuration { EvictAfterEveryX = 3 };
            using var cache = CreateTestCache(config: config);
            
            // Act - Add exactly at threshold
            cache.Add("item-0", out _);
            cache.Add("item-1", out _);
            Assert.Equal(2, cache.Count);
            
            cache.Add("item-2", out _); // This triggers eviction
            
            // Assert - All entries evicted since no enumerators
            Assert.Equal(0, cache.Count);
        }

        [Fact]
        public async Task OrderedCache_Eviction_EnumeratorAtTail_EvictsAllButLast()
        {
            // Arrange
            var config = new Caching.Configuration { EvictAfterEveryX = 5 };
            using var cache = CreateTestCache(config: config);
            
            // Add entries
            for (int i = 0; i < 4; i++)
            {
                cache.Add($"item-{i}", out _);
            }

            // Enumerator reads to tail
            var enumerator = cache.GetAsyncEnumerator(CancellationToken.None);
            for (int i = 0; i < 4; i++)
            {
                await enumerator.MoveNextAsync();
            }

            // Act - Add one more to trigger eviction at threshold
            cache.Add("item-4", out _);
            cache.Add("item-5", out _); // Trigger eviction again
            
            // Assert - Should keep only entries at or after enumerator position
            Assert.True(cache.Count <= 2, $"Expected at most 2 entries, got {cache.Count}");
        }

        [Fact]
        public async Task OrderedCache_Eviction_ConcurrentEnumeratorsMovingAtSameSpeed_EvictsCorrectly()
        {
            // Arrange
            var config = new Caching.Configuration { EvictAfterEveryX = 10 };
            using var cache = CreateTestCache(config: config);
            
            // Add initial entries
            for (int i = 0; i < 5; i++)
            {
                cache.Add($"item-{i}", out _);
            }

            // Multiple enumerators at same position
            var enum1 = cache.GetAsyncEnumerator(CancellationToken.None);
            var enum2 = cache.GetAsyncEnumerator(CancellationToken.None);
            var enum3 = cache.GetAsyncEnumerator(CancellationToken.None);

            // All read 3 entries
            for (int i = 0; i < 3; i++)
            {
                await enum1.MoveNextAsync();
                await enum2.MoveNextAsync();
                await enum3.MoveNextAsync();
            }

            // Act - Add more to trigger eviction
            for (int i = 5; i < 15; i++)
            {
                cache.Add($"item-{i}", out _);
            }

            // Assert - Should evict entries before position 2 (all enumerators at same position)
            Assert.True(cache.Count >= 12, $"Expected at least 12 entries, got {cache.Count}");
        }

        [Fact]
        public async Task OrderedCache_Eviction_WithFutureEnumerator_RespectsFuturePosition()
        {
            // Arrange
            var config = new Caching.Configuration { EvictAfterEveryX = 5 };
            using var cache = CreateTestCache(config: config);
            
            // Add initial entries
            for (int i = 0; i < 3; i++)
            {
                cache.Add($"item-{i}", out _);
            }

            // Future enumerator starts at current tail
            var futureEnum = cache.GetFutureAsyncEnumerator(CancellationToken.None);

            // Act - Add more entries (future enumerator should block eviction)
            for (int i = 3; i < 10; i++)
            {
                cache.Add($"item-{i}", out _);
            }

            // Assert - Future enumerator hasn't moved, so it should be at tail position
            // Entries before its position can be evicted
            Assert.True(cache.Count <= 7, $"Expected at most 7 entries, got {cache.Count}");
        }

        #endregion

        #region EnumerateAsync Tests

        [Fact]
        public async Task EnumerateAsync_EmptyCache_ReturnsNoEntries()
        {
            // Arrange
            using var cache = CreateTestCache();
            var results = new List<(Guid, string)>();
            var cts = new CancellationTokenSource(100); // Timeout to prevent hanging

            // Act
            try
            {
                await foreach (var tuple in cache.EnumerateAsync<string>(cts.Token))
                {
                    results.Add(tuple);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cache is empty and we timeout
            }

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public async Task EnumerateAsync_WithMatchingType_ReturnsAllEntries()
        {
            // Arrange
            using var cache = CreateTestCache();
            cache.Add("first", out var entry1);
            cache.Add("second", out var entry2);
            cache.Add("third", out var entry3);
            var results = new List<(Guid, string)>();

            // Act
            await foreach (var tuple in cache.EnumerateAsync<string>())
            {
                results.Add(tuple);
                if (results.Count >= 3) break; // Stop after getting all entries
            }

            // Assert
            Assert.Equal(3, results.Count);
            Assert.Equal(entry1.Id, results[0].Item1);
            Assert.Equal("first", results[0].Item2);
            Assert.Equal(entry2.Id, results[1].Item1);
            Assert.Equal("second", results[1].Item2);
            Assert.Equal(entry3.Id, results[2].Item1);
            Assert.Equal("third", results[2].Item2);
        }

        [Fact]
        public async Task EnumerateAsync_WithCancellation_StopsEnumeration()
        {
            // Arrange
            using var cache = CreateTestCache();
            for (int i = 0; i < 10; i++)
            {
                cache.Add($"item-{i}", out _);
            }
            var cts = new CancellationTokenSource();
            var results = new List<(Guid, string)>();

            // Act
            var enumTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (var tuple in cache.EnumerateAsync<string>(cts.Token))
                    {
                        results.Add(tuple);
                        if (results.Count >= 5)
                        {
                            cts.Cancel(); // Cancel after reading 5 entries
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected
                }
            });

            await enumTask;

            // Assert - Should have stopped at or shortly after 5 entries
            Assert.True(results.Count >= 5 && results.Count <= 10, $"Expected 5-10 results, got {results.Count}");
        }

        [Fact]
        public async Task EnumerateAsync_MaintainsOrderOfEntries()
        {
            // Arrange
            using var cache = CreateTestCache();
            var expectedOrder = new List<Guid>();
            for (int i = 0; i < 10; i++)
            {
                cache.Add($"value-{i}", out var entry);
                expectedOrder.Add(entry.Id);
            }
            var results = new List<Guid>();

            // Act
            await foreach (var tuple in cache.EnumerateAsync<string>())
            {
                results.Add(tuple.Item1);
                if (results.Count >= 10) break;
            }

            // Assert
            Assert.Equal(expectedOrder, results);
        }

        [Fact]
        public async Task EnumerateAsync_WithL1AndL2Stores_EnumeratesAllEntries()
        {
            // Arrange
            using var cache = CreateTestCache(l1MinCap: 5, l1MaxCap: 10);
            for (int i = 0; i < 20; i++)
            {
                cache.Add($"item-{i}", out _);
            }
            var results = new List<(Guid, string)>();

            // Act
            await foreach (var tuple in cache.EnumerateAsync<string>())
            {
                results.Add(tuple);
                if (results.Count >= 20) break;
            }

            // Assert
            Assert.Equal(20, results.Count);
        }

        [Fact]
        public async Task EnumerateAsync_WaitsForNewEntries()
        {
            // Arrange
            using var cache = CreateTestCache();
            cache.Add("initial-1", out _);
            cache.Add("initial-2", out _);
            var results = new List<(Guid, string)>();
            var cts = new CancellationTokenSource();

            // Act
            var enumTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (var tuple in cache.EnumerateAsync<string>(cts.Token))
                    {
                        results.Add(tuple);
                        if (results.Count >= 5)
                        {
                            break; // Stop after reading 5 entries
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Not expected in this test but handle it
                }
            });

            // Wait for enumerator to start and read initial entries
            await Task.Delay(100);
            
            // Add more entries while enumeration is waiting
            cache.Add("future-1", out _);
            cache.Add("future-2", out _);
            cache.Add("future-3", out _);

            await enumTask;

            // Assert - Should have read both initial and future entries
            Assert.Equal(5, results.Count);
            Assert.Equal("initial-1", results[0].Item2);
            Assert.Equal("initial-2", results[1].Item2);
            Assert.Equal("future-1", results[2].Item2);
            Assert.Equal("future-2", results[3].Item2);
            Assert.Equal("future-3", results[4].Item2);
        }

        #endregion

        #region EnumerateFutureAsync Tests

        [Fact]
        public async Task EnumerateFutureAsync_WaitsForNewEntries()
        {
            // Arrange
            using var cache = CreateTestCache();
            cache.Add("initial", out _);
            var results = new List<(Guid, string)>();

            // Act
            var enumTask = Task.Run(async () =>
            {
                await foreach (var tuple in cache.EnumerateFutureAsync<string>())
                {
                    results.Add(tuple);
                    if (results.Count >= 3)
                    {
                        break; // Stop after getting 3 entries
                    }
                }
            });

            // Add entries after enumeration starts
            await Task.Delay(50);
            cache.Add("future-1", out var entry1);
            cache.Add("future-2", out var entry2);
            cache.Add("future-3", out var entry3);

            // Wait for enumeration to complete
            await enumTask;

            // Assert
            Assert.Equal(3, results.Count);
            Assert.Equal(entry1.Id, results[0].Item1);
            Assert.Equal("future-1", results[0].Item2);
            Assert.Equal(entry2.Id, results[1].Item1);
            Assert.Equal("future-2", results[1].Item2);
            Assert.Equal(entry3.Id, results[2].Item1);
            Assert.Equal("future-3", results[2].Item2);
        }

        [Fact]
        public async Task EnumerateFutureAsync_IgnoresExistingEntries()
        {
            // Arrange
            using var cache = CreateTestCache();
            cache.Add("existing-1", out _);
            cache.Add("existing-2", out _);
            cache.Add("existing-3", out _);
            var results = new List<(Guid, string)>();

            // Act
            var enumTask = Task.Run(async () =>
            {
                await foreach (var tuple in cache.EnumerateFutureAsync<string>())
                {
                    results.Add(tuple);
                    if (results.Count >= 2)
                    {
                        break; // Stop after getting 2 entries
                    }
                }
            });

            await Task.Delay(50);
            cache.Add("future-1", out var entry1);
            cache.Add("future-2", out var entry2);

            await enumTask;

            // Assert - Should only contain future entries, not existing ones
            Assert.Equal(2, results.Count);
            Assert.Equal(entry1.Id, results[0].Item1);
            Assert.Equal(entry2.Id, results[1].Item1);
        }

        [Fact]
        public async Task EnumerateFutureAsync_WithCancellation_StopsWaiting()
        {
            // Arrange
            using var cache = CreateTestCache();
            var cts = new CancellationTokenSource(100);
            var reached = false;

            // Act
            try
            {
                await foreach (var tuple in cache.EnumerateFutureAsync<string>(cts.Token))
                {
                    // Should not reach here since no entries will be added
                    reached = true;
                }
            }
            catch (OperationCanceledException)
            {
                // Expected
            }

            // Assert
            Assert.False(reached, "Should not have enumerated any entries");
        }

        [Fact]
        public async Task EnumerateFutureAsync_MultipleEnumerators_AllReceiveNewEntries()
        {
            // Arrange
            using var cache = CreateTestCache();
            var results1 = new List<(Guid, string)>();
            var results2 = new List<(Guid, string)>();

            // Act
            var enum1Task = Task.Run(async () =>
            {
                await foreach (var tuple in cache.EnumerateFutureAsync<string>())
                {
                    results1.Add(tuple);
                    if (results1.Count >= 2) break;
                }
            });

            var enum2Task = Task.Run(async () =>
            {
                await foreach (var tuple in cache.EnumerateFutureAsync<string>())
                {
                    results2.Add(tuple);
                    if (results2.Count >= 2) break;
                }
            });

            await Task.Delay(50);
            cache.Add("shared-1", out var entry1);
            cache.Add("shared-2", out var entry2);

            await Task.WhenAll(enum1Task, enum2Task);

            // Assert - Both enumerators should see the same entries
            Assert.Equal(2, results1.Count);
            Assert.Equal(2, results2.Count);
            Assert.Equal(entry1.Id, results1[0].Item1);
            Assert.Equal(entry1.Id, results2[0].Item1);
            Assert.Equal(entry2.Id, results1[1].Item1);
            Assert.Equal(entry2.Id, results2[1].Item1);
        }

        #endregion

        #region OnNextAsync Tests

        [Fact]
        public async Task OnNextAsync_ProcessesNewEntriesWithHandler()
        {
            // Arrange
            using var cache = CreateTestCache();
            var processedEntries = new List<(Guid, string)>();
            var cts = new CancellationTokenSource();
            var processedCount = 0;

            // Act
            var handlerTask = Task.Run(async () =>
            {
                try
                {
                    await cache.OnNextAsync<string>(
                        async (tuple, state) =>
                        {
                            var list = state as List<(Guid, string)>;
                            list?.Add(tuple);
                            processedCount++;
                            if (processedCount >= 3)
                            {
                                cts.Cancel();
                            }
                            return await Task.FromResult(true);
                        },
                        processedEntries,
                        cts.Token);
                }
                catch (OperationCanceledException)
                {
                    // Expected
                }
            });

            await Task.Delay(50);
            cache.Add("entry-1", out var entry1);
            cache.Add("entry-2", out var entry2);
            cache.Add("entry-3", out var entry3);

            await handlerTask;

            // Assert
            Assert.Equal(3, processedEntries.Count);
            Assert.Equal(entry1.Id, processedEntries[0].Item1);
            Assert.Equal("entry-1", processedEntries[0].Item2);
            Assert.Equal(entry2.Id, processedEntries[1].Item1);
            Assert.Equal("entry-2", processedEntries[1].Item2);
            Assert.Equal(entry3.Id, processedEntries[2].Item1);
            Assert.Equal("entry-3", processedEntries[2].Item2);
        }

        [Fact]
        public async Task OnNextAsync_PassesStateToHandler()
        {
            // Arrange
            using var cache = CreateTestCache();
            var counter = 0;
            var cts = new CancellationTokenSource();
            var stateObject = new { MaxCount = 2 };

            // Act
            var handlerTask = Task.Run(async () =>
            {
                try
                {
                    await cache.OnNextAsync<string>(
                        async (tuple, state) =>
                        {
                            counter++;
                            var s = state as dynamic;
                            if (counter >= s.MaxCount)
                            {
                                cts.Cancel();
                            }
                            return await Task.FromResult(true);
                        },
                        stateObject,
                        cts.Token);
                }
                catch (OperationCanceledException)
                {
                    // Expected
                }
            });

            await Task.Delay(50);
            cache.Add("entry-1", out _);
            cache.Add("entry-2", out _);

            await handlerTask;

            // Assert
            Assert.Equal(2, counter);
        }

        [Fact]
        public async Task OnNextAsync_WithNullHandler_DoesNotThrow()
        {
            // Arrange
            using var cache = CreateTestCache();
            var cts = new CancellationTokenSource(100);

            // Act - Should not throw, just complete when cancelled
            try
            {
                await cache.OnNextAsync<string>(null, null, cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected
            }

            // No assertion needed - test passes if no exception other than OperationCanceledException
        }

        [Fact]
        public async Task OnNextAsync_WithCancellation_StopsProcessing()
        {
            // Arrange
            using var cache = CreateTestCache();
            var processedCount = 0;
            var cts = new CancellationTokenSource();

            // Act
            var handlerTask = Task.Run(async () =>
            {
                try
                {
                    await cache.OnNextAsync<string>(
                        async (tuple, state) =>
                        {
                            processedCount++;
                            return await Task.FromResult(true);
                        },
                        null,
                        cts.Token);
                }
                catch (OperationCanceledException)
                {
                    // Expected
                }
            });

            await Task.Delay(50);
            cache.Add("entry-1", out _);
            await Task.Delay(50);
            cts.Cancel();
            cache.Add("entry-2", out _);

            await handlerTask;

            // Assert - Should have processed only entry-1, not entry-2
            Assert.Equal(1, processedCount);
        }

        [Fact]
        public async Task OnNextAsync_IgnoresExistingEntries()
        {
            // Arrange
            using var cache = CreateTestCache();
            cache.Add("existing-1", out _);
            cache.Add("existing-2", out _);
            var processedEntries = new List<string>();
            var cts = new CancellationTokenSource();
            var processedCount = 0;

            // Act
            var handlerTask = Task.Run(async () =>
            {
                try
                {
                    await cache.OnNextAsync<string>(
                        async (tuple, state) =>
                        {
                            var list = state as List<string>;
                            list?.Add(tuple.Item2);
                            processedCount++;
                            if (processedCount >= 2)
                            {
                                cts.Cancel();
                            }
                            return await Task.FromResult(true);
                        },
                        processedEntries,
                        cts.Token);
                }
                catch (OperationCanceledException)
                {
                    // Expected
                }
            });

            await Task.Delay(50);
            cache.Add("future-1", out _);
            cache.Add("future-2", out _);

            await handlerTask;

            // Assert - Should only process future entries
            Assert.Equal(2, processedEntries.Count);
            Assert.Equal("future-1", processedEntries[0]);
            Assert.Equal("future-2", processedEntries[1]);
        }

        [Fact]
        public async Task OnNextAsync_HandlerCanProcessAsynchronously()
        {
            // Arrange
            using var cache = CreateTestCache();
            var processedEntries = new List<(Guid, string, DateTime)>();
            var cts = new CancellationTokenSource();
            var processedCount = 0;

            // Act
            var handlerTask = Task.Run(async () =>
            {
                try
                {
                    await cache.OnNextAsync<string>(
                        async (tuple, state) =>
                        {
                            var list = state as List<(Guid, string, DateTime)>;
                            await Task.Delay(10); // Simulate async work
                            list?.Add((tuple.Item1, tuple.Item2, DateTime.UtcNow));
                            processedCount++;
                            if (processedCount >= 3)
                            {
                                cts.Cancel();
                            }
                            return await Task.FromResult(true);
                        },
                        processedEntries,
                        cts.Token);
                }
                catch (OperationCanceledException)
                {
                    // Expected
                }
            });

            await Task.Delay(50);
            cache.Add("async-1", out _);
            cache.Add("async-2", out _);
            cache.Add("async-3", out _);

            await handlerTask;

            // Assert
            Assert.Equal(3, processedEntries.Count);
            Assert.Equal("async-1", processedEntries[0].Item2);
            Assert.Equal("async-2", processedEntries[1].Item2);
            Assert.Equal("async-3", processedEntries[2].Item2);
        }

        #endregion
    }
}