namespace Baubit.Caching
{
    /// <summary>
    /// Configuration options for the Baubit.Caching library, including adaptive resizing and eviction policies.
    /// </summary>
    public class Configuration : Baubit.Configuration.Configuration
    {
        /// <summary>
        /// Gets or sets whether adaptive resizing is enabled for the L1 cache store.
        /// When enabled, the cache periodically adjusts L1 capacity based on production rate.
        /// Default is <c>false</c>.
        /// </summary>
        public bool RunAdaptiveResizing { get; set; } = false;
        /// <summary>
        /// Gets or sets the window size in milliseconds for measuring production rate during adaptation.
        /// Default is 2,000 ms (2 seconds).
        /// </summary>
        public int AdaptionWindowMS { get; set; } = 2_000;
        /// <summary>
        /// Gets or sets the number of entries to grow the L1 cache capacity by when production rate exceeds <see cref="RoomRateUpperLimit"/>.
        /// Default is 64 entries.
        /// </summary>
        public int GrowStep { get; set; } = 64;
        /// <summary>
        /// Gets or sets the number of entries to shrink the L1 cache capacity by when production rate falls below <see cref="RoomRateLowerLimit"/>.
        /// Default is 32 entries.
        /// </summary>
        public int ShrinkStep { get; set; } = 32;
        /// <summary>
        /// Gets or sets the lower threshold for room rate (rooms per second) below which the cache shrinks.
        /// Default is 1.0 rooms/second.
        /// </summary>
        public double RoomRateLowerLimit { get; set; } = 1;
        /// <summary>
        /// Gets or sets the upper threshold for room rate (rooms per second) above which the cache grows.
        /// Default is 5.0 rooms/second.
        /// </summary>
        public double RoomRateUpperLimit { get; set; } = 5;
        /// <summary>
        /// Gets or sets the number of additions to the cache after which automatic eviction is triggered.
        /// Eviction removes entries that have been read by all active enumerators.
        /// Default is 100 additions.
        /// </summary>
        public int EvictAfterEveryX { get; set; } = 100;
    }
}
