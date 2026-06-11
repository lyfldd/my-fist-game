# Resources 资源管理补充知识点

> 为已有的同步加载、异步加载、卸载、特殊文件夹笔记补充遗漏的重要概念

---

## 一、Resources 系统深入理解

### 1. Resources 的架构限制（原笔记忽略的重要约束）

```
Resources 系统本质是一个 "硬编码的引用系统"
```

**限制一：包体膨胀**
- Resources 文件夹下的所有资源都会被打入**主包体**，无法按需下载
- 即使某个关卡只需要资源A，整个 Resources 目录都会被加载

**限制二：更新困难**
- Resources 内的文件被打包进 `assets` 文件（加密二进制）
- 无法单独替换某个资源，更新必须重新下载整个包体

**限制三：启动时间**
- Unity 在启动时会建立 Resources 资源的索引
- Resources 文件夹越大，启动时间越长

### 2. Resources 加载的完整生命周期

```
Resources.Load() 调用
  → 1. 检查是否是第一次加载
       → 是：从打包文件中反序列化资源 → 存入缓存表
       → 否：直接从缓存表获取引用
  → 2. 返回对象引用给调用方

注意：
- 如果资源后续不再使用，应调用 Resources.UnloadAsset() 释放
- 但缓存表本身不会释放，除非调用 Resources.UnloadUnusedAssets()
```

### 3. 不同类型资源的加载差异

```csharp
// 1. 预设体 —— 必须实例化才能看到
GameObject prefab = Resources.Load<GameObject>("Enemy");
GameObject instance = Instantiate(prefab);  // 必须在场景中

// 2. 材质/纹理 —— 可直接使用
Material mat = Resources.Load<Material>("MyMaterial");
GetComponent<Renderer>().material = mat;  // 直接赋值使用

// 3. TextAsset —— 可读取文本/二进制内容
TextAsset textFile = Resources.Load<TextAsset>("Config");
string content = textFile.text;  // 文本文件内容
byte[] bytes = textFile.bytes;   // 二进制文件内容

// 4. Sprite —— 需要转为 Sprite
Sprite sprite = Resources.Load<Sprite>("CharacterIdle");
// Image 组件直接使用 sprite

// 5. 同一路径下同名但不同类型资源的加载
Object[] assets = Resources.LoadAll("path", typeof(Object));
// 或者分别指定类型
Texture tex = Resources.Load("path", typeof(Texture)) as Texture;
```

---

## 二、Addressables —— Resources 的现代替代方案

Unity 官方已不再推荐使用 Resources 系统，推荐 **Addressables**。

### 1. Resources vs Addressables 对比

| 特性 | Resources | Addressables |
|------|-----------|-------------|
| 包体 | 全部打入主包 | 可分包、按需下载 |
| 更新 | 必须整包更新 | 热更新单个资源 |
| 依赖管理 | 手动 | 自动处理依赖 |
| 远程加载 | 不支持 | 原生支持 CDN |
| 异步加载 | 仅支持协程 | 原生 async/await |
| 引用计数 | 无 | 自动引用计数 |
| 平台 | 所有 Unity 版本 | Unity 2019.4+ |

### 2. 何时可以继续用 Resources

```
✅ 可以使用 Resources 的场景：
  - 极小型项目（1-2个场景）
  - 原型开发阶段
  - 全局通用资源（如通用Shader、Cursors）
  - 无需热更新的单机小游戏

❌ 应迁移到 Addressables 的场景：
  - 商业手游
  - 需要热更新的项目
  - 资源总量 > 500MB
  - 7日留存作为重要指标的联网游戏
```

---

## 三、Resources.UnloadUnusedAssets 深入分析

### 1. 不释放资源的常见原因

```csharp
// 场景：你调用了 UnloadUnusedAssets，但资源仍然在内存中

// 原因1：脚本中仍然持有引用
private Texture _cache;
void Start()
{
    _cache = Resources.Load<Texture>("Logo");
    Resources.UnloadAsset(_cache);
    // ⚠️ _cache 变量还没置空！对象引用还在
    // 后续调用 Resources.UnloadUnusedAssets 不会释放这个纹理
}
// ✅ 正确做法：UnloadAsset 后将引用置空
_cache = null;

// 原因2：Instantiate 实例化后的物体也持有引用
GameObject go = Instantiate(Resources.Load<GameObject>("Enemy"));
Resources.UnloadAsset(prefab);  // ❌ 会失败：UnloadAsset 不能卸载 GameObject
DestroyImmediate(go, true);     // 必须先删除实例

// 原因3：UnloadUnusedAssets 不是立即执行的
Resources.UnloadUnusedAssets();
// 这里还是有可能看到旧资源
// 它在一帧之后才会真正释放
```

### 2. 正确内存释放模式

```csharp
// 推荐的完整释放流程
public void CleanupResource(Object resource)
{
    if (resource is GameObject)
    {
        // GameObject 不能直接 UnloadAsset
        // 确保实例已销毁
    }
    else
    {
        Resources.UnloadAsset(resource);
    }
    resource = null;
}

// 批量释放（场景切换时）
IEnumerator CleanupOnSceneChange()
{
    yield return null;  // 等一帧确保所有引用被清除
    Resources.UnloadUnusedAssets();
    System.GC.Collect();  // 强制垃圾回收
    yield return null;    // 等 GC 完成
}
```

---

## 四、特殊文件夹补充

### 1. StreamingAssets 的跨平台坑

```csharp
// StreamingAssets 在不同平台的路径差异
string path = Application.streamingAssetsPath;
// Windows: file:///.../StreamingAssets
// Android: jar:file://... (在APK内，不能直接用System.IO读取)
// iOS: file:///.../Raw （真机为只读）
// WebGL: url/StreamingAssets

// ✅ 跨平台读取方案：UnityWebRequest
string filePath = System.IO.Path.Combine(Application.streamingAssetsPath, "data.json");
#if UNITY_ANDROID
    // Android 需要用 UnityWebRequest
    UnityWebRequest request = UnityWebRequest.Get(filePath);
    yield return request.SendWebRequest();
    string json = request.downloadHandler.text;
#else
    // 其他平台可直接 IO
    string json = System.IO.File.ReadAllText(filePath);
#endif
```

### 2. persistentDataPath 的典型用途

```csharp
// 存档操作
string savePath = Application.persistentDataPath + "/save.dat";

// 保存
void SaveGame(GameData data)
{
    string json = JsonUtility.ToJson(data);
    System.IO.File.WriteAllText(savePath, json);
}

// 读取
GameData LoadGame()
{
    if (System.IO.File.Exists(savePath))
    {
        string json = System.IO.File.ReadAllText(savePath);
        return JsonUtility.FromJson<GameData>(json);
    }
    return new GameData();
}

// ** 注意：persistentDataPath 的内容在 iOS 上会被 iCloud 自动备份 **
// 如果不想备份，需要设置：
// UnityEngine.iOS.Device.SetNoBackupFlag(savePath);
```

### 3. 各路径完全对比

| 路径 | API | 读 | 写 | 打包方式 | 典型用途 |
|------|-----|----|----|---------|---------|
| 项目内 | `Application.dataPath` | ✅ | ✅ | 不打入包 | 编辑器工具 |
| Resources | `Resources.Load()` | ✅只读 | ❌ | 加密打包 | 游戏资源 |
| StreamingAssets | `Application.streamingAssetsPath` | ✅ | ⚠️PC可写 | 原样保留 | 配置表、视频 |
| persistentDataPath | `Application.persistentDataPath` | ✅ | ✅ | 不打包 | 存档、下载缓存 |
| temporaryCachePath | `Application.temporaryCachePath` | ✅ | ✅ | 不打包 | 临时下载缓存(可能被OS清理) |

### 4. Resources 路径规范补充

```csharp
// 路径规则总结：
// 1. 不包含扩展名
Resources.Load<GameObject>("Enemy");  // ✅
Resources.Load<GameObject>("Enemy.prefab");  // ❌

// 2. 区分大小写（不同平台行为不同！）
// Windows 编辑器不区分大小写
// iOS/Android 真机区分大小写
// 建议：始终视为区分大小写

// 3. Resources 文件夹可以有多层
// "UI/Prefabs/Button" 会查找 Resources/UI/Prefabs/Button

// 4. Resources 文件夹可以有多个
// 项目中的多个 Resources 文件夹会被合并
// 重名的路径会引发警告，以第一个找到的为准
```

---

## 五、AssetBundle 简述（Resources 的扩展）

虽然原笔记没涉及，但 AssetBundle 是从 Resources 到 Addressables 的过渡方案：

```
Resources.Load("path")     → 从包体加载
AssetBundle.LoadFromFile()  → 从外部文件加载
Addressables.LoadAsset()    → 从任意位置（包体/外部/CDN）加载
```

```csharp
// AssetBundle 基本使用
AssetBundle ab = AssetBundle.LoadFromFile(Application.streamingAssetsPath + "/mybundle");
GameObject prefab = ab.LoadAsset<GameObject>("Enemy");
Instantiate(prefab);
ab.Unload(false);  // 卸载包，但不卸载已加载的资源
```
