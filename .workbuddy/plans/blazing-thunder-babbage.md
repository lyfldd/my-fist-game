# L2 交互系统 — 实现计划

## 目标
玩家通过按 E 与世界中的物体交互（打开容器、使用设备等），支持交互提示和搜索进度条。

## 架构

### 1. IInteractable 接口
`_Game/Systems/Interaction/IInteractable.cs`
```csharp
namespace _Game.Systems.Interaction
{
    public interface IInteractable
    {
        string InteractionPrompt { get; }   // "搜索柜子" / "打开门"
        float InteractionTime { get; }      // 搜索耗时（0=瞬间，>0=进度条）
        bool IsInteractable { get; }        // 是否可交互
        void OnInteract(GameObject interactor); // 执行交互
    }
}
```

### 2. PlayerInteraction 组件
`_Game/Systems/Interaction/PlayerInteraction.cs`
- 挂在 Player 上
- Update 中向前发射 OverlapSphere / Raycast
- 检测最近的 IInteractable
- 检测到 → 更新交互提示 UI
- 按 E → 调用 OnInteract
- 支持进度条（按住 E 持续交互）

### 3. 交互提示 UI
- 使用场景中的 World Space Canvas
- 在可交互物体上方显示 "按 E 搜索柜子"
- 在玩家交互组件中控制显隐

### 4. 搜索进度条 UI
- 当 InteractionTime > 0 时显示
- 按住 E 进度条走完 → 执行交互
- 松手或远离 → 进度条重置

### 5. 测试用交互物
`_Game/Systems/Interaction/TestInteractable.cs`
- 一个 Cube，带有 IInteractable 实现
- 靠近显示提示，按 E 输出日志

## 文件清单
| 新建 | `_Game/Systems/Interaction/IInteractable.cs` |
| 新建 | `_Game/Systems/Interaction/PlayerInteraction.cs` |
| 新建 | `_Game/Systems/Interaction/TestInteractable.cs` |
| 修改 | `_Game/Core/GameEvents.cs` → 添加交互事件 |
| 新建 | `_Game/UI/InteractionPromptUI.cs` |

## 实现顺序
1. IInteractable 接口 + GameEvents 交互事件
2. PlayerInteraction 检测组件
3. 交互提示 UI（Canvas + Text）
4. 搜索进度条
5. TestInteractable Cube 验证
