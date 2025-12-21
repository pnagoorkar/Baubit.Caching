namespace Baubit.Caching
{
    /// <summary>
    /// Configuration options for the Baubit.Caching library, including adaptive resizing and eviction policies.
    /// </summary>
    public class Configuration : Baubit.Configuration.Configuration
    {
        /// <summary>
        /// Gets or sets whether adaptive resizing is enabled for the cache.
        /// </summary>
        public bool RunAdaptiveResizing { get; set; } = false;
        /// <summary>
        /// Gets or sets the window size in milliseconds for adaptation.
        /// </summary>
        public int AdaptionWindowMS { get; set; } = 2_000;
        /// <summary>
        /// Gets or sets the number of entries to grow the cache by during adaptation.
        /// </summary>
        public int GrowStep { get; set; } = 64;
        /// <summary>
        /// Gets or sets the number of entries to shrink the cache by during adaptation.
        /// </summary>
        public int ShrinkStep { get; set; } = 32;
        /// <summary>
        /// Gets or sets the lower limit for the room rate used in adaptation.
        /// </summary>
        public double RoomRateLowerLimit { get; set; } = 1;
        /// <summary>
        /// Gets or sets the upper limit for the room rate used in adaptation.
        /// </summary>
        public double RoomRateUpperLimit { get; set; } = 5;
        /// <summary>
        /// Gets or sets the number of additions after which eviction is triggered.
        /// </summary>
        public int EvictAfterEveryX { get; set; } = 100;
    }
}
