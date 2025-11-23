using Baubit.Caching.InMemory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Baubit.Caching.Test.OrderedCache
{
    /// <summary>
    /// Concurrency and parallel access tests for <see cref="OrderedCache{TValue}"/>
    /// </summary>
    public class ConcurrencyTest
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
        public void OrderedCache_ConcurrentAdd_AllSucceed()
        {
            // Arrange
            using var cache = CreateTestCache();
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
            Task.WaitAll(tasks);

            // Assert
            Assert.All(addResults, result => Assert.True(result));
            Assert.Equal(threadCount * itemsPerThread, cache.Count);
        }

        [Fact]
        public void OrderedCache_ConcurrentRead_AllSucceed()
        {
            // Arrange
            using var cache = CreateTestCache();
            // Pre-populate cache
            var entries = new List<IEntry<string>>();
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
            Task.WaitAll(tasks);

            // Assert
            Assert.All(readResults, result => Assert.True(result));
        }

        [Fact]
        public void OrderedCache_ConcurrentMixedReadWrite_NoDeadlock()
        {
            // Arrange
            using var cache = CreateTestCache();
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

            var completed = Task.WaitAll(tasks.ToArray(), TimeSpan.FromSeconds(10));

            // Assert
            Assert.True(completed, "Operations should complete without deadlock");
            Assert.True(allSuccessful, "All write operations should succeed");
        }

        [Fact]
        public void OrderedCache_ConcurrentRemove_HandlesCorrectly()
        {
            // Arrange
            using var cache = CreateTestCache();
            var entries = new List<IEntry<string>>();
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
            Task.WaitAll(tasks);

            // Assert
            Assert.All(removeResults, result => Assert.True(result));
            Assert.Equal(0, cache.Count);
        }

        [Fact]
        public void OrderedCache_ConcurrentUpdate_AllSucceed()
        {
            // Arrange
            using var cache = CreateTestCache();
            var entries = new List<IEntry<string>>();
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
            Task.WaitAll(tasks);

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
            var tasks = new Task<IEntry<string>>[waiterCount];

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
            var tasks = new Task<IEntry<string>>[waiterCount];

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
        public void OrderedCache_ConcurrentEviction_WithActiveEnumerators()
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
            enumeratorStarted.Task.Wait(TimeSpan.FromSeconds(1));

            for (int i = 1; i < 50; i++)
            {
                cache.Add($"item-{i}", out _);
            }

            continueEnumeration.SetResult(true);
            enumeratorTask.Wait(TimeSpan.FromSeconds(2));

            // Assert - Should not throw or deadlock
            Assert.True(enumeratorTask.IsCompleted);
        }

        [Fact]
        public void OrderedCache_ConcurrentClear_WithReaders()
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

            Task.WaitAll(tasks.ToArray());

            // Assert
            Assert.True(clearExecuted);
            Assert.Equal(0, cache.Count);
        }

        [Fact]
        public void OrderedCache_ConcurrentAddAndGetNext_RaceCondition()
        {
            // Arrange
            using var cache = CreateTestCache();
            cache.Add("initial", out var initial);

            var tasks = new List<Task>();
            var nextResults = new System.Collections.Concurrent.ConcurrentBag<IEntry<string>>();

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

            Task.WaitAll(tasks.ToArray());

            // Assert - Should complete without exceptions
            Assert.True(tasks.All(t => t.IsCompleted));
        }

        [Fact]
        public void OrderedCache_ConcurrentL1L2Access_MaintainsConsistency()
        {
            // Arrange - Small L1 cache to force L1/L2 interaction
            using var cache = CreateTestCache(l1MinCap: 10, l1MaxCap: 10);
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

            Task.WaitAll(tasks.ToArray());

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
        public void OrderedCache_ConcurrentDispose_HandlesGracefully()
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
            Thread.Sleep(50);
            cache.Dispose();

            // Wait for all tasks
            try
            {
                Task.WaitAll(tasks.ToArray(), TimeSpan.FromSeconds(5));
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
        public void OrderedCache_ConcurrentUpdateSameEntry_LastWins()
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

            Task.WaitAll(tasks);

            // Assert
            Assert.All(updateResults, result => Assert.True(result));
            cache.GetEntryOrDefault(entry.Id, out var final);
            Assert.NotNull(final);
            Assert.NotEqual("original", final.Value); // Should be updated
        }

        [Fact]
        public void OrderedCache_StressTest_MixedOperations()
        {
            // Arrange
            using var cache = CreateTestCache(l1MinCap: 50, l1MaxCap: 100);
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
                            case 3: // Update
                                if (!addedIds.IsEmpty && addedIds.TryPeek(out var id))
                                    cache.Update(id, $"updated-{j}");
                                break;
                            case 4: // GetLast
                                cache.GetLastOrDefault(out _);
                                break;
                        }
                    }
                }));
            }

            var completed = Task.WaitAll(tasks.ToArray(), TimeSpan.FromSeconds(30));

            // Assert
            Assert.True(completed, "Stress test should complete without deadlock");
            Assert.True(cache.Count > 0, "Cache should have entries");
        }
    }
}
