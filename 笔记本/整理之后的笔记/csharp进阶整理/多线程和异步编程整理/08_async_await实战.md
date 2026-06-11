# 08_async/await 实战

## 一、异常处理（游戏技能系统）

### 问题场景
角色释放技能，可能因各种原因失败：
- 没蓝
- 被沉默
- 目标死亡
- 距离太远

### await 的异常处理写法
```csharp
// 异步施放技能
async Task CastSkillAsync(Player caster, Enemy target)
{
    await Task.Delay(500);  // 前摇动画

    // 可能出现各种异常
    if (target.IsDead)
        throw new InvalidOperationException("目标已死亡");

    if (caster.Mana < 50)
        throw new InvalidOperationException("法力不足");

    if (Vector3.Distance(caster.Position, target.Position) > 20)
        throw new InvalidOperationException("距离过远");

    await ApplyDamageAsync(caster, target);
}

// 调用 + 捕获异常
try
{
    await CastSkillAsync(player, enemy);
    Debug.Log("技能施放成功");
}
catch (InvalidOperationException ex)
{
    ShowTip(ex.Message);  // ✅ 直接弹出对应提示
}
```

### 如果用 .Result / Wait() 的坑
```csharp
// ❌ 根本进不来这个 catch
Task task = CastSkillAsync(player, enemy);
try
{
    task.Wait();
}
catch (InvalidOperationException ex)
{
    // ❗ 根本进不来这个 catch
}
catch (AggregateException ex)  // ✅ 只能抓到这个包装异常
{
    var realError = ex.InnerException;  // 必须手动拆包
    ShowTip(realError.Message);
}
```

**为什么 await 的异常处理更好？**
| 问题 | .Result/Wait() | await |
|---|---|---|
| 异常类型 | AggregateException 包装 | 直接原始异常 |
| 拆包 | 需要手动 `ex.InnerException` | 不需要 |
| 多异常处理 | 列表 `InnerExceptions` | 正常 try/catch |
| 代码量 | 多 | 少 |
| 错误提示 | 可能不显示 | 清晰 |

---

## 二、顺序执行 vs 并行执行

### 1. 顺序执行
```csharp
await A();
await B();
await C();
```

**游戏例子：角色复活流程**
```csharp
async Task RespawnAsync()
{
    await PlayRespawnAnimation();   // 1. 播放复活动画
    await RestoreHp();              // 2. 恢复血量
    await ShowTip("复活成功");      // 3. 弹出复活提示
}
```
- 有先后依赖，必须一步一步来
- **总时间 = A + B + C**

### 2. 并行执行（重点！）
```csharp
// 先把所有任务启动
Task t1 = A();
Task t2 = B();
Task t3 = C();

// 再一起等全部结束
await Task.WhenAll(t1, t2, t3);
```

**游戏例子：进入关卡，同时加载资源**
```csharp
async Task LoadLevelAsync()
{
    Task loadScene = LoadSceneAsync();   // 场景资源
    Task loadModel = LoadModelAsync();   // 角色模型
    Task loadAudio = LoadAudioAsync();   // 背景音乐

    await Task.WhenAll(loadScene, loadModel, loadAudio);

    Debug.Log("全部加载完成，进入游戏");
}
```
- 无先后依赖，同时执行
- **总时间 ≈ 最慢的那个**
- 速度大幅提升

### 3. 获取并行任务的返回值
```csharp
async Task Demo()
{
    Task<int> t1 = Calc1Async();
    Task<int> t2 = Calc2Async();

    // ✅ await WhenAll 直接获取结果数组
    int[] results = await Task.WhenAll(t1, t2);

    int r1 = results[0];
    int r2 = results[1];
}
```

### 4. 等待任意一个完成
```csharp
// 谁先完成，就立刻继续
int index = await Task.WhenAny(task1, task2, task3);
```

**场景**：谁先击杀 BOSS，谁拿奖励

---

## 三、异步加载资源（Unity 游戏开发）

### 场景1：异步加载场景
```csharp
async Task LoadSceneAsync(string sceneName)
{
    AsyncOperation asyncOp = SceneManager.LoadSceneAsync(sceneName);

    while (!asyncOp.isDone)
    {
        float progress = asyncOp.progress * 100;
        Debug.Log($"加载进度：{progress:F1}%");
        await Task.Delay(100);  // 每帧更新一次
    }

    Debug.Log("场景加载完成！");
}
```

### 场景2：异步加载图片/模型
```csharp
async Task<Sprite> LoadSpriteAsync(string path)
{
    var request = Resources.LoadAsync<Sprite>(path);

    while (!request.isDone)
    {
        loadingBar.fillAmount = request.progress;
        await Task.Yield();  // 让出本帧
    }

    return request.asset as Sprite;
}
```

### 场景3：异步网络请求
```csharp
async Task<PlayerData> FetchPlayerDataAsync(string playerId)
{
    using (HttpClient client = new HttpClient())
    {
        string url = $"https://api.game.com/players/{playerId}";
        string json = await client.GetStringAsync(url);

        PlayerData data = JsonUtility.FromJson<PlayerData>(json);
        return data;
    }
}
```

### 场景4：带超时控制的异步请求
```csharp
async Task<T> FetchWithTimeoutAsync<T>(Task<T> task, int timeoutMs)
{
    Task winner = await Task.WhenAny(task, Task.Delay(timeoutMs));

    if (winner == task)
    {
        return await task;  // 正常返回结果
    }
    else
    {
        throw new TimeoutException($"请求超时：{timeoutMs}ms");
    }
}
```

---

## 四、异步技能系统设计

### 设计思路：异步 + 回调
```csharp
// 异步施放火球术
async Task<bool> CastFireballAsync(Player caster, Vector3 targetPos)
{
    try
    {
        // 1. 检查条件
        if (caster.Mana < 30)
        {
            ShowTip("法力不足！");
            return false;
        }

        // 2. 播放前摇动画
        await PlayCastAnimationAsync(caster, 0.5f);

        // 3. 扣除法力
        caster.Mana -= 30;

        // 4. 生成火球并飞行
        Fireball fb = SpawnFireball(caster.Position, targetPos);
        await fb.FlyAsync();

        // 5. 造成伤害
        Enemy target = GetEnemyAt(targetPos);
        if (target != null)
        {
            target.TakeDamage(caster.Attack);
        }

        return true;
    }
    catch (Exception ex)
    {
        Debug.LogError($"火球术施放失败：{ex.Message}");
        return false;
    }
}
```

---

## 五、取消与超时

### CancellationToken（取消异步操作）
```csharp
CancellationTokenSource cts = new CancellationTokenSource();

// 玩家取消加载
async Task LoadWithCancelAsync()
{
    try
    {
        for (int i = 0; i < 100; i++)
        {
            cts.Token.ThrowIfCancellationRequested();  // 检查取消
            await Task.Delay(100);
            UpdateProgress(i);
        }
    }
    catch (OperationCanceledException)
    {
        Debug.Log("加载已取消");
    }
}

// 按ESC取消
void Update()
{
    if (Input.GetKeyDown(KeyCode.Escape))
    {
        cts.Cancel();
    }
}
```

### 超时控制
```csharp
async Task<bool> TryLoadWithinAsync(int timeoutMs)
{
    Task loadTask = LoadLevelAsync();
    Task timeoutTask = Task.Delay(timeoutMs);

    Task winner = await Task.WhenAny(loadTask, timeoutTask);

    if (winner == loadTask)
    {
        return true;  // 加载成功
    }
    else
    {
        return false;  // 超时
    }
}
```

---

## 六、战斗系统异步设计

### 回合制战斗
```csharp
async Task BattleRoundAsync(Player player, Enemy enemy)
{
    // 玩家行动
    await PlayAttackAnimationAsync(player);
    enemy.Hp -= player.Attack;
    UpdateHpBar(enemy);

    // 等待一小段时间
    await Task.Delay(300);

    // 敌人反击
    if (enemy.Hp > 0)
    {
        await PlayAttackAnimationAsync(enemy);
        player.Hp -= enemy.Attack;
        UpdateHpBar(player);
    }
}

// 完整战斗流程
async Task FightAsync(Player player, Enemy enemy)
{
    while (player.Hp > 0 && enemy.Hp > 0)
    {
        await BattleRoundAsync(player, enemy);
        await Task.Delay(500);  // 每回合间隔
    }

    if (player.Hp > 0)
        ShowVictoryUI();
    else
        ShowDefeatUI();
}
```

---

## 七、并行计算（最大化利用多核）

### 场景：批量怪物 AI 计算
```csharp
// ❌ 顺序计算（慢）
for (int i = 0; i < monsters.Count; i++)
{
    monsters[i].CalculatePath();
}

// ✅ 并行计算（快）
Task[] tasks = monsters.Select(m => 
    Task.Run(() => m.CalculatePath())
).ToArray();

await Task.WhenAll(tasks);
```

### 场景：批量伤害计算
```csharp
async Task<int> CalculateTotalDamageAsync(List<SkillEffect> effects)
{
    Task<int>[] tasks = effects.Select(effect =>
        Task.Run(() => effect.CalculateDamage())
    ).ToArray();

    int[] results = await Task.WhenAll(tasks);

    return results.Sum();
}
```

---

## 八、常见错误与最佳实践

### ❌ 常见错误
| 错误 | 问题 |
|---|---|
| `async void` | 异常无法捕获，危险 |
| `.Result` | 会阻塞，可能死锁 |
| 异步 void 方法中 `await` | 变成 `async void`，异常丢失 |
| 在循环中 await | 无法并行，速度慢 |

### ✅ 最佳实践
| 场景 | 推荐写法 |
|---|---|
| 异步方法返回值 | `async Task<T>` 而不是 `T` |
| 事件处理程序 | `async void` |
| 不用返回值的异步逻辑 | `async Task` |
| 并行执行 | `Task.WhenAll` |
| 等待任意一个 | `Task.WhenAny` |
| 取消操作 | `CancellationToken` |
| 性能优化（库代码） | `.ConfigureAwait(false)` |

---

## 九、综合示例：异步游戏启动流程
```csharp
async Task StartGameAsync()
{
    try
    {
        // 并行加载
        Task loadScene = LoadMainSceneAsync();
        Task loadPlayer = LoadPlayerDataAsync();
        Task loadAudio = LoadAudioAsync();

        await Task.WhenAll(loadScene, loadPlayer, loadAudio);

        // 播放开场动画
        await PlayIntroAnimationAsync();

        // 显示主界面
        ShowMainUI();

        Debug.Log("游戏加载完成！");
    }
    catch (Exception ex)
    {
        Debug.LogError($"启动失败：{ex.Message}");
        ShowErrorUI("加载失败，请重试");
    }
}
```

---

## 十、修正错误汇总表

| 错误认知 | 正确理解 |
|---|---|
| 异步方法里循环 await 就是并行 | ❌ 循环里 await 是顺序执行，要并行用 `Task.WhenAll` |
| async 会让代码变快 | ❌ async 不加快速度，只是不阻塞 |
| 异步方法可以返回 void | ❌ 只能返回 `Task`，除非是事件处理程序 |
| Task.WhenAny 只返回一个 | ✅ 返回第一个完成的，但结果不会自动提取 |
| async 可以省略 return | ⚠️ `async Task` 必须 `return` 值；`async void` 永远不要用 |
