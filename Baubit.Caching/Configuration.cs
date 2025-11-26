using Baubit.Configuration;

namespace Baubit.Caching
{
    public class Configuration : AConfiguration
    {
        public bool RunAdaptiveResizing { get; set; } = false;
        public int AdaptionWindowMS { get; set; } = 2_000;
        public int GrowStep { get; set; } = 64;
        public int ShrinkStep { get; set; } = 32;
        public double RoomRateLowerLimit { get; set; } = 1;
        public double RoomRateUpperLimit { get; set; } = 5;
        public int EvictAfterEveryX { get; set; } = 100;
    }
}
