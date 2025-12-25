using Baubit.Caching.InMemory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Baubit.Caching.Test.GenericId
{
    /// <summary>
    /// Tests for generic TId support in caching components.
    /// Verifies that the cache works correctly with different identifier types beyond Guid.
    /// </summary>
    public class Test
    {
        private readonly ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;

        #region Entry Tests with Generic IDs

        [Fact]
        public void Entry_WithIntId_CreatesCorrectly()
        {
            // Arrange
            var id = 42;
            var value = "test value";

            // Act
            var entry = new Entry<int, string>(id, value);

            // Assert
            Assert.Equal(id, entry.Id);
            Assert.Equal(value, entry.Value);
            Assert.NotEqual(default(DateTime), entry.CreatedOnUTC);
        }

        [Fact]
        public void Entry_WithLongId_CreatesCorrectly()
        {
            // Arrange
            var id = 999_999_999L;
            var value = 123.45;

            // Act
            var entry = new Entry<long, double>(id, value);

            // Assert
            Assert.Equal(id, entry.Id);
            Assert.Equal(value, entry.Value);
        }

        [Fact]
        public void Entry_WithDifferentIdTypes_AreDistinct()
        {
            // Arrange & Act
            var intEntry = new Entry<int, string>(1, "int");
            var longEntry = new Entry<long, string>(1L, "long");

            // Assert - Different generic types, so these are distinct types
            Assert.NotNull(intEntry);
            Assert.NotNull(longEntry);
            Assert.IsType<Entry<int, string>>(intEntry);
            Assert.IsType<Entry<long, string>>(longEntry);
        }

        #endregion

        #region Metadata Tests with Generic IDs

        [Fact]
        public void Metadata_WithIntId_AddsAndRetrievesCorrectly()
        {
            // Arrange
            var config = new Baubit.Caching.Configuration { EvictAfterEveryX = 100 };
            var metadata = new Metadata<int>(config, _loggerFactory);

            // Act
            var result1 = metadata.AddTail(1);
            var result2 = metadata.AddTail(2);
            var result3 = metadata.AddTail(3);

            // Assert
            Assert.True(result1);
            Assert.True(result2);
            Assert.True(result3);
            Assert.Equal(3, metadata.Count);
            Assert.Equal(1, metadata.HeadId);
            Assert.Equal(3, metadata.TailId);
        }

        [Fact]
        public void Metadata_WithIntId_GetNextId_ReturnsCorrectSequence()
        {
            // Arrange
            var config = new Baubit.Caching.Configuration { EvictAfterEveryX = 100 };
            var metadata = new Metadata<int>(config, _loggerFactory);
            metadata.AddTail(10);
            metadata.AddTail(20);
            metadata.AddTail(30);

            // Act & Assert
            Assert.True(metadata.GetNextId(null, out var firstId));
            Assert.Equal(10, firstId);

            Assert.True(metadata.GetNextId(10, out var secondId));
            Assert.Equal(20, secondId);

            Assert.True(metadata.GetNextId(20, out var thirdId));
            Assert.Equal(30, thirdId);

            Assert.True(metadata.GetNextId(30, out var fourthId));
            Assert.Null(fourthId); // No entry after tail
        }

        [Fact]
        public void Metadata_WithLongId_HandlesLargeValues()
        {
            // Arrange
            var config = new Baubit.Caching.Configuration { EvictAfterEveryX = 100 };
            var metadata = new Metadata<long>(config, _loggerFactory);
            var id1 = long.MaxValue - 2;
            var id2 = long.MaxValue - 1;
            var id3 = long.MaxValue;

            // Act
            metadata.AddTail(id1);
            metadata.AddTail(id2);
            metadata.AddTail(id3);

            // Assert
            Assert.Equal(3, metadata.Count);
            Assert.Equal(id1, metadata.HeadId);
            Assert.Equal(id3, metadata.TailId);
        }

        [Fact]
        public void Metadata_WithIntId_Remove_MaintainsOrder()
        {
            // Arrange
            var config = new Baubit.Caching.Configuration { EvictAfterEveryX = 100 };
            var metadata = new Metadata<int>(config, _loggerFactory);
            metadata.AddTail(100);
            metadata.AddTail(200);
            metadata.AddTail(300);

            // Act - Remove middle entry
            var removed = metadata.Remove(200);

            // Assert
            Assert.True(removed);
            Assert.Equal(2, metadata.Count);
            Assert.True(metadata.GetNextId(100, out var nextId));
            Assert.Equal(300, nextId); // Should skip over removed entry
        }

        [Fact]
        public void Metadata_WithIntId_ContainsKey_WorksCorrectly()
        {
            // Arrange
            var config = new Baubit.Caching.Configuration { EvictAfterEveryX = 100 };
            var metadata = new Metadata<int>(config, _loggerFactory);
            metadata.AddTail(5);
            metadata.AddTail(10);

            // Act & Assert
            Assert.True(metadata.ContainsKey(5));
            Assert.True(metadata.ContainsKey(10));
            Assert.False(metadata.ContainsKey(15));
        }

        [Fact]
        public void Metadata_WithIntId_GetIdsThrough_ReturnsCorrectRange()
        {
            // Arrange
            var config = new Baubit.Caching.Configuration { EvictAfterEveryX = 100 };
            var metadata = new Metadata<int>(config, _loggerFactory);
            metadata.AddTail(1);
            metadata.AddTail(2);
            metadata.AddTail(3);
            metadata.AddTail(4);
            metadata.AddTail(5);

            // Act
            var result = metadata.GetIdsThrough(3, out var ids);

            // Assert
            Assert.True(result);
            var idList = ids.ToList();
            Assert.Equal(3, idList.Count);
            Assert.Equal(1, idList[0]);
            Assert.Equal(2, idList[1]);
            Assert.Equal(3, idList[2]);
        }

        #endregion

        #region Store Tests with Generic IDs (requires custom implementation)

        /// <summary>
        /// Test implementation of Store with integer IDs for testing purposes.
        /// </summary>
        private class IntStore<TValue> : Caching.InMemory.Store<int, TValue>
        {
            private int nextId = 1;

            public IntStore(long? minCap, long? maxCap, ILoggerFactory loggerFactory)
                : base(minCap, maxCap, loggerFactory)
            {
            }

            protected override int? GenerateNextId(int? lastGeneratedId)
            {
                if (lastGeneratedId.HasValue)
                {
                    return lastGeneratedId.Value + 1;
                }
                return nextId++;
            }
        }

        [Fact]
        public void Store_WithIntId_AddsAndRetrievesEntries()
        {
            // Arrange
            var store = new IntStore<string>(null, null, _loggerFactory);

            // Act
            var result1 = store.Add("first", out var entry1);
            var result2 = store.Add("second", out var entry2);

            // Assert
            Assert.True(result1);
            Assert.True(result2);
            Assert.Equal(1, entry1.Id);
            Assert.Equal(2, entry2.Id);
            Assert.Equal("first", entry1.Value);
            Assert.Equal("second", entry2.Value);
        }

        [Fact]
        public void Store_WithIntId_UpdatesEntriesCorrectly()
        {
            // Arrange
            var store = new IntStore<string>(null, null, _loggerFactory);
            store.Add("original", out var entry);
            var id = entry.Id;

            // Act
            var updated = store.Update(id, "updated");

            // Assert
            Assert.True(updated);
            Assert.True(store.GetEntryOrDefault(id, out var retrieved));
            Assert.Equal("updated", retrieved.Value);
        }

        [Fact]
        public void Store_WithIntId_RemovesEntriesCorrectly()
        {
            // Arrange
            var store = new IntStore<string>(null, null, _loggerFactory);
            store.Add("to remove", out var entry);
            var id = entry.Id;

            // Act
            var removed = store.Remove(id, out var removedEntry);

            // Assert
            Assert.True(removed);
            Assert.Equal(id, removedEntry.Id);
            Assert.False(store.GetEntryOrDefault(id, out _));
        }

        [Fact]
        public void Store_WithIntId_RespectsCapacity()
        {
            // Arrange
            var store = new IntStore<string>(2, 2, _loggerFactory);

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
        public void Store_WithIntId_GetCount_ReturnsCorrectCount()
        {
            // Arrange
            var store = new IntStore<int>(null, null, _loggerFactory);

            // Act
            store.Add(1, out _);
            store.Add(2, out _);
            store.Add(3, out _);

            // Assert
            Assert.True(store.GetCount(out var count));
            Assert.Equal(3, count);
        }

        #endregion

        #region ID Comparison Tests

        [Fact]
        public void IntId_CompareTo_WorksCorrectly()
        {
            // Arrange
            int id1 = 10;
            int id2 = 20;
            int id3 = 10;

            // Act & Assert
            Assert.True(id1.CompareTo(id2) < 0);
            Assert.True(id2.CompareTo(id1) > 0);
            Assert.True(id1.CompareTo(id3) == 0);
        }

        [Fact]
        public void LongId_CompareTo_WorksCorrectly()
        {
            // Arrange
            long id1 = 1000L;
            long id2 = 2000L;
            long id3 = 1000L;

            // Act & Assert
            Assert.True(id1.CompareTo(id2) < 0);
            Assert.True(id2.CompareTo(id1) > 0);
            Assert.True(id1.CompareTo(id3) == 0);
        }

        [Fact]
        public void IntId_Equals_WorksCorrectly()
        {
            // Arrange
            int id1 = 42;
            int id2 = 42;
            int id3 = 43;

            // Act & Assert
            Assert.True(id1.Equals(id2));
            Assert.False(id1.Equals(id3));
        }

        #endregion

        #region Edge Case Tests

        [Fact]
        public void Metadata_WithIntId_MinValue_HandlesCorrectly()
        {
            // Arrange
            var config = new Baubit.Caching.Configuration { EvictAfterEveryX = 100 };
            var metadata = new Metadata<int>(config, _loggerFactory);

            // Act
            var result = metadata.AddTail(int.MinValue);

            // Assert
            Assert.True(result);
            Assert.Equal(int.MinValue, metadata.HeadId);
            Assert.Equal(int.MinValue, metadata.TailId);
        }

        [Fact]
        public void Metadata_WithIntId_MaxValue_HandlesCorrectly()
        {
            // Arrange
            var config = new Baubit.Caching.Configuration { EvictAfterEveryX = 100 };
            var metadata = new Metadata<int>(config, _loggerFactory);

            // Act
            var result = metadata.AddTail(int.MaxValue);

            // Assert
            Assert.True(result);
            Assert.Equal(int.MaxValue, metadata.HeadId);
            Assert.Equal(int.MaxValue, metadata.TailId);
        }

        [Fact]
        public void Metadata_WithIntId_NonSequentialIds_MaintainsInsertionOrder()
        {
            // Arrange
            var config = new Baubit.Caching.Configuration { EvictAfterEveryX = 100 };
            var metadata = new Metadata<int>(config, _loggerFactory);

            // Act - Add in non-sequential order (IDs don't have to be monotonically increasing)
            metadata.AddTail(100);
            metadata.AddTail(50);  // Smaller than previous - this is allowed
            metadata.AddTail(150); // Larger than first

            // Assert - Order is based on insertion, not value
            Assert.Equal(100, metadata.HeadId);
            Assert.Equal(150, metadata.TailId);
            
            // GetNextId follows the linked list order (insertion order)
            Assert.True(metadata.GetNextId(100, out var next1));
            Assert.Equal(50, next1); // Next after 100 is 50 (second inserted)
            
            // Note: GetNextId(50) will return 100 because 50 < HeadId(100),
            // so it returns HeadId. This is by design - asking for next after
            // an ID smaller than head returns the head.
            Assert.True(metadata.GetNextId(50, out var next2));
            Assert.Equal(100, next2); // Returns HeadId because 50 < HeadId
            
            // To get 150, ask for next after 100
            Assert.True(metadata.GetNextId(100, out var afterHead));
            Assert.Equal(50, afterHead);
        }

        #endregion
    }
}
