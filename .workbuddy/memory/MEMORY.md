---
summary: "curated long-term memory for mygame project"
---

# MEMORY.md

## 用户偏好

- **语言**: 中文
- **引擎**: 团结引擎 1.8.5（基于 Unity 2022 LTS）
- **Unity API**: 用 `Rigidbody.velocity`（非 `linearVelocity`）
- **游戏**: 3D 世界 + 45° 俯视摄像机，僵尸生存（类 Project Zomboid）
- **渲染管线**: Built-in Render Pipeline（`Shader.Find("Standard")`）
- **开发阶段**: 4 阶段（基础框架 → 核心玩法 → 程序生成 → 丰富细节）
- **对话分工**: 框架/大纲在此讨论，代码实现另开新对话
- **模块追踪**: Phase 1 完成 ✅ + 优化项自动排期，基础完成即可做下一系统
- **Git 提交**: 每个模块完成后提醒提交，格式：`模块名(阶段): Phase1 完成 — 简述`
- **大更新后归档**: 同步更新 `开发计划书.md` + `MEMORY.md`
- **审查任务**: 只指出问题，不修复（2026-06-24 确认）— 审查文档/代码时只列出问题清单和位置，由阿铁自己决定改不改、怎么改，不要主动用 Edit/Write 改文档内容

## 架构偏好

- 4 层：表现层 → 核心玩法层 → 数据配置层 → 基础设施层
- 原则：依赖倒置 + 事件驱动（EventBus 泛型 + 委托）
- 物理：3D Rigidbody + Collider，XZ 平面移动
- 解耦：主动指出耦合过强的地方

## 版本变更记录（精简）

- 2026-05-07：从 2D Tilemap → 3D + 45° 俯视
- 2026-05-08：确认团结引擎 1.8.5，装备系统扩展，负重配置
- 2026-05-10~11：生存系统 + UI 重构 + 装备系统重构
- 2026-05-13：WorldGen Phase 1~2（地形+聚落）、Built-in RP 确认、程序化城市重写
- 2026-05-14：武器系统 Phase 1~6 全部完成、UI 全面重构三区骨架式
- 2026-05-15：建造系统 Phase 1 完成、WorldGen 40m 基础单元重构
- 2026-05-16：车辆系统设计评审通过 + 模型导入，Phase 1 代码实现完成

## 📊 项目进度

| 阶段 | 进度 | 关键模块 |
|------|:----:|---------|
| 阶段 1：基础框架 | ✅ | 项目/目录/EventBus/移动/摄像机/碰撞 |
| 阶段 2 L1 地基 | ✅ | ItemData/EventBus/移动摄像 |
| 阶段 2 L2 框架 | ✅ | 物品/背包/装备/交互/时间/人物 |
| 阶段 2 L3 机制 | ✅ | 世界容器✅/生存✅/战斗✅/建造✅/交通工具✅ |
| 阶段 2 L4 组装 | ⬜ | 僵尸AI/配方/电力/区域 |
| 阶段 3：内容填充 | 🔄 | 世界生成✅/存档⬜/其余⬜ |
| 阶段 4：美术音效 | 🔄 | UI🏗️/3D模型🏗️/动画⬜/特效⬜/音效⬜ |

## 关键系统记录

### 武器系统 (✅)
- WeaponHolder/WeaponAiming/WeaponShooting
- 4 武器：木棍/长刀(近战) + 沙鹰(SidearmBelt)/AK47(枪械)
- EquipSlot: RightHand/LeftHand/KnifeBelt/SidearmBelt

### 建造系统 (✅)
- BuildableData/GhostPreview/BuildModeController/PlacedStructure
- BuildModeInputLock + BuildMenuUI + 7 默认资产

### 世界生成 (✅ Phase1)
- 40m 基础单元 × 20×20 网格 WFC
- 一级模块 12 个：商业/住宅/工业/郊区/道路/水域
- Pipeline: Seed→CityLayout→ModuleGrid→ModuleAssignment→Mesh
- 详见 `程序化城市生成算法文档.md`

### 车辆系统 (✅ 2026-05-18)
- 8 文件：VehicleData/VehicleController/VehicleInteraction/VehicleInputLock + CameraFollow修改 + GameEvents + GameConstants + Editor
- 物理：Rigidbody + 4 WheelCollider，W/S=油门倒车, A/D=转向, Space=刹车, Shift=加速
- 交互：IInteractable(E键上车)，输入锁定(禁用移动/武器/交互)，事件解耦
- 表现：仅第三人称跟随，驾驶时摄像机距离+5m
- 模型：OffRoad 越野车已导入 `Models/Vehicles/`
- 加速：两档速度 — 不按Shift=maxSpeed，按下=maxSpeed×2；油耗1.5倍
- 防侧翻：三层防护 — 重心下移(centerOfMassYOffset=-0.3) + 反侧倾杆(antiRollStiffness=5000) + 角速度阻尼(pitch×0.95, roll×0.92)
- **v6 Editor (2026-05-16)**: 世界空间先算再逆回本地（位置修复）
- **v7 Editor (2026-05-18)**: 轮子旋转退化为本地空间计算；悬架阻尼2500→8000修复弹跳；SHIFT加速+防侧翻

### 世界容器系统 (✅ 2026-05-18)
- 7 文件：LootTable/ContainerLootProfile（新建）+ WorldContainer/ContainerWindowUI/GameEvents/GameConstants（修改）+ CreateDefaultLootTables Editor
- 状态机：Unsearched → Searched → Empty，动态 IInteractable 属性
- 掉落：LootTable 不放回权重随机，ContainerLootProfile 绑定配置
- 交互：搜索进度条（世界空间 Slider）+ 空容器不可交互
- 4 默认 Profile：FridgeProfile(4×3,1.5s) / CabinetProfile(6×4,2s) / CorpseProfile(3×2,1s) / CrateProfile(4×5,2.5s)
- 事件：ContainerSearchedEvent + ContainerOpenedEvent
- L4 预集成：ZombieDied → CorpseProfile → 尸体容器
