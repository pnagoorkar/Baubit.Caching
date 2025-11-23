using Baubit.Caching.InMemory;

namespace Baubit.Caching.Test.InMemory.Entry
{
    /// <summary>
    /// Tests for <see cref="Baubit.Caching.InMemory.Entry{TValue}"/>
    /// </summary>
    public class Test
    {
        [Fact]
        public void Entry_Constructor_SetsProperties()
        {
            // Arrange
            var id = Guid.NewGuid();
            var value = "test value";

            // Act
            var entry = new Entry<string>(id, value);

            // Assert
            Assert.Equal(id, entry.Id);
            Assert.Equal(value, entry.Value);
            Assert.NotEqual(default(DateTime), entry.CreatedOnUTC);
            Assert.True(entry.CreatedOnUTC <= DateTime.UtcNow);
        }

        [Fact]
        public void Entry_WithIntValue_StoresCorrectly()
        {
            // Arrange
            var id = Guid.NewGuid();
            var value = 42;

            // Act
            var entry = new Entry<int>(id, value);

            // Assert
            Assert.Equal(id, entry.Id);
            Assert.Equal(value, entry.Value);
        }

        [Fact]
        public void Entry_WithNullValue_AllowsNull()
        {
            // Arrange
            var id = Guid.NewGuid();
            string? value = null;

            // Act
            var entry = new Entry<string?>(id, value);

            // Assert
            Assert.Equal(id, entry.Id);
            Assert.Null(entry.Value);
        }

        [Fact]
        public void Entry_WithComplexType_StoresCorrectly()
        {
            // Arrange
            var id = Guid.NewGuid();
            var value = new { Name = "Test", Count = 123 };

            // Act
            var entry = new Entry<object>(id, value);

            // Assert
            Assert.Equal(id, entry.Id);
            Assert.Equal(value, entry.Value);
        }

        [Fact]
        public void Entry_CreatedOnUTC_IsCloseToNow()
        {
            // Arrange
            var before = DateTime.UtcNow;
            var id = Guid.NewGuid();

            // Act
            var entry = new Entry<string>(id, "test");
            var after = DateTime.UtcNow;

            // Assert
            Assert.True(entry.CreatedOnUTC >= before);
            Assert.True(entry.CreatedOnUTC <= after);
        }

        [Fact]
        public void Entry_MultipleEntries_HaveDifferentTimestamps()
        {
            // Arrange & Act
            var entry1 = new Entry<string>(Guid.NewGuid(), "test1");
            Thread.Sleep(5); // Small delay to ensure different timestamp
            var entry2 = new Entry<string>(Guid.NewGuid(), "test2");

            // Assert
            Assert.NotEqual(entry1.Id, entry2.Id);
            Assert.True(entry2.CreatedOnUTC >= entry1.CreatedOnUTC);
        }
    }
}
