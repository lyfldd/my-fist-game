using _Game.Config;
using _Game.Systems.Inventory;

namespace _Game.Systems.WorldContainer
{
    public class ContainerRecord
    {
        public int containerId;
        public bool isOpened;
        public float openedGameDay;
        public int chunkId;
        public ContainerLootProfile profile;
        public InventoryContainer container;
    }
}
