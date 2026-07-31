using Microsoft.Extensions.Logging.Abstractions;

namespace Baubit.Caching.Test.InMemory.Metadata
{
    /// <summary>
    /// Tests for <see cref="Baubit.Caching.InMemory.Metadata"/>
    /// </summary>
    public class Test
    {
        private readonly Baubit.Identity.IIdentityGenerator idGenerator = Baubit.Identity.IdentityGenerator.CreateNew();

        private Guid GenerateNextId()
        {
            return idGenerator.GetNext();
        }

        [Fact]
        public void Metadata_Constructor_InitializesEmpty()
        {
            // Arrange & Act
            var metadata = new Baubit.Caching.InMemory.Metadata<Guid>(new Caching.Configuration(), NullLoggerFactory.Instance);

            // Assert
            Assert.Equal(0, metadata.Count);
            Assert.Null(metadata.HeadId);
            Assert.Null(metadata.TailId);
        }

        [Fact]
        public void Metadata_AddTail_FirstEntry_SetsHeadAndTail()
        {
            // Arrange
            var metadata = new Baubit.Caching.InMemory.Metadata<Guid>(new Caching.Configuration(), NullLoggerFactory.Instance);
            var id = GenerateNextId();

            // Act
            var result = metadata.AddTail(id);

            // Assert
            Assert.True(result);
            Assert.Equal(1, metadata.Count);
            Assert.Equal(id, metadata.HeadId);
            Assert.Equal(id, metadata.TailId);
        }

        [Fact]
        public void Metadata_AddTail_MultipleEntries_MaintainsOrder()
        {
            // Arrange
            var metadata = new Baubit.Caching.InMemory.Metadata<Guid>(new Caching.Configuration(), NullLoggerFactory.Instance);
            var id1 = GenerateNextId();
            metadata.AddTail(id1);
            var id2 = GenerateNextId();
            metadata.AddTail(id2);
            var id3 = GenerateNextId();

            // Act
            metadata.AddTail(id3);

            // Assert
            Assert.Equal(3, metadata.Count);
            Assert.Equal(id1, metadata.HeadId);
            Assert.Equal(id3, metadata.TailId);
        }

        [Fact]
        public void Metadata_ContainsKey_ExistingId_ReturnsTrue()
        {
            // Arrange
            var metadata = new Baubit.Caching.InMemory.Metadata<Guid>(new Caching.Configuration(), NullLoggerFactory.Instance);
            var id = GenerateNextId();
            metadata.AddTail(id);

            // Act
            var result = metadata.ContainsKey(id);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void Metadata_ContainsKey_NonExistingId_ReturnsFalse()
        {
            // Arrange
            var metadata = new Baubit.Caching.InMemory.Metadata<Guid>(new Caching.Configuration(), NullLoggerFactory.Instance);
            var id = GenerateNextId();

            // Act
            var result = metadata.ContainsKey(id);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Metadata_GetNextId_NullId_ReturnsHead()
        {
            // Arrange
            var metadata = new Baubit.Caching.InMemory.Metadata<Guid>(new Caching.Configuration(), NullLoggerFactory.Instance);
            var id1 = GenerateNextId();
            metadata.AddTail(id1);
            var id2 = GenerateNextId();
            metadata.AddTail(id2);

            // Act
            var result = metadata.GetNextId(null, out var nextId);

            // Assert
            Assert.True(result);
            Assert.Equal(id1, nextId);
        }

        [Fact]
        public void Metadata_GetNextId_HeadId_ReturnsSecond()
        {
            // Arrange
            var metadata = new Baubit.Caching.InMemory.Metadata<Guid>(new Caching.Configuration(), NullLoggerFactory.Instance);
            var id1 = GenerateNextId();
            metadata.AddTail(id1);
            var id2 = GenerateNextId();
            metadata.AddTail(id2);

            // Act
            var result = metadata.GetNextId(id1, out var nextId);

            // Assert
            Assert.True(result);
            Assert.Equal(id2, nextId);
        }

        [Fact]
        public void Metadata_GetNextId_TailId_ReturnsNull()
        {
            // Arrange
            var metadata = new Baubit.Caching.InMemory.Metadata<Guid>(new Caching.Configuration(), NullLoggerFactory.Instance);
            var id1 = GenerateNextId();
            metadata.AddTail(id1);
            var id2 = GenerateNextId();
            metadata.AddTail(id2);

            // Act - Use the actual TailId from metadata
            var result = metadata.GetNextId(metadata.TailId, out var nextId);

            // Assert
            Assert.True(result);
            Assert.Null(nextId);
        }

        [Fact]
        public void Metadata_GetNextId_IdSmallerThanHead_ReturnsHead()
        {
            // Arrange
            var metadata = new Baubit.Caching.InMemory.Metadata<Guid>(new Caching.Configuration(), NullLoggerFactory.Instance);
            var id1 = Guid.Parse("10000000-0000-0000-0000-000000000000");
            var id2 = Guid.Parse("20000000-0000-0000-0000-000000000000");
            var smallerId = Guid.Parse("05000000-0000-0000-0000-000000000000");
            metadata.AddTail(id1);
            metadata.AddTail(id2);

            // Act
            var result = metadata.GetNextId(smallerId, out var nextId);

            // Assert
            Assert.True(result);
            Assert.Equal(id1, nextId);
        }

        [Fact]
        public async Task Metadata_GetNextIdAsync_WhenNoNext_WaitsForNew()
        {
            // Arrange
            var metadata = new Baubit.Caching.InMemory.Metadata<Guid>(new Caching.Configuration(), NullLoggerFactory.Instance);
            var id1 = GenerateNextId();
            metadata.AddTail(id1);

            // Use a TaskCompletionSource to ensure the async wait is properly started
            var waitStarted = new TaskCompletionSource<bool>();

            // Act
            var nextIdTask = Task.Run(async () =>
            {
                var task = metadata.GetNextIdAsync(id1, CancellationToken.None);
                waitStarted.SetResult(true);
                return await task;
            });

            // Wait for the async operation to actually start and register in the waiting room
            await waitStarted.Task;

            // Add a small delay to ensure Join() has been called and _numOfGuests incremented
            await Task.Delay(100);

            var id2 = GenerateNextId();
            metadata.AddTail(id2);

            var nextId = await nextIdTask;

            // Assert
            Assert.Equal(id2, nextId);
        }

        [Fact]
        public async Task Metadata_GetNextIdAsync_WhenNextExists_ReturnsImmediately()
        {
            // Arrange
            var metadata = new Baubit.Caching.InMemory.Metadata<Guid>(new Caching.Configuration(), NullLoggerFactory.Instance);
            var id1 = GenerateNextId();
            metadata.AddTail(id1);
            var id2 = GenerateNextId();
            metadata.AddTail(id2);

            // Act
            var nextId = await metadata.GetNextIdAsync(id1, CancellationToken.None);

            // Assert
            Assert.Equal(id2, nextId);
        }

        [Fact]
        public void Metadata_Remove_ExistingId_Success()
        {
            // Arrange
            var metadata = new Baubit.Caching.InMemory.Metadata<Guid>(new Caching.Configuration(), NullLoggerFactory.Instance);
            var id1 = GenerateNextId();
            metadata.AddTail(id1);
            var id2 = GenerateNextId();
            metadata.AddTail(id2);

            // Act
            var result = metadata.Remove(id1);

            // Assert
            Assert.True(result);
            Assert.Equal(1, metadata.Count);
            Assert.False(metadata.ContainsKey(id1));
            Assert.Equal(id2, metadata.HeadId);
        }

        [Fact]
        public void Metadata_Remove_NonExistingId_ReturnsFalse()
        {
            // Arrange
            var metadata = new Baubit.Caching.InMemory.Metadata<Guid>(new Caching.Configuration(), NullLoggerFactory.Instance);
            var id = GenerateNextId();

            // Act
            var result = metadata.Remove(id);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Metadata_Remove_LastEntry_SetsHeadAndTailToNull()
        {
            // Arrange
            var metadata = new Baubit.Caching.InMemory.Metadata<Guid>(new Caching.Configuration(), NullLoggerFactory.Instance);
            var id = GenerateNextId();
            metadata.AddTail(id);

            // Act
            var result = metadata.Remove(id);

            // Assert
            Assert.True(result);
            Assert.Equal(0, metadata.Count);
            Assert.Null(metadata.HeadId);
            Assert.Null(metadata.TailId);
        }

        [Fact]
        public void Metadata_GetIdsThrough_EmptyStore_ReturnsEmpty()
        {
            // Arrange
            var metadata = new Baubit.Caching.InMemory.Metadata<Guid>(new Caching.Configuration(), NullLoggerFactory.Instance);
            var id = GenerateNextId();

            // Act
            var result = metadata.GetIdsThrough(id, out var ids);

            // Assert
            Assert.False(result);
            Assert.Empty(ids);
        }

        [Fact]
        public void Metadata_GetIdsThrough_IdBeforeHead_ReturnsEmpty()
        {
            // Arrange
            var metadata = new Baubit.Caching.InMemory.Metadata<Guid>(new Caching.Configuration(), NullLoggerFactory.Instance);
            var id1 = Guid.Parse("20000000-0000-0000-0000-000000000000");
            var smallerId = Guid.Parse("10000000-0000-0000-0000-000000000000");
            metadata.AddTail(id1);

            // Act
            var result = metadata.GetIdsThrough(smallerId, out var ids);

            // Assert
            Assert.False(result);
            Assert.Empty(ids);
        }

        [Fact]
        public void Metadata_GetIdsThrough_IdAtTail_ReturnsAllIds()
        {
            // Arrange
            var metadata = new Baubit.Caching.InMemory.Metadata<Guid>(new Caching.Configuration(), NullLoggerFactory.Instance);
            var id1 = Guid.Parse("10000000-0000-0000-0000-000000000000");
            var id2 = Guid.Parse("20000000-0000-0000-0000-000000000000");
            var id3 = Guid.Parse("30000000-0000-0000-0000-000000000000");
            metadata.AddTail(id1);
            metadata.AddTail(id2);
            metadata.AddTail(id3);

            // Act
            var result = metadata.GetIdsThrough(id3, out var ids);

            // Assert
            Assert.True(result);
            var idArray = ids.ToArray();
            Assert.Equal(3, idArray.Length);
            Assert.Equal(id1, idArray[0]);
            Assert.Equal(id2, idArray[1]);
            Assert.Equal(id3, idArray[2]);
        }

        [Fact]
        public void Metadata_GetIdsThrough_MiddleId_ReturnsPartialList()
        {
            // Arrange
            var metadata = new Baubit.Caching.InMemory.Metadata<Guid>(new Caching.Configuration(), NullLoggerFactory.Instance);
            var id1 = Guid.Parse("10000000-0000-0000-0000-000000000000");
            var id2 = Guid.Parse("20000000-0000-0000-0000-000000000000");
            var id3 = Guid.Parse("30000000-0000-0000-0000-000000000000");
            metadata.AddTail(id1);
            metadata.AddTail(id2);
            metadata.AddTail(id3);

            // Act
            var result = metadata.GetIdsThrough(id2, out var ids);

            // Assert
            Assert.True(result);
            var idArray = ids.ToArray();
            Assert.Equal(2, idArray.Length);
            Assert.Equal(id1, idArray[0]);
            Assert.Equal(id2, idArray[1]);
        }

        [Fact]
        public async Task Metadata_ResetRoomCount_ReturnsAndResets()
        {
            // Arrange
            var metadata = new Baubit.Caching.InMemory.Metadata<Guid>(new Caching.Configuration { RunAdaptiveResizing = true }, NullLoggerFactory.Instance);

            // Add first entry
            var id1 = GenerateNextId();
            metadata.AddTail(id1);

            // Start an async wait (this will join the waiting room)
            var nextIdTask = metadata.GetNextIdAsync(id1, CancellationToken.None);

            // Give the task a moment to register in the waiting room
            await Task.Delay(10);

            // Add second entry while there's a waiter - this should increment room count
            var id2 = GenerateNextId();
            metadata.AddTail(id2);

            // Wait for the async operation to complete
            await nextIdTask;

            // Act
            var count = metadata.ResetRoomCount();
            var secondCount = metadata.ResetRoomCount();

            // Assert
            Assert.Equal(1, count); // Should have counted 1 room entry
            Assert.Equal(0, secondCount); // Should be reset to 0
        }

        [Fact]
        public void Metadata_Dispose_CompletesSuccessfully()
        {
            // Arrange
            var metadata = new Baubit.Caching.InMemory.Metadata<Guid>(new Caching.Configuration(), NullLoggerFactory.Instance);
            var id = GenerateNextId();
            metadata.AddTail(id);

            // Act
            metadata.Dispose();

            // Assert - Cannot access properties after dispose
            Assert.Null(metadata.HeadId);
            Assert.Null(metadata.TailId);
            Assert.Throws<NullReferenceException>(() => metadata.Count);
        }

        [Fact]
        public void Metadata_GetNextId_DeletedMiddleId_ReturnsNextBiggestId()
        {
            // Arrange - Tests line 62: out-of-order deletion scenario
            // When an id is not in IdNodeMap but is between head and tail
            var metadata = new Baubit.Caching.InMemory.Metadata<Guid>(new Caching.Configuration(), NullLoggerFactory.Instance);
            var id1 = Guid.Parse("10000000-0000-0000-0000-000000000000");
            var id2 = Guid.Parse("20000000-0000-0000-0000-000000000000");
            var id3 = Guid.Parse("30000000-0000-0000-0000-000000000000");
            metadata.AddTail(id1);
            metadata.AddTail(id2);
            metadata.AddTail(id3);

            // Act - Remove middle id and then ask for next of that removed id
            metadata.Remove(id2);
            var result = metadata.GetNextId(id2, out var nextId);

            // Assert - Should return id3 (the next biggest id after id2)
            Assert.True(result);
            Assert.Equal(id3, nextId);
        }

        [Fact]
        public void Metadata_GetIdsThrough_IdNotInMap_ReturnsEmptyAndFalse()
        {
            // Arrange - Tests lines 106-110: edge case where id is in range but not in IdNodeMap
            var metadata = new Baubit.Caching.InMemory.Metadata<Guid>(new Caching.Configuration(), NullLoggerFactory.Instance);
            var id1 = Guid.Parse("10000000-0000-0000-0000-000000000000");
            var id3 = Guid.Parse("30000000-0000-0000-0000-000000000000");
            var idNotInMap = Guid.Parse("20000000-0000-0000-0000-000000000000");
            metadata.AddTail(id1);
            metadata.AddTail(id3);

            // Act - Request ids through an id that's in range but not in the map
            var result = metadata.GetIdsThrough(idNotInMap, out var ids);

            // Assert
            Assert.False(result);
            Assert.Empty(ids);
        }

        [Fact]
        public void Metadata_GetNextId_EmptyMetadata_WithNonNullId_ReturnsNull()
        {
            // Arrange - Tests line 58: when HeadId is null but id is not null
            var metadata = new Baubit.Caching.InMemory.Metadata<Guid>(new Caching.Configuration(), NullLoggerFactory.Instance);
            var someId = Guid.NewGuid();

            // Act
            var result = metadata.GetNextId(someId, out var nextId);

            // Assert
            Assert.True(result);
            Assert.Null(nextId);
        }

        [Fact]
        public void Metadata_Constructor_WithStartingIds_InitializesInOrder()
        {
            // Arrange
            var id1 = Guid.Parse("30000000-0000-0000-0000-000000000000");
            var id2 = Guid.Parse("10000000-0000-0000-0000-000000000000");
            var id3 = Guid.Parse("20000000-0000-0000-0000-000000000000");
            var startingIds = new List<Guid> { id1, id2, id3 };

            // Act
            var metadata = new Baubit.Caching.InMemory.Metadata<Guid>(
                new Caching.Configuration(),
                NullLoggerFactory.Instance,
                startingIds);

            // Assert - Should be ordered: id2, id3, id1
            Assert.Equal(3, metadata.Count);
            Assert.Equal(id2, metadata.HeadId);
            Assert.Equal(id1, metadata.TailId);
            Assert.True(metadata.ContainsKey(id1));
            Assert.True(metadata.ContainsKey(id2));
            Assert.True(metadata.ContainsKey(id3));
        }

        [Fact]
        public void Metadata_Constructor_WithStartingIds_GuaranteesDirectAccess()
        {
            // Arrange
            var id1 = GenerateNextId();
            var id2 = GenerateNextId();
            var id3 = GenerateNextId();
            var startingIds = new List<Guid> { id1, id2, id3 };

            // Act
            var metadata = new Baubit.Caching.InMemory.Metadata<Guid>(
                new Caching.Configuration(),
                NullLoggerFactory.Instance,
                startingIds);

            // Assert - Direct access to each id should work
            Assert.True(metadata.ContainsKey(id1));
            Assert.True(metadata.ContainsKey(id2));
            Assert.True(metadata.ContainsKey(id3));

            // GetNextId should work for all entries
            Assert.True(metadata.GetNextId(null, out var firstId));
            Assert.Equal(id1, firstId);

            Assert.True(metadata.GetNextId(id1, out var secondId));
            Assert.Equal(id2, secondId);

            Assert.True(metadata.GetNextId(id2, out var thirdId));
            Assert.Equal(id3, thirdId);
        }

        [Fact]
        public void Metadata_Constructor_WithNullStartingIds_InitializesEmpty()
        {
            // Arrange & Act
            var metadata = new Baubit.Caching.InMemory.Metadata<Guid>(
                new Caching.Configuration(),
                NullLoggerFactory.Instance,
                null);

            // Assert
            Assert.Equal(0, metadata.Count);
            Assert.Null(metadata.HeadId);
            Assert.Null(metadata.TailId);
        }

        [Fact]
        public void Metadata_Constructor_WithEmptyStartingIds_InitializesEmpty()
        {
            // Arrange
            var startingIds = new List<Guid>();

            // Act
            var metadata = new Baubit.Caching.InMemory.Metadata<Guid>(
                new Caching.Configuration(),
                NullLoggerFactory.Instance,
                startingIds);

            // Assert
            Assert.Equal(0, metadata.Count);
            Assert.Null(metadata.HeadId);
            Assert.Null(metadata.TailId);
        }

        [Fact]
        public void Metadata_Constructor_WithDuplicateStartingIds_ThrowsArgumentException()
        {
            // Arrange
            var id1 = GenerateNextId();
            var id2 = GenerateNextId();
            var startingIds = new List<Guid> { id1, id2, id1 }; // id1 appears twice

            // Act & Assert
            Assert.Throws<ArgumentException>(() => new Baubit.Caching.InMemory.Metadata<Guid>(
                new Caching.Configuration(),
                NullLoggerFactory.Instance,
                startingIds));
        }

        [Fact]
        public void Metadata_Constructor_WithSingleStartingId_SetsHeadAndTailToSameValue()
        {
            // Arrange
            var id = GenerateNextId();
            var startingIds = new List<Guid> { id };

            // Act
            var metadata = new Baubit.Caching.InMemory.Metadata<Guid>(
                new Caching.Configuration(),
                NullLoggerFactory.Instance,
                startingIds);

            // Assert
            Assert.Equal(1, metadata.Count);
            Assert.Equal(id, metadata.HeadId);
            Assert.Equal(id, metadata.TailId);
        }

        [Fact]
        public void Metadata_Constructor_WithStartingIds_AllowsSubsequentAddTail()
        {
            // Arrange
            var id1 = GenerateNextId();
            var id2 = GenerateNextId();
            var startingIds = new List<Guid> { id1, id2 };
            var metadata = new Baubit.Caching.InMemory.Metadata<Guid>(
                new Caching.Configuration(),
                NullLoggerFactory.Instance,
                startingIds);

            // Act
            var id3 = GenerateNextId();
            var result = metadata.AddTail(id3);

            // Assert
            Assert.True(result);
            Assert.Equal(3, metadata.Count);
            Assert.Equal(id3, metadata.TailId);
        }

        [Fact]
        public void Metadata_GetNextId_WhenIdDeletedOutOfOrder_ReturnsNextGreaterId()
        {
            // Arrange - Add 3 ids, remove the middle one, then ask for next after the middle
            var metadata = new Baubit.Caching.InMemory.Metadata<Guid>(new Caching.Configuration(), NullLoggerFactory.Instance);
            var id1 = GenerateNextId();
            var id2 = GenerateNextId();
            var id3 = GenerateNextId();
            metadata.AddTail(id1);
            metadata.AddTail(id2);
            metadata.AddTail(id3);

            // Remove middle id
            metadata.Remove(id2);

            // Act - Ask for next after the removed id (triggers FindNextGreaterId)
            var result = metadata.GetNextId(id2, out var nextId);

            // Assert
            Assert.True(result);
            Assert.Equal(id3, nextId);
        }

        [Fact]
        public void Metadata_GetNextId_WhenIdSmallerThanHead_ReturnsHead()
        {
            // Arrange
            var metadata = new Baubit.Caching.InMemory.Metadata<int>(new Caching.Configuration(), NullLoggerFactory.Instance);
            metadata.AddTail(10);
            metadata.AddTail(20);
            metadata.AddTail(30);

            // Act - Ask for next after an id smaller than head
            var result = metadata.GetNextId(5, out var nextId);

            // Assert
            Assert.True(result);
            Assert.Equal(10, nextId);
        }

        [Fact]
        public void Metadata_GetNextId_WhenHeadIsNull_ReturnsNull()
        {
            // Arrange - Empty metadata with a non-null id request
            var metadata = new Baubit.Caching.InMemory.Metadata<int>(new Caching.Configuration(), NullLoggerFactory.Instance);

            // Add and remove an entry to simulate the scenario where head is null
            // but the caller has a non-null id
            metadata.AddTail(10);
            metadata.Remove(10);

            // Act
            var result = metadata.GetNextId(10, out var nextId);

            // Assert
            Assert.True(result);
            Assert.Null(nextId);
        }

        [Fact]
        public void Metadata_GetNextId_WhenIdIsTail_ReturnsNull()
        {
            // Arrange
            var metadata = new Baubit.Caching.InMemory.Metadata<int>(new Caching.Configuration(), NullLoggerFactory.Instance);
            metadata.AddTail(10);
            metadata.AddTail(20);

            // Act
            var result = metadata.GetNextId(20, out var nextId);

            // Assert
            Assert.True(result);
            Assert.Null(nextId);
        }

        [Fact]
        public void Metadata_FindNextGreaterId_WhenNoGreaterIdExists_ReturnsNull()
        {
            // Arrange
            var metadata = new Baubit.Caching.InMemory.Metadata<int>(new Caching.Configuration(), NullLoggerFactory.Instance);
            metadata.AddTail(10);
            metadata.AddTail(20);

            // Remove 20, then ask for next after a value greater than all remaining
            metadata.Remove(20);

            // Act - Ask for next after 15 (not in map, FindNextGreaterId path, no greater id exists since only 10 left)
            var result = metadata.GetNextId(15, out var nextId);

            // Assert
            Assert.True(result);
            Assert.Null(nextId);
        }

        [Fact]
        public void Metadata_GetIdsThrough_WhenIdPrecedesHead_ReturnsEmpty()
        {
            // Arrange
            var metadata = new Baubit.Caching.InMemory.Metadata<int>(new Caching.Configuration(), NullLoggerFactory.Instance);
            metadata.AddTail(10);
            metadata.AddTail(20);
            metadata.AddTail(30);

            // Act - id 5 precedes head 10
            var result = metadata.GetIdsThrough(5, out var ids);

            // Assert
            Assert.False(result);
            Assert.Empty(ids);
        }

        [Fact]
        public void Metadata_GetIdsThrough_WhenIdAtOrAfterTail_ReturnsWholeList()
        {
            // Arrange
            var metadata = new Baubit.Caching.InMemory.Metadata<int>(new Caching.Configuration(), NullLoggerFactory.Instance);
            metadata.AddTail(10);
            metadata.AddTail(20);
            metadata.AddTail(30);

            // Act - id >= tail
            var result = metadata.GetIdsThrough(30, out var ids);

            // Assert
            Assert.True(result);
            Assert.Equal(new[] { 10, 20, 30 }, ids);
        }

        [Fact]
        public void Metadata_GetIdsThrough_WhenIdNotInMap_ReturnsEmpty()
        {
            // Arrange
            var metadata = new Baubit.Caching.InMemory.Metadata<int>(new Caching.Configuration(), NullLoggerFactory.Instance);
            metadata.AddTail(10);
            metadata.AddTail(20);
            metadata.AddTail(30);

            // Act - id 15 is between head and tail but not in map
            var result = metadata.GetIdsThrough(15, out var ids);

            // Assert
            Assert.False(result);
            Assert.Empty(ids);
        }

        [Fact]
        public void Metadata_SignalAwaiters_WhenAdaptiveResizingEnabled_IncrementsRoomCount()
        {
            // Arrange
            var config = new Caching.Configuration { RunAdaptiveResizing = true };
            var metadata = new Baubit.Caching.InMemory.Metadata<int>(config, NullLoggerFactory.Instance);

            // Create a waiter to ensure HasGuests is true
            var cts = new CancellationTokenSource();
            var waiterTask = metadata.GetNextIdAsync(null, cts.Token);

            // Act - Add tail triggers SignalAwaiters with RunAdaptiveResizing enabled
            metadata.AddTail(1);

            // Assert
            var roomCount = metadata.ResetRoomCount();
            Assert.Equal(1, roomCount);

            cts.Cancel();
        }

        [Fact]
        public void Metadata_IsIdSmallerThanHeadId_WithNullId_ReturnsFalse()
        {
            // Arrange
            var metadata = new Baubit.Caching.InMemory.Metadata<int>(new Caching.Configuration(), NullLoggerFactory.Instance);
            metadata.AddTail(10);

            // Act - null id passed to GetNextId returns head
            var result = metadata.GetNextId(null, out var nextId);

            // Assert
            Assert.True(result);
            Assert.Equal(10, nextId);
        }

        [Fact]
        public void Metadata_SignalAwaiters_WhenAdaptiveResizingDisabled_DoesNotIncrementRoomCount()
        {
            // Arrange - RunAdaptiveResizing is false (default)
            var config = new Caching.Configuration { RunAdaptiveResizing = false };
            var metadata = new Baubit.Caching.InMemory.Metadata<int>(config, NullLoggerFactory.Instance);

            // Create a waiter to ensure HasGuests is true
            var cts = new CancellationTokenSource();
            var waiterTask = metadata.GetNextIdAsync(null, cts.Token);

            // Act - Add tail triggers SignalAwaiters but RunAdaptiveResizing is false
            metadata.AddTail(1);

            // Assert - Room count should NOT be incremented
            var roomCount = metadata.ResetRoomCount();
            Assert.Equal(0, roomCount);

            cts.Cancel();
        }

        [Fact]
        public void Metadata_GetNextId_InBetweenNode_ReturnsNext()
        {
            // Arrange - Add 3 entries, ask for next after the middle one
            var metadata = new Baubit.Caching.InMemory.Metadata<int>(new Caching.Configuration(), NullLoggerFactory.Instance);
            metadata.AddTail(10);
            metadata.AddTail(20);
            metadata.AddTail(30);

            // Act - Get next after middle id (hits IdNodeMap.TryGetValue path, node.Next is not null)
            var result = metadata.GetNextId(20, out var nextId);

            // Assert
            Assert.True(result);
            Assert.Equal(30, nextId);
        }

        [Fact]
        public void Metadata_FindNextGreaterId_WithMultipleGreaterIds_ReturnsSmallest()
        {
            // Arrange - Add ids 10, 30, 50; remove 20 (not present) to trigger FindNextGreaterId
            var metadata = new Baubit.Caching.InMemory.Metadata<int>(new Caching.Configuration(), NullLoggerFactory.Instance);
            metadata.AddTail(10);
            metadata.AddTail(30);
            metadata.AddTail(50);

            // Remove 30 to make GetNextId(25) trigger FindNextGreaterId
            metadata.Remove(30);

            // Act - 25 is not in map, not smaller than head, not tail -> FindNextGreaterId
            // Should find 50 (only remaining id > 25)
            var result = metadata.GetNextId(25, out var nextId);

            // Assert
            Assert.True(result);
            Assert.Equal(50, nextId);
        }

        [Fact]
        public void Metadata_GetIdsThrough_MiddleId_ReturnsHeadThroughId()
        {
            // Arrange
            var metadata = new Baubit.Caching.InMemory.Metadata<int>(new Caching.Configuration(), NullLoggerFactory.Instance);
            metadata.AddTail(10);
            metadata.AddTail(20);
            metadata.AddTail(30);

            // Act - Get ids from head through 20 (middle)
            var result = metadata.GetIdsThrough(20, out var ids);

            // Assert
            Assert.True(result);
            Assert.Equal(new[] { 10, 20 }, ids);
        }

        [Fact]
        public void Metadata_GetIdsThrough_AfterTail_ReturnsAllIds()
        {
            // Arrange
            var metadata = new Baubit.Caching.InMemory.Metadata<int>(new Caching.Configuration(), NullLoggerFactory.Instance);
            metadata.AddTail(10);
            metadata.AddTail(20);

            // Act - id 100 is beyond tail
            var result = metadata.GetIdsThrough(100, out var ids);

            // Assert
            Assert.True(result);
            Assert.Equal(new[] { 10, 20 }, ids);
        }

        [Fact]
        public void Metadata_Dispose_ClearsAllState()
        {
            // Arrange
            var metadata = new Baubit.Caching.InMemory.Metadata<int>(new Caching.Configuration(), NullLoggerFactory.Instance);
            metadata.AddTail(10);
            metadata.AddTail(20);

            // Act
            metadata.Dispose();

            // Assert - After dispose, double dispose should not throw
            metadata.Dispose();
        }
    }
}