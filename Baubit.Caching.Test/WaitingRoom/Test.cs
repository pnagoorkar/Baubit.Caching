namespace Baubit.Caching.Test.WaitingRoom
{
    /// <summary>
    /// Tests for <see cref="Baubit.Caching.WaitingRoom{TValue}"/>
    /// </summary>
    public class Test
    {
        [Fact]
        public void WaitingRoom_HasGuests_InitiallyFalse()
        {
            // Arrange & Act
            using var room = new Caching.WaitingRoom<string>();

            // Assert
            Assert.False(room.HasGuests);
        }

        [Fact]
        public async Task WaitingRoom_Join_WithoutResult_Waits()
        {
            // Arrange
            using var room = new Caching.WaitingRoom<string>();
            var cts = new CancellationTokenSource(100); // Short timeout

            // Act & Assert
            await Assert.ThrowsAsync<TaskCanceledException>(async () =>
            {
                await room.Join(cts.Token);
            });
        }

        [Fact]
        public async Task WaitingRoom_TrySetResult_CompletesJoin()
        {
            // Arrange
            using var room = new Caching.WaitingRoom<string>();
            var expectedValue = "test result";

            // Act
            var joinTask = room.Join();
            var setResult = room.TrySetResult(expectedValue);
            var actualValue = await joinTask;

            // Assert
            Assert.True(setResult);
            Assert.Equal(expectedValue, actualValue);
        }

        [Fact]
        public async Task WaitingRoom_MultipleGuests_AllGetResult()
        {
            // Arrange
            using var room = new Caching.WaitingRoom<int>();
            var expectedValue = 42;

            // Act
            var task1 = room.Join();
            var task2 = room.Join();
            var task3 = room.Join();

            Assert.True(room.HasGuests);

            room.TrySetResult(expectedValue);

            var result1 = await task1;
            var result2 = await task2;
            var result3 = await task3;

            // Assert
            Assert.Equal(expectedValue, result1);
            Assert.Equal(expectedValue, result2);
            Assert.Equal(expectedValue, result3);
        }

        [Fact]
        public async Task WaitingRoom_TrySetCanceled_CancelsJoin()
        {
            // Arrange
            using var room = new Caching.WaitingRoom<string>();

            // Act
            var joinTask = room.Join();
            var setCanceled = room.TrySetCanceled();

            // Assert
            Assert.True(setCanceled);
            await Assert.ThrowsAsync<TaskCanceledException>(async () => await joinTask);
        }

        [Fact]
        public async Task WaitingRoom_JoinWithCancelledToken_ReturnsCancelled()
        {
            // Arrange
            using var room = new Caching.WaitingRoom<string>();
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAsync<TaskCanceledException>(async () =>
            {
                await room.Join(cts.Token);
            });
        }

        [Fact]
        public async Task WaitingRoom_Dispose_CancelsWaitingTasks()
        {
            // Arrange
            var room = new Caching.WaitingRoom<string>();
            var joinTask = room.Join();

            // Act
            room.Dispose();
            await Task.Delay(10); // Give it a moment to process

            // Assert
            Assert.True(joinTask.IsCanceled || joinTask.IsFaulted || joinTask.IsCompleted);
        }

        [Fact]
        public async Task WaitingRoom_HasGuests_ReflectsJoinedCount()
        {
            // Arrange
            using var room = new Caching.WaitingRoom<string>();

            // Act & Assert - No guests initially
            Assert.False(room.HasGuests);

            // Start a join
            var task = room.Join();

            // Give it a moment to register
            await Task.Delay(10);
            Assert.True(room.HasGuests);

            // Complete the join
            room.TrySetResult("done");
            await task;

            // Give it a moment to unregister
            await Task.Delay(10);
            Assert.False(room.HasGuests);
        }

        [Fact]
        public async Task WaitingRoom_TrySetResult_TwiceFails()
        {
            // Arrange
            using var room = new Caching.WaitingRoom<string>();

            // Act
            var task = room.Join();
            var first = room.TrySetResult("first");
            var second = room.TrySetResult("second");
            var result = await task;

            // Assert
            Assert.True(first);
            Assert.False(second); // Second attempt should fail
            Assert.Equal("first", result);
        }

        [Fact]
        public async Task WaitingRoom_WithCancellationToken_PropagatesCancellation()
        {
            // Arrange
            using var room = new Caching.WaitingRoom<int>();
            var cts = new CancellationTokenSource();
            var joinTask = room.Join(cts.Token);

            // Act
            cts.Cancel();
            await Task.Delay(10); // Give it a moment to process

            // Assert
            Assert.True(joinTask.IsCanceled || joinTask.IsFaulted || joinTask.IsCompleted);
        }
    }
}