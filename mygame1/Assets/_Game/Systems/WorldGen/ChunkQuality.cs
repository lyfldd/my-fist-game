namespace _Game.Systems.WorldGen
{
    public enum ChunkQuality
    {
        Low,
        Medium,
        High
    }

    public static class ChunkQualityConfig
    {
        public static int GetLoadRadius(ChunkQuality quality)
        {
            return quality switch
            {
                ChunkQuality.Low => 1,
                ChunkQuality.High => 3,
                _ => 2
            };
        }

        public static int GetPreloadBaseRadius(ChunkQuality quality)
        {
            return GetLoadRadius(quality) + 1;
        }

        public static int GetUnloadRadius(ChunkQuality quality)
        {
            return GetPreloadBaseRadius(quality) + 1;
        }
    }
}
