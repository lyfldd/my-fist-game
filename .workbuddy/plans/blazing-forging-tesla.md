# L2 人物系统 — 三步实现计划

## Step 1：基础属性（数据+组件）

### 文件
- `_Game/Config/CharacterData.cs` — SO 模板
- `_Game/Systems/Character/PlayerCharacter.cs` — 运行时组件

### CharacterData (SO)
```
基础属性：
  strength (力量)     → 近战伤害、负重
  agility (敏捷)      → 移速、攻速
  constitution (体质) → 血量、抗性
  intelligence (智力) → 制作速度

出生默认值: 5
范围: 1~10
```

### PlayerCharacter (组件)
```
- 引用 CharacterData SO 作为出生模板
- 运行时四维属性（可成长）
- 发布事件：属性变化时通知其他系统
- 挂载到 Player 上
```

## Step 2：技能 + 经验

### 文件
- `_Game/Systems/Character/CharacterSkill.cs`
- 修改 `PlayerCharacter.cs` 增加技能列表

### 技能系统
```
技能列表（初版）：
  近战 / 远程 / 医疗 / 烹饪 / 建造 / 潜行

升级规则：
  每次使用 → +XP
  XP 满 → 等级 +1 → 属性点 +1（玩家自由分配）
```

## Step 3：性别/职业模板

### 文件
- `_Game/Config/CharacterPreset.cs` — 预设数据
- 修改 `PlayerCharacter.cs` 增加预设应用逻辑

### 预设
```
性别：
  男性：STR+1, CON+1
  女性：AGI+1, INT+1

职业（初版 3 个）：
  军人：STR+2, CON+1
  医生：INT+2
  工程师：INT+1, STR+1
```
