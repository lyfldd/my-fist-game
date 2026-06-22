using _Game.Core;

namespace _Game.Systems.SaveLoad
{
    // EventBus 事件结构体定义在 GameEvents.cs 中（保持项目约定）
    // 本文件仅作为存档系统事件结构的文档参考

    // 以下为需要在 GameEvents.cs 中新增的事件（参见 GameEvents.cs 末尾）:

    // === 存档操作 ===
    // public readonly struct RequestSaveGame { public int slotIndex; public bool isAutoSave; }
    // public readonly struct SaveCompleted    { public int slotIndex; public bool success; }
    // public readonly struct LoadCompleted    { public int slotIndex; public bool success; }
    // public readonly struct GameLoadStarted  { public int slotIndex; }

    // === 加载流程控制 ===
    // public readonly struct WorldGenCompleted   { }  // WorldGenerator 管线完毕
    // public readonly struct WorldEntitiesRestored { } // Phase 4 全部 Instantiate 完毕
}
