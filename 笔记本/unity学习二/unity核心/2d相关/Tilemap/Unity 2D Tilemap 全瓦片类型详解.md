
通用基础项（所有瓦片均自带）：

- `Color`：瓦片整体染色 / 色调叠加
- `Collider Type`：碰撞体类型
    
    - `None`：无碰撞
    - `Sprite`：根据精灵形状生成碰撞体
    - `Grid`：按格子生成矩形碰撞体
    
- `Flags`：瓦片标签（是否可通行、是否静态等，一般保持默认）

---

## 1. Rule Tile 基础规则瓦片

**作用**：根据周边相邻瓦片自动匹配对应贴图，实现墙壁、地面、边缘无缝拼接，最常用核心瓦片。

表格

|参数|功能说明|实战用法|
|---|---|---|
|Color|瓦片整体色调|废墟地面调暗、老旧墙体加黄调|
|Collider Type|碰撞体模式|墙体选 `Sprite`；地面 / 装饰选 `None`|
|Flags|瓦片行为标记|默认即可，无需修改|
|Rules（规则列表）|核心拼接规则集合，可添加多条规则|按上下左右、四角区分墙体 / 地面样式|
|Neighbors（8 方向邻居）|单方向邻居判定规则：<br><br>1. Don't Care：无视邻居<br><br>2. This：必须是同类型瓦片<br><br>3. Not This：必须不是同类型瓦片|配置 “上方无瓦片 = 显示墙顶” 逻辑|
|Output Sprites|规则匹配后显示的精灵图|同规则下可放多张图随机切换，减少重复感|
|Variation|贴图随机变体|地面添加多种裂纹、碎石纹理随机展示|
|Animation Speed|变体切换速度|静态场景拉满 / 默认，仅做随机切换不做动画|

---

## 2. Animated Tile 动画瓦片

**作用**：制作循环 / 单次帧动画瓦片，用于动态场景特效。

表格

|参数|功能说明|实战用法|
|---|---|---|
|Color|瓦片整体色调|火焰加深橙红色、灯光提亮|
|Collider Type|碰撞体模式|火焰、雾气选 `None`；可交互灯台选 `Sprite`|
|Flags|瓦片行为标记|默认即可|
|Sprites|动画帧序列（按播放顺序排列）|火焰、滴水、闪烁路灯、摇曳杂草帧图|
|Animation Speed|动画播放帧率|火焰 8~12 FPS；路灯闪烁 1~2 FPS|
|Loop Once|仅播放一次，不循环|爆炸闪光、临时特效、触发式亮光|
|Start Frame Randomization|随机初始播放帧|避免整片动画瓦片同步卡顿，场景更自然|

---

## 3. Isometric Rule Tile 等距规则瓦片

**作用**：`Rule Tile` 专属分支，适配**斜 45° 等距视角**网格，自动处理斜向拼接与层级遮挡。

表格

|参数|功能说明|实战用法|
|---|---|---|
|Color|瓦片整体色调|统一场景色调、昼夜明暗切换|
|Collider Type|碰撞体模式|建筑墙体选 `Sprite`，自动适配斜向外形|
|Flags|瓦片行为标记|默认即可|
|Rules|等距网格专用拼接规则|处理斜向建筑、台阶、围墙拼接|
|Neighbors|等距视角专属邻居判定（适配斜向相邻逻辑）|区分建筑前后层、斜向边缘|
|Output Sprites|匹配规则后显示贴图|等距建筑顶面、立面、边角贴图|
|Sorting Order Offset|渲染层级偏移|靠前建筑加正值、靠后加负值，实现遮挡|

---

## 4. Hexagonal Rule Tile 六边形规则瓦片

**作用**：适配**六边形网格**地图，分两种朝向，多用于格子探索、战棋类地图。

表格

|参数|功能说明|实战用法|
|---|---|---|
|Color|瓦片整体色调|统一区域色调、区分地形|
|Collider Type|碰撞体模式|障碍墙体选 `Sprite`，行走地面选 `None`|
|Flags|瓦片行为标记|默认即可|
|Rules|六边形网格拼接规则|六边形地块、围墙、通道拼接|
|Neighbors|6 个方向邻居判定（六边形专属）|判定相邻地块是否存在墙体 / 道路|
|Output Sprites|规则匹配贴图|六边形草地、废墟、道路纹理|
|Hexagonal Settings|六边形朝向：<br><br>Flat-Top（平边朝上）<br><br>Pointed-Top（顶点朝上）|根据美术资源选择对应朝向|

---

## 5. Rule Override Tile 规则覆盖瓦片

**作用**：复用已有 `Rule Tile` 的**全部拼接规则**，仅替换贴图 / 碰撞 / 颜色，快速制作同规则变体，不用重写规则。

表格

|参数|功能说明|实战用法|
|---|---|---|
|Original Rule Tile|绑定源规则瓦片（继承其所有 Rules）|绑定原版完好墙体 Rule Tile|
|Override Sprites|替换源瓦片的输出贴图|制作破损墙、烧焦墙、涂鸦墙变体|
|Override Collider Type|可选：覆盖原瓦片碰撞体|倒塌废墟墙体改为 `None`，允许穿行|
|Override Color|可选：覆盖原瓦片色调|破损墙体加深灰色、暗黄色|
|Flags|瓦片行为标记|继承源瓦片，一般不修改|

---

## 6. Advanced Rule Override Tile 高级规则覆盖瓦片

**作用**：`Rule Override Tile` 增强版，支持**条件判定、权重、事件触发**，实现动态交互瓦片。

表格

|参数|功能说明|实战用法|
|---|---|---|
|Original Rule Tile|绑定源基础规则瓦片|复用墙体 / 地面原有拼接逻辑|
|Override Conditions|自定义额外匹配条件|玩家靠近、夜晚、怪物存在时切换样式|
|Weighted Variants|带权重的贴图变体|70% 完好墙体 + 30% 破损墙体随机分布|
|Override Sprites|替换输出贴图|多状态场景纹理切换|
|Override Animation Speed|覆盖动画 / 变体切换速度|差异化灯光、火焰跳动速度|
|Trigger Events|规则匹配时触发事件|踩中瓦片播放音效、触发提示、生成怪物|
|Override Color / Collider|覆盖色调与碰撞体|动态修改瓦片外观与通行规则|

---

## 7. Custom Rule Tile Script 自定义规则瓦片（脚本模板）

**作用**：C# 脚本自定义瓦片逻辑，扩展原生功能，可在 Inspector 挂载自定义参数，对接游戏业务逻辑（血量、破坏、建造、AI 交互等）。

### 内置继承参数（原生 Rule Tile 全部参数）

同 **Rule Tile**，包含 Color、Collider、Rules、Neighbors、Output Sprites 等。

### 自定义拓展参数（Inspector 可视化）

表格

|自定义参数|功能说明|实战用法|
|---|---|---|
|damageLevel|瓦片损坏等级（0~100）|对接怪物攻击，数值越高显示越破损|
|destructibleByZombies|是否可被僵尸破坏|控制怪物是否攻击当前瓦片|
|buildable|是否允许玩家建造|建造系统判定可用地块|
|footstepCount|踩踏计数（运行时更新）|踩踏越多，地面污渍 / 裂纹越明显|

### 配套基础脚本（可直接新建 C# 使用）

csharp

运行

```csharp
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "CustomSurvivalTile", menuName = "Tiles/Custom Rule Tile")]
public class CustomSurvivalTile : RuleTile
{
    [Header("自定义瓦片配置")]
    [Tooltip("损坏等级 0=完好 100=完全破损")]
    public int damageLevel;
    [Tooltip("是否可被僵尸破坏")]
    public bool destructibleByZombies;
    [Tooltip("是否可在上方建造")]
    public bool buildable;

    [HideInInspector] public int footstepCount;

    public override bool RuleMatch(int neighbor, TileBase other)
    {
        // 可在此重写自定义拼接逻辑
        return base.RuleMatch(neighbor, other);
    }
}
```

---

## 补充：Tilemap / Tile Palette 全局面板（配套）

### Tile Palette 瓦片调色板

表格

|参数|功能说明|实战用法|
|---|---|---|
|Grid|网格类型|常规俯视角选 `Rectangular`|
|Cell Size|单瓦片像素尺寸|和美术图一致（32/64 像素常用）|
|Cell Swizzle|坐标转换|矩形网格默认不修改|
|Sort Order|瓦片渲染排序基准|地面层级低于墙体，保证遮挡正常|

### Tilemap 地图组件（场景物体）

表格

|参数|功能说明|实战用法|
|---|---|---|
|Tilemap|瓦片位置 / 颜色基础组件|整层地图统一调色、偏移|
|Tilemap Renderer > Sorting Layer|渲染分层|划分地面、墙体、装饰、前景层|
|Tilemap Renderer > Order in Layer|同层内排序|数值越大，渲染越靠前|
|Tilemap Collider 2D|瓦片碰撞体生成器|墙体层必加，生成格子碰撞|
|Composite Collider 2D|合并整体碰撞体|搭配 Tilemap Collider，优化性能|

---

## 瓦片选型速查（项目推荐)
1. 常规 2D / 俯视角地图：**Rule Tile**（主力）
2. 动态特效（火、灯、雾气）：**Animated Tile**
3. 等距斜视角地图：**Isometric Rule Tile**
4. 六边形格子地图：**Hexagonal Rule Tile**
5. 同规则多外观变体：**Rule Override Tile**
6. 动态交互、权重、触发事件：**Advanced Rule Override Tile**
7. 对接游戏逻辑（破坏、建造、AI）：**Custom Rule Tile Script**
