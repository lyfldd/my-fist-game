---
name: import-3d-model
description: 从 D:\游戏模型\ 导入 3D 模型（武器/建筑/车辆/角色），自动完成贴图重命名、目录迁移、材质生成、Humanoid骨骼配置、动画导入、Git 提交
---

# 3D 模型导入工作流

## 项目环境（硬编码，不可更改）

- **Unity 项目路径**: `C:\Users\Administrator\Desktop\Unity学习一\mygame\mygame1`
- **模型存放根路径**: `Assets/_Game/Config/Models/`
- **渲染管线**: 内置渲染管线，材质固定 Shader: `Standard`
- **外部模型素材总目录**: `D:\游戏模型\`
- **核心硬性规则**: 所有贴图必须和 FBX 放在同一文件夹，禁止单独放入 Materials 文件夹，分离会导致材质丢失色彩

## 四大模型分类目录规范

### 1. 武器 Weapon
- FBX 路径: `Assets/_Game/Config/Models/Weapons/{模型英文名}.fbx`
- 贴图同目录存放（与 FBX 同级）

### 2. 建筑 Building
- 分类: `Commercial` (商铺) / `Residential` (民居) / `Industrial` (工厂) / `Suburban` (废墟乡村) / `Roads` (道路)
- FBX 路径: `Assets/_Game/Config/Models/Buildings/{分类名}/{模型英文名}/{模型英文名}.fbx`
- 贴图全部放进模型同名子文件夹，与 FBX 同级

### 3. 车辆 Vehicle
- FBX 路径: `Assets/_Game/Config/Models/Vehicles/{模型英文名}.fbx`
- 贴图同目录存放（与 FBX 同级）

### 4. 角色 Character（骨骼+动画）
- 分类: `Player` / `NPC` / `Zombie`
- 目录结构:
  ```
  Characters/{分类}/
    {模型名}.fbx            # 网格 + 骨骼（可含内嵌动画）
    {模型名}.mat            # 材质
    {模型名}.prefab         # 预制体（带 Animator）
    {模型名}.controller     # Animator Controller
    {模型名}_BaseColor.png
    {模型名}_Normal.png
    {模型名}_Metallic.png
    {模型名}_Roughness.png
    Animations/             # 单独导入的动画 fbx
      Walk.fbx
      Run.fbx
      ...
  ```
- NPC/Zombie 下设有 `Shared/` 目录，存放同类型共享的动画或材质
- 角色动画 fbx 文件命名直接用英文动画名（`Idle.fbx`、`Walk.fbx`、`Run.fbx`、`Attack.fbx`、`Death.fbx`），方便 Animator 里识别

## 贴图统一命名规则（Meshy 导出专用）

| 源文件匹配模式 | 重命名为 |
|---|---|
| `Meshy_*_texture.png` | `{模型名}_BaseColor.png` |
| `Meshy_*_texture_normal.png` | `{模型名}_Normal.png` |
| `Meshy_*_texture_metallic.png` | `{模型名}_Metallic.png` |
| `Meshy_*_texture_roughness.png` | `{模型名}_Roughness.png` |

复制完成后删除贴图对应的 `.meta` 缓存文件。

## 材质自动生成
- 同目录下 `{模型名}.mat`，由编辑器脚本自动生成并绑定贴图

---

## 完整导入工作流程

### 步骤 1: 确认信息

向用户确认以下信息（缺少则询问）:
- **模型类型**: 武器 / 建筑 / 车辆 / 角色
- **模型英文名称**: (例: AK47, BrickHouse, PoliceCar, Player)
- **仅武器额外确认**: 网格尺寸 (gridW×gridH)、物品重量

建筑需要额外确认:
- **建筑分类**: Commercial / Residential / Industrial / Suburban / Roads

角色需要额外确认:
- **角色分类**: Player / NPC / Zombie
- **是否含内嵌动画**: 如果 fbx 已包含动画，需要提取 clip 名称

### 步骤 2: 复制 FBX 与贴图

从用户提供的 `D:\游戏模型\` 具体子路径中:
1. 列出该目录下所有文件
2. 复制 FBX 与全部贴图文件到目标 Unity 目录
3. 按照规范目录迁移文件:
   - 武器: `Assets/_Game/Config/Models/Weapons/`
   - 建筑: `Assets/_Game/Config/Models/Buildings/{分类名}/{模型英文名}/` (自动新建子文件夹)
   - 车辆: `Assets/_Game/Config/Models/Vehicles/`
   - 角色: `Assets/_Game/Config/Models/Characters/{分类}/{模型名}/` (按角色分类创建 Animations 子目录)

### 步骤 3: 贴图重命名

对每个复制到目标目录的贴图文件，按 Meshy 规则重命名:

```
源文件                              → 重命名为
Meshy_*_texture.png               → {模型名}_BaseColor.png
Meshy_*_texture_normal.png        → {模型名}_Normal.png
Meshy_*_texture_metallic.png      → {模型名}_Metallic.png
Meshy_*_texture_roughness.png     → {模型名}_Roughness.png
```

重命名后删除贴图对应的 `.meta` 缓存文件（如果已生成）。

### 步骤 4: 创建编辑器导入脚本

根据模型类型生成对应的 Unity 编辑器脚本:

- **武器**: 沿用 `SetupWeaponModel.cs` 模式，创建材质 + 预制体 + 绑定 ItemData
- **建筑**: 沿用 `GhostPreview` 模式，如有需要创建 BuildableData
- **车辆**: 沿用 `CreateVehicleData.cs` 模式
- **角色**: 生成 `Setup{模型名}Character.cs`，包含:
  - Humanoid rig 设置（`ModelImporterAnimationType.Human`）
  - 动画 clip 提取与命名
  - 材质创建（绑定四张贴图到 Standard Shader）
  - Prefab 生成（带 SkinnedMeshRenderer + Animator 组件）

### 步骤 5: 仅角色 — Humanoid + 动画配置

角色模型导入后的特殊处理（写入编辑器脚本或手动告知用户）:

**主模型 FBX（含网格+骨骼）:**
1. Rig 标签 → Animation Type = **Humanoid**
2. Material 标签 → Material Import Mode = **None**（使用外部材质）
3. Animation 标签 → 如有内嵌动画，提取 clip 并命名

**单独动画 FBX（仅骨骼，无网格）:**
1. 放入 `Animations/` 子目录
2. Rig 标签 → Animation Type = Humanoid, Avatar Definition = **Copy From Other Avatar**, Source = 主模型的 Avatar
3. Animation 标签 → clip 重命名为英文，勾选 Loop Time（如需要），点 Apply

**Animator Controller 创建:**
1. 同目录右键 → Create → Animator Controller, 命名 `{模型名}.controller`
2. 将动画 clip 拖入，连线，设过渡条件
3. 拖到 Prefab 的 Animator 组件的 Controller 槽位

### 步骤 6: 仅武器 — 注册物品数据

仅当模型类型为**武器**时，在 `Assets/_Game/Editor/CreateDefaultItems.cs` 的 `CreateWeapons()` 方法中添加:

```csharp
CreateItem("{中文名}", "{英文名}", {gridW}, {gridH}, {weight}f, ItemCategory.Weapon, EquipSlot.RightHand, 0, 0);
```

参数映射:
- `displayName`: 用户提供的中文名
- `fileName`: 模型英文名
- `gridW, gridH`: 用户确认的网格尺寸
- `weight`: 用户确认的重量
- `category`: `ItemCategory.Weapon`
- `equipSlot`: 默认 `EquipSlot.RightHand`（手枪类用 `EquipSlot.SidearmBelt`）

### 步骤 7: Git 提交

```bash
cd "C:\Users\Administrator\Desktop\Unity学习一\mygame\mygame1"
git add -A
git commit -m "导入: {模型名} {模型类型}"
```

### 步骤 8: 告知后续 Unity 操作

导入完成后，提醒用户在 Unity 编辑器中执行对应菜单命令。若编辑器脚本未生成或失效，给出手动操作步骤。

---

## 禁止行为

- 不得更改预设文件夹层级
- 不得自定义贴图名称（严格按 Meshy 规则）
- 不得使用非 Standard 管线材质
- 不得拆分 FBX 与贴图存放位置
- 角色模型不得使用 Generic rig（必须 Humanoid）
- 动画 fbx 的 Avatar 不得新建，必须复用主模型的 Avatar

---

## 使用方式

用户发送格式:
> D:\游戏模型\xxx 文件夹 + 模型类型 + 英文名称 [+ 武器: 网格尺寸 + 重量 | 角色: Player/NPC/Zombie]

收到后按上述流程全自动执行，缺信息则主动询问。
