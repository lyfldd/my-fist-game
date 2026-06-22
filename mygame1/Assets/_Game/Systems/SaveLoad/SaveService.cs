using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace _Game.Systems.SaveLoad
{
    /// <summary>
    /// 存档序列化/文件IO服务（无 MonoBehaviour，线程安全）。
    ///
    /// 原子写入三步法:
    ///   1. 写 save.json.tmp
    ///   2. File.Move(save.json → save.json.backup) — 原子 rename
    ///   3. File.Move(save.json.tmp → save.json)   — 原子 rename
    ///
    /// .tmp 清理: Initialize() 扫描所有槽位删除残留 .tmp 文件。
    /// </summary>
    public static class SaveService
    {
        public const int MAX_SLOTS = 5;
        public const string SAVES_DIR = "Saves";

        private static readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            TypeNameHandling = TypeNameHandling.None,
            NullValueHandling = NullValueHandling.Include,
        };

        private static string _savesRoot;

        /// <summary>
        /// 初始化（程序启动时调用一次）。创建目录 + 清理 .tmp 残留。
        /// </summary>
        public static void Initialize()
        {
            _savesRoot = Path.Combine(Application.persistentDataPath, SAVES_DIR);
            Directory.CreateDirectory(_savesRoot);

            // 清理崩溃残留的 .tmp 文件
            for (int i = 0; i < MAX_SLOTS; i++)
            {
                var slotDir = GetSlotPath(i);
                if (!Directory.Exists(slotDir)) continue;

                foreach (var tmpFile in Directory.GetFiles(slotDir, "*.tmp"))
                {
                    try { File.Delete(tmpFile); }
                    catch (Exception ex) { Debug.LogWarning($"[SaveService] 无法删除残留 .tmp: {tmpFile} — {ex.Message}"); }
                }
            }
        }

        /// <summary> 获取指定槽位路径 </summary>
        public static string GetSlotPath(int slotIndex)
        {
            return Path.Combine(_savesRoot, $"Slot_{slotIndex}");
        }

        /// <summary> 检查槽位是否已有存档 </summary>
        public static bool SlotExists(int slotIndex)
        {
            return File.Exists(Path.Combine(GetSlotPath(slotIndex), "save.json"));
        }

        /// <summary> 读取元数据（load 之前调用，用于 UI 展示） </summary>
        public static SaveSlotMeta LoadMeta(int slotIndex)
        {
            var metaPath = Path.Combine(GetSlotPath(slotIndex), "meta.json");
            if (!File.Exists(metaPath)) return null;

            try
            {
                var json = File.ReadAllText(metaPath);
                return JsonConvert.DeserializeObject<SaveSlotMeta>(json, _jsonSettings);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveService] 读取 meta 失败 slot={slotIndex}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 保存元数据（轻量文本，主线程同步写入，<1ms）。
        /// </summary>
        public static void SaveMeta(int slotIndex, SaveSlotMeta meta)
        {
            try
            {
                var slotDir = GetSlotPath(slotIndex);
                Directory.CreateDirectory(slotDir);

                var metaPath = Path.Combine(slotDir, "meta.json");
                var json = JsonConvert.SerializeObject(meta, _jsonSettings);
                File.WriteAllText(metaPath, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveService] 保存 meta 失败 slot={slotIndex}: {ex.Message}");
            }
        }

        /// <summary>
        /// 主线程调用：准备好后，提交 SaveData 快照到后台线程写盘。
        /// 返回 Task，调用方可 await 或 fire-and-forget。
        /// </summary>
        public static Task SaveAsync(int slotIndex, SaveData snapshot)
        {
            return Task.Run(() => SaveToDisk(slotIndex, snapshot));
        }

        private static void SaveToDisk(int slotIndex, SaveData data)
        {
            try
            {
                var slotDir = GetSlotPath(slotIndex);
                Directory.CreateDirectory(slotDir);

                var savePath = Path.Combine(slotDir, "save.json");
                var backupPath = Path.Combine(slotDir, "save.json.backup");
                var tmpPath = Path.Combine(slotDir, "save.json.tmp");

                // 步骤 1: 序列化 → 写临时文件
                var json = JsonConvert.SerializeObject(data, _jsonSettings);
                File.WriteAllText(tmpPath, json);

                // 步骤 2: 旧正式档 → 备份（原子 rename）
                // Windows: File.Move 目标已存在会抛异常，先删旧的 backup
                if (File.Exists(savePath))
                {
                    if (File.Exists(backupPath))
                        File.Delete(backupPath);
                    File.Move(savePath, backupPath);
                }

                // 步骤 3: 临时文件 → 正式档（原子 rename）
                if (File.Exists(savePath))
                    File.Delete(savePath);
                File.Move(tmpPath, savePath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveService] 保存失败 slot={slotIndex}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 主线程调用：从磁盘读取存档。
        /// 版本校验由 SaveLoadManager 负责。
        /// </summary>
        public static SaveData Load(int slotIndex)
        {
            var slotDir = GetSlotPath(slotIndex);
            var savePath = Path.Combine(slotDir, "save.json");

            if (!File.Exists(savePath))
            {
                // 尝试从 backup 恢复
                var backupPath = Path.Combine(slotDir, "save.json.backup");
                if (File.Exists(backupPath))
                {
                    Debug.LogWarning($"[SaveService] save.json 不存在，从 backup 恢复 slot={slotIndex}");
                    File.Move(backupPath, savePath);
                }
                else
                {
                    Debug.LogError($"[SaveService] 存档文件不存在 slot={slotIndex}");
                    return null;
                }
            }

            try
            {
                var json = File.ReadAllText(savePath);
                var data = JsonConvert.DeserializeObject<SaveData>(json, _jsonSettings);
                if (data == null)
                {
                    Debug.LogError($"[SaveService] 反序列化返回 null slot={slotIndex}，存档可能损坏");
                    return null;
                }
                return data;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveService] 加载失败 slot={slotIndex}: {ex.Message}");
                return null;
            }
        }

        /// <summary> 删除指定槽位的所有文件 </summary>
        public static void DeleteSlot(int slotIndex)
        {
            try
            {
                var slotDir = GetSlotPath(slotIndex);
                if (Directory.Exists(slotDir))
                    Directory.Delete(slotDir, true);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveService] 删除槽位失败 slot={slotIndex}: {ex.Message}");
            }
        }

        /// <summary> 获取所有槽位的元数据列表（UI 展示用） </summary>
        public static SaveSlotMeta[] GetAllMetas()
        {
            var metas = new SaveSlotMeta[MAX_SLOTS];
            for (int i = 0; i < MAX_SLOTS; i++)
                metas[i] = LoadMeta(i);
            return metas;
        }
    }
}
