using Baubit.Caching.InMemory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Baubit.Caching.Test.InMemory.Store
{
    /// <summary>
    /// Tests for <see cref="Baubit.Caching.InMemory.Store{TValue}"/>
    /// </summary>
    public class Test
    {
        private readonly ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;

        [Fact]
        public void Store_Constructor_UncappedStore()
        {
            // Arrange & Act
            var store = new Caching.InMemory.Store<string>(Baubit.Identity.IdentityGenerator.CreateNew(), _loggerFactory);

            // Assert
            Assert.True(store.Uncapped);
            Assert.Null(store.MinCapacity);
            Assert.Null(store.MaxCapacity);
            Assert.Null(store.TargetCapacity);
            Assert.Null(store.CurrentCapacity);
            Assert.True(store.HasCapacity);
        }

        [Fact]
        public void Store_Constructor_WithCapacity()
        {
            // Arrange & Act
            var store = new Caching.InMemory.Store<string>(10, 100, null, _loggerFactory);

            // Assert
            Assert.False(store.Uncapped);
            Assert.Equal(10, store.MinCapacity);
            Assert.Equal(100, store.MaxCapacity);
            Assert.Equal(10, store.TargetCapacity);
            Assert.Equal(10, store.CurrentCapacity);
            Assert.True(store.HasCapacity);
        }

        [Fact]
        public void Store_Add_Entry_Success()
        {
            // Arrange
            var store = new Caching.InMemory.Store<string>(Baubit.Identity.IdentityGenerator.CreateNew(), _loggerFactory);
            var id = Guid.NewGuid();
            var entry = new Entry<string>(id, "test");

            // Act
            var result = store.Add(entry);

            // Assert
            Assert.True(result);
            Assert.True(store.GetEntryOrDefault(id, out var retrieved));
            Assert.NotNull(retrieved);
            Assert.Equal(id, retrieved.Id);
            Assert.Equal("test", retrieved.Value);
        }

        [Fact]
        public void Store_Add_WithIdAndValue_Success()
        {
            // Arrange
            var store = new Caching.InMemory.Store<int>(Baubit.Identity.IdentityGenerator.CreateNew(), _loggerFactory);
            var id = Guid.NewGuid();

            // Act
            var result = store.Add(id, 42, out var entry);

            // Assert
            Assert.True(result);
            Assert.NotNull(entry);
            Assert.Equal(id, entry.Id);
            Assert.Equal(42, entry.Value);
        }

        [Fact]
        public void Store_Add_WhenCapacityExceeded_Fails()
        {
            // Arrange
            var store = new Caching.InMemory.Store<string>(2, 2, null, _loggerFactory);
            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();
            var id3 = Guid.NewGuid();

            // Act
            var result1 = store.Add(id1, "first", out _);
            var result2 = store.Add(id2, "second", out _);
            var result3 = store.Add(id3, "third", out _);

            // Assert
            Assert.True(result1);
            Assert.True(result2);
            Assert.False(result3); // Should fail due to capacity
            Assert.False(store.HasCapacity);
        }

        [Fact]
        public void Store_Add_DuplicateId_Fails()
        {
            // Arrange
            var store = new Caching.InMemory.Store<string>(Baubit.Identity.IdentityGenerator.CreateNew(), _loggerFactory);
            var id = Guid.NewGuid();

            // Act
            var result1 = store.Add(id, "first", out _);
            var result2 = store.Add(id, "second", out _);

            // Assert
            Assert.True(result1);
            Assert.False(result2); // Should fail due to duplicate
        }

        [Fact]
        public void Store_Add_WithValueOnly_AutoGeneratesId_Success()
        {
            // Arrange
            var store = new Caching.InMemory.Store<string>(Baubit.Identity.IdentityGenerator.CreateNew(), _loggerFactory);

            // Act
            var result = store.Add("test value", out var entry);

            // Assert
            Assert.True(result);
            Assert.NotNull(entry);
            Assert.NotEqual(Guid.Empty, entry.Id);
            Assert.Equal("test value", entry.Value);
            Assert.NotEqual(default(DateTime), entry.CreatedOnUTC);
        }

        [Fact]
        public void Store_Add_WithValueOnly_MultipleEntries_GeneratesMonotonicIds()
        {
            // Arrange
            var store = new Caching.InMemory.Store<int>(Baubit.Identity.IdentityGenerator.CreateNew(), _loggerFactory);

            // Act
            store.Add(1, out var entry1);
            store.Add(2, out var entry2);
            store.Add(3, out var entry3);

            // Assert
            Assert.True(entry1.Id.CompareTo(entry2.Id) < 0);
            Assert.True(entry2.Id.CompareTo(entry3.Id) < 0);
            Assert.Equal(1, entry1.Value);
            Assert.Equal(2, entry2.Value);
            Assert.Equal(3, entry3.Value);
        }

        [Fact]
        public void Store_Add_WithValueOnly_WhenCapacityExceeded_Fails()
        {
            // Arrange
            var store = new Caching.InMemory.Store<string>(2, 2, Baubit.Identity.IdentityGenerator.CreateNew(), _loggerFactory);

            // Act
            var result1 = store.Add("first", out _);
            var result2 = store.Add("second", out _);
            var result3 = store.Add("third", out _);

            // Assert
            Assert.True(result1);
            Assert.True(result2);
            Assert.False(result3); // Should fail due to capacity
        }

        [Fact]
        public void Store_Add_WithValueOnly_NoIdentityGenerator_Fails()
        {
            // Arrange - L1 store without identity generator
            var store = new Caching.InMemory.Store<string>(10, 100, null, _loggerFactory);

            // Act
            var result = store.Add("test value", out var entry);

            // Assert
            Assert.False(result);
            Assert.Null(entry);
        }

        [Fact]
        public void Store_GetEntryOrDefault_ExistingId_ReturnsEntry()
        {
            // Arrange
            var store = new Caching.InMemory.Store<string>(Baubit.Identity.IdentityGenerator.CreateNew(), _loggerFactory);
            var id = Guid.NewGuid();
            store.Add(id, "test value", out _);

            // Act
            var result = store.GetEntryOrDefault(id, out var entry);

            // Assert
            Assert.True(result);
            Assert.NotNull(entry);
            Assert.Equal("test value", entry.Value);
        }

        [Fact]
        public void Store_GetEntryOrDefault_NonExistingId_ReturnsFalse()
        {
            // Arrange
            var store = new Caching.InMemory.Store<string>(Baubit.Identity.IdentityGenerator.CreateNew(), _loggerFactory);
            var id = Guid.NewGuid();

            // Act
            var result = store.GetEntryOrDefault(id, out var entry);

            // Assert
            Assert.False(result);
            Assert.Null(entry);
        }

        [Fact]
        public void Store_GetValueOrDefault_ExistingId_ReturnsValue()
        {
            // Arrange
            var store = new Caching.InMemory.Store<int>(Baubit.Identity.IdentityGenerator.CreateNew(), _loggerFactory);
            var id = Guid.NewGuid();
            store.Add(id, 123, out _);

            // Act
            var result = store.GetValueOrDefault(id, out var value);

            // Assert
            Assert.True(result);
            Assert.Equal(123, value);
        }

        [Fact]
        public void Store_GetValueOrDefault_NonExistingId_ReturnsDefault()
        {
            // Arrange
            var store = new Caching.InMemory.Store<string>(Baubit.Identity.IdentityGenerator.CreateNew(), _loggerFactory);
            var id = Guid.NewGuid();

            // Act
            var result = store.GetValueOrDefault(id, out var value);

            // Assert
            Assert.False(result);
            Assert.Null(value);
        }

        [Fact]
        public void Store_Update_ExistingEntry_Success()
        {
            // Arrange
            var store = new Caching.InMemory.Store<string>(Baubit.Identity.IdentityGenerator.CreateNew(), _loggerFactory);
            var id = Guid.NewGuid();
            store.Add(id, "original", out _);

            // Act
            var result = store.Update(id, "updated");

            // Assert
            Assert.True(result);
            store.GetValueOrDefault(id, out var value);
            Assert.Equal("updated", value);
        }

        [Fact]
        public void Store_Update_WithEntry_Success()
        {
            // Arrange
            var store = new Caching.InMemory.Store<int>(Baubit.Identity.IdentityGenerator.CreateNew(), _loggerFactory);
            var id = Guid.NewGuid();
            store.Add(id, 10, out _);
            var updatedEntry = new Entry<int>(id, 20);

            // Act
            var result = store.Update(updatedEntry);

            // Assert
            Assert.True(result);
            store.GetValueOrDefault(id, out var value);
            Assert.Equal(20, value);
        }

        [Fact]
        public void Store_Update_NonExistingEntry_Fails()
        {
            // Arrange
            var store = new Caching.InMemory.Store<string>(Baubit.Identity.IdentityGenerator.CreateNew(), _loggerFactory);
            var id = Guid.NewGuid();
            var entry = new Entry<string>(id, "test");

            // Act
            var result = store.Update(entry);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Store_Remove_ExistingEntry_Success()
        {
            // Arrange
            var store = new Caching.InMemory.Store<string>(Baubit.Identity.IdentityGenerator.CreateNew(), _loggerFactory);
            var id = Guid.NewGuid();
            store.Add(id, "test", out _);

            // Act
            var result = store.Remove(id, out var removed);

            // Assert
            Assert.True(result);
            Assert.NotNull(removed);
            Assert.Equal("test", removed.Value);
            Assert.False(store.GetEntryOrDefault(id, out _));
        }

        [Fact]
        public void Store_Remove_NonExistingEntry_Fails()
        {
            // Arrange
            var store = new Caching.InMemory.Store<string>(Baubit.Identity.IdentityGenerator.CreateNew(), _loggerFactory);
            var id = Guid.NewGuid();

            // Act
            var result = store.Remove(id, out var removed);

            // Assert
            Assert.False(result);
            Assert.Null(removed);
        }

        [Fact]
        public void Store_GetCount_ReturnsCorrectCount()
        {
            // Arrange
            var store = new Caching.InMemory.Store<string>(Baubit.Identity.IdentityGenerator.CreateNew(), _loggerFactory);
            store.Add(Guid.NewGuid(), "first", out _);
            store.Add(Guid.NewGuid(), "second", out _);

            // Act
            var result = store.GetCount(out var count);

            // Assert
            Assert.True(result);
            Assert.Equal(2, count);
        }

        [Fact]
        public void Store_AddCapacity_IncreasesCapacity()
        {
            // Arrange
            var store = new Caching.InMemory.Store<string>(10, 100, null, _loggerFactory);
            var initialCapacity = store.TargetCapacity;

            // Act
            var result = store.AddCapacity(20);

            // Assert
            Assert.True(result);
            Assert.Equal(initialCapacity + 20, store.TargetCapacity);
        }

        [Fact]
        public void Store_AddCapacity_RespectsMaxCapacity()
        {
            // Arrange
            var store = new Caching.InMemory.Store<string>(10, 100, null, _loggerFactory);

            // Act
            var result = store.AddCapacity(200);

            // Assert
            Assert.True(result);
            Assert.Equal(100, store.TargetCapacity); // Should not exceed max
        }

        [Fact]
        public void Store_CutCapacity_DecreasesCapacity()
        {
            // Arrange
            var store = new Caching.InMemory.Store<string>(10, 100, null, _loggerFactory);
            store.AddCapacity(40); // Set to 50
            var beforeCut = store.TargetCapacity;

            // Act
            var result = store.CutCapacity(20);

            // Assert
            Assert.True(result);
            Assert.Equal(beforeCut - 20, store.TargetCapacity);
        }

        [Fact]
        public void Store_CutCapacity_RespectsMinCapacity()
        {
            // Arrange
            var store = new Caching.InMemory.Store<string>(10, 100, null, _loggerFactory);

            // Act
            var result = store.CutCapacity(20);

            // Assert
            Assert.True(result);
            Assert.Equal(10, store.TargetCapacity); // Should not go below min
        }

        [Fact]
        public void Store_Dispose_ClearsData()
        {
            // Arrange
            var store = new Caching.InMemory.Store<string>(Baubit.Identity.IdentityGenerator.CreateNew(), _loggerFactory);
            store.Add(Guid.NewGuid(), "test", out _);

            // Act
            store.Dispose();

            // Assert
            store.GetCount(out var count);
            Assert.Equal(0, count);
        }

        [Fact]
        public void Store_CurrentCapacity_UpdatesAfterAddAndRemove()
        {
            // Arrange
            var store = new Caching.InMemory.Store<string>(5, 10, null, _loggerFactory);
            Assert.Equal(5, store.CurrentCapacity);

            // Act - Add entries
            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();
            store.Add(id1, "first", out _);
            store.Add(id2, "second", out _);

            // Assert after add
            Assert.Equal(3, store.CurrentCapacity);

            // Act - Remove entry
            store.Remove(id1, out _);

            // Assert after remove
            Assert.Equal(4, store.CurrentCapacity);
        }
    }
}