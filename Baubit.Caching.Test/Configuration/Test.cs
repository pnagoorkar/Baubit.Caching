namespace Baubit.Caching.Test.Configuration
{
    /// <summary>
    /// Tests for <see cref="Baubit.Caching.Configuration"/>
    /// </summary>
    public class Test
    {
        [Fact]
        public void Configuration_DefaultValues_AreSetCorrectly()
        {
            // Arrange & Act
            var config = new Caching.Configuration();

            // Assert
            Assert.False(config.RunAdaptiveResizing);
            Assert.Equal(2_000, config.AdaptionWindowMS);
            Assert.Equal(64, config.GrowStep);
            Assert.Equal(32, config.ShrinkStep);
            Assert.Equal(1, config.RoomRateLowerLimit);
            Assert.Equal(5, config.RoomRateUpperLimit);
            Assert.Equal(100, config.EvictAfterEveryX);
        }

        [Fact]
        public void Configuration_CanBeCustomized()
        {
            // Arrange & Act
            var config = new Caching.Configuration
            {
                RunAdaptiveResizing = true,
                AdaptionWindowMS = 5_000,
                GrowStep = 128,
                ShrinkStep = 64,
                RoomRateLowerLimit = 2,
                RoomRateUpperLimit = 10,
                EvictAfterEveryX = 200
            };

            // Assert
            Assert.True(config.RunAdaptiveResizing);
            Assert.Equal(5_000, config.AdaptionWindowMS);
            Assert.Equal(128, config.GrowStep);
            Assert.Equal(64, config.ShrinkStep);
            Assert.Equal(2, config.RoomRateLowerLimit);
            Assert.Equal(10, config.RoomRateUpperLimit);
            Assert.Equal(200, config.EvictAfterEveryX);
        }

        [Theory]
        [InlineData(0, 32, 16, 0.5, 3, 50)]
        [InlineData(10_000, 256, 128, 0.1, 100, 1000)]
        public void Configuration_SupportsVariousValues(int windowMs, int growStep, int shrinkStep,
            double lowerLimit, double upperLimit, int evictX)
        {
            // Arrange & Act
            var config = new Caching.Configuration
            {
                AdaptionWindowMS = windowMs,
                GrowStep = growStep,
                ShrinkStep = shrinkStep,
                RoomRateLowerLimit = lowerLimit,
                RoomRateUpperLimit = upperLimit,
                EvictAfterEveryX = evictX
            };

            // Assert
            Assert.Equal(windowMs, config.AdaptionWindowMS);
            Assert.Equal(growStep, config.GrowStep);
            Assert.Equal(shrinkStep, config.ShrinkStep);
            Assert.Equal(lowerLimit, config.RoomRateLowerLimit);
            Assert.Equal(upperLimit, config.RoomRateUpperLimit);
            Assert.Equal(evictX, config.EvictAfterEveryX);
        }
    }
}