# 🖥️ 耐久全局 UI — 设计文档

> **日期**: 2026-06-23 · **状态**: 设计定稿 · **依赖**: 耐久 v1.0 + 前置 H/G/F
> 🔍 迭代追踪 | 第1轮: ⬜ | 第2轮: ⬜ | 第3轮: ⬜ | 最后审查: —

> 设计哲学：玩家不需要记住每个东西的耐久——看就完了。准星对准什么，屏幕上就显示什么。

---

## 一、覆盖范围

| 实体 | 耐久来源 | 显示时机 | 显示方式 |
|------|----------|----------|----------|
| **建造物**（墙/门/路障/家具/工作台/工业） | `PlacedStructure._currentHealth` | 准星对准建造物 | 屏幕中上方浮一条血条（ScreenOverlay） |
| **生产设备** | `ProductionDevice._currentDurability` | 打开设备交互面板 | 面板内耐久条 |
| **AIBot 机器人** | `AIBot.currentHP` + 武器耐久 | 打开 AIBot 管理面板 | 面板内血量条 + 武器图标耐久条 |
| **车辆** | `VehicleController._currentHealth` | 驾驶中 + 准星对准车辆 | HUD 血量条（已有 TopLeftHUD 扩展） |
| **玩家装备** | `PlacedItem.itemDurability` | 始终（装备槽+快捷栏） | ✅ 前置K 已实现 |
| **地面物品** | `WorldItem.itemDurability` | 准星对准地面物品 | 物品标签旁小耐久条 |

---

## 二、实现方式

### 2.1 准星检测统一入口

新建 `DurabilityHUD.cs`（挂到 Player 上，由 `PreconfigureUI` 预建 Canvas）：

```csharp
public class DurabilityHUD : MonoBehaviour
{
    public Image targetHealthBar;       // 屏幕中上方血条 fill
    public Text targetNameText;         // 物品/建造物名称
    
    void Update()
    {
        // 准星射线检测
        var hit = GetCrosshairTarget();
        if (hit != null)
        {
            var dur = hit.GetComponent<IDurabilityTarget>();  // 统一接口
            if (dur != null)
            {
                float ratio = dur.DurabilityRatio;
                ShowBar(dur.DisplayName, ratio);
                return;
            }
        }
        HideBar();
    }
}
```

### 2.2 IDurabilityTarget 接口

所有有耐久的实体实现此接口：

```csharp
public interface IDurabilityTarget
{
    float DurabilityRatio { get; }   // 0~1
    string DisplayName { get; }
}
```

PlacedStructure、ProductionDevice、AIBot、VehicleController 各自实现。

### 2.3 面板内耐久条

- 生产设备面板 → 现有 ProductionDeviceUI 加一行耐久条
- AIBot 管理面板 → 现有 AIBotUI 加血量条 + 武器耐久条
- 车辆驾驶 HUD → 现有 TopLeftHUD 扩展血量条

---

## 三、文件清单

| 文件 | 动作 | 说明 |
|------|:--:|------|
| `UI/DurabilityHUD.cs` | 新建 | 准星检测 + 浮条显示 |
| `Core/IDurabilityTarget.cs` | 新建 | 统一接口 |
| `Editor/PreconfigureUI.cs` | 改 | 预建 DurabilityHUD_Canvas |
| `Systems/World/PlacedStructure.cs` | 改 | 实现 IDurabilityTarget |
| `Systems/Crafting/ProductionDevice.cs` | 改 | 实现 IDurabilityTarget |
| `Systems/AIBot/AIBot.cs` | 改 | 实现 IDurabilityTarget |
| `Systems/Vehicle/VehicleController.cs` | 改 | 实现 IDurabilityTarget |
| `UI/ProductionDeviceUI.cs` | 改 | +耐久条 |
| `UI/AIBotUI.cs` | 改 | +血量条+武器耐久条 |
| `UI/TopLeftHUD.cs` | 改 | +车辆血量条 |

---

## 四、依赖关系

```
前置K (玩家装备UI) ✅ 已完成
       │
       ▼
前置H (建造物) ──┐
前置G (生产设备) ─┼──→ 耐久全局UI (本系统)
前置F (AIBot)  ──┤
前置E (车辆)   ──┘
```

**执行时机**：前置 H/G/F/E 全部完成后，最后统一做。

---

## 📜 版本历史

| 版本 | 日期 | 说明 |
|------|------|------|
| `v1.0` | 2026-06-23 | 设计定稿：IDurabilityTarget 统一接口 + 准星检测 + 面板内耐久条 |
