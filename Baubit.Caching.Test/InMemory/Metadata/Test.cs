namespace Baubit.Caching.Test.InMemory.Metadata
{
    /// <summary>
    /// Tests for <see cref="Baubit.Caching.InMemory.Metadata"/>
    /// </summary>
    public class Test
    {
        [Fact]
        public void Metadata_Constructor_InitializesEmpty()
        {
            // Arrange & Act
            var metadata = new Caching.InMemory.Metadata();

            // Assert
            Assert.Equal(0, metadata.Count);
            Assert.Null(metadata.HeadId);
            Assert.Null(metadata.TailId);
            Assert.NotNull(metadata.CurrentOrder);
            Assert.NotNull(metadata.IdNodeMap);
        }

        [Fact]
        public void Metadata_AddTail_FirstEntry_SetsHeadAndTail()
        {
            // Arrange
            var metadata = new Caching.InMemory.Metadata();
            metadata.GenerateNextId(out var id);

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
            var metadata = new Caching.InMemory.Metadata();
            metadata.GenerateNextId(out var id1);
            metadata.AddTail(id1);
            metadata.GenerateNextId(out var id2);
            metadata.AddTail(id2);
            metadata.GenerateNextId(out var id3);

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
            var metadata = new Caching.InMemory.Metadata();
            metadata.GenerateNextId(out var id);
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
            var metadata = new Caching.InMemory.Metadata();
            metadata.GenerateNextId(out var id);

            // Act
            var result = metadata.ContainsKey(id);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Metadata_GetNextId_NullId_ReturnsHead()
        {
            // Arrange
            var metadata = new Caching.InMemory.Metadata();
            metadata.GenerateNextId(out var id1);
            metadata.AddTail(id1);
            metadata.GenerateNextId(out var id2);
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
            var metadata = new Caching.InMemory.Metadata();
            metadata.GenerateNextId(out var id1);
            metadata.AddTail(id1);
            metadata.GenerateNextId(out var id2);
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
            var metadata = new Caching.InMemory.Metadata();
            metadata.GenerateNextId(out var id1);
            metadata.AddTail(id1);
            metadata.GenerateNextId(out var id2);
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
            var metadata = new Caching.InMemory.Metadata();
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
            var metadata = new Caching.InMemory.Metadata();
            metadata.GenerateNextId(out var id1);
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

            metadata.GenerateNextId(out var id2);
            metadata.AddTail(id2);

            var nextId = await nextIdTask;

            // Assert
            Assert.Equal(id2, nextId);
        }

        [Fact]
        public async Task Metadata_GetNextIdAsync_WhenNextExists_ReturnsImmediately()
        {
            // Arrange
            var metadata = new Caching.InMemory.Metadata();
            metadata.GenerateNextId(out var id1);
            metadata.AddTail(id1);
            metadata.GenerateNextId(out var id2);
            metadata.AddTail(id2);

            // Act
            var nextId = await metadata.GetNextIdAsync(id1, CancellationToken.None);

            // Assert
            Assert.Equal(id2, nextId);
        }

        [Fact]
        public void Metadata_GenerateNextId_FirstCall_ReturnsNewId()
        {
            // Arrange
            var metadata = new Caching.InMemory.Metadata();

            // Act
            var result = metadata.GenerateNextId(out var id);

            // Assert
            Assert.True(result);
            Assert.NotEqual(Guid.Empty, id);
        }

        [Fact]
        public void Metadata_GenerateNextId_SubsequentCalls_GenerateIncreasingIds()
        {
            // Arrange
            var metadata = new Caching.InMemory.Metadata();
            metadata.GenerateNextId(out var id1);
            metadata.AddTail(id1);

            // Act
            var result = metadata.GenerateNextId(out var id2);

            // Assert
            Assert.True(result);
            Assert.True(id2 > id1);
        }

        [Fact]
        public void Metadata_Remove_ExistingId_Success()
        {
            // Arrange
            var metadata = new Caching.InMemory.Metadata();
            metadata.GenerateNextId(out var id1);
            metadata.AddTail(id1);
            metadata.GenerateNextId(out var id2);
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
            var metadata = new Caching.InMemory.Metadata();
            metadata.GenerateNextId(out var id);

            // Act
            var result = metadata.Remove(id);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Metadata_Remove_LastEntry_SetsHeadAndTailToNull()
        {
            // Arrange
            var metadata = new Caching.InMemory.Metadata();
            metadata.GenerateNextId(out var id);
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
            var metadata = new Caching.InMemory.Metadata();
            metadata.GenerateNextId(out var id);

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
            var metadata = new Caching.InMemory.Metadata();
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
            var metadata = new Caching.InMemory.Metadata();
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
            var metadata = new Caching.InMemory.Metadata();
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
            var metadata = new Caching.InMemory.Metadata { Configuration = new Caching.Configuration { RunAdaptiveResizing = true } };

            // Add first entry
            metadata.GenerateNextId(out var id1);
            metadata.AddTail(id1);

            // Start an async wait (this will join the waiting room)
            var nextIdTask = metadata.GetNextIdAsync(id1, CancellationToken.None);

            // Give the task a moment to register in the waiting room
            await Task.Delay(10);

            // Add second entry while there's a waiter - this should increment room count
            metadata.GenerateNextId(out var id2);
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
            var metadata = new Caching.InMemory.Metadata();
            metadata.GenerateNextId(out var id);
            metadata.AddTail(id);

            // Act
            metadata.Dispose();

            // Assert - Cannot access properties after dispose
            Assert.Null(metadata.Configuration);
            Assert.Null(metadata.CurrentOrder);
            Assert.Null(metadata.HeadId);
            Assert.Null(metadata.IdNodeMap);
            Assert.Null(metadata.TailId);
            Assert.Throws<NullReferenceException>(() => metadata.Count);
        }

        [Fact]
        public void Metadata_GetNextId_DeletedMiddleId_ReturnsNextBiggestId()
        {
            // Arrange - Tests line 62: out-of-order deletion scenario
            // When an id is not in IdNodeMap but is between head and tail
            var metadata = new Caching.InMemory.Metadata();
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
            var metadata = new Caching.InMemory.Metadata();
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
            var metadata = new Caching.InMemory.Metadata();
            var someId = Guid.NewGuid();

            // Act
            var result = metadata.GetNextId(someId, out var nextId);

            // Assert
            Assert.True(result);
            Assert.Null(nextId);
        }
    }
}