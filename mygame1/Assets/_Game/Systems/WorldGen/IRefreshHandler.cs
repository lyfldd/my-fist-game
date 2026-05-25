namespace _Game.Systems.WorldGen
{
    public interface IRefreshHandler
    {
        string handlerName { get; }
        void OnChunkLoad(int chunkId, float currentGameDay);
        void OnChunkUnload(int chunkId);
    }
}
