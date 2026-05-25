using UnityEngine;

namespace _Game.Config
{
    /// <summary>
    /// 容器 Loot 配置资产
    /// 绑定掉落表 + 容器规格 + 搜索参数 + 区域标签
    /// 挂在 WorldContainer 的 profile 字段上
    /// </summary>
    [CreateAssetMenu(fileName = "ContainerLootProfile", menuName = "Game/ContainerLootProfile")]
    public class ContainerLootProfile : ScriptableObject
    {
        [Header("显示")]
        [Tooltip("容器名称，显示在交互提示和窗口标题")]
        public string displayName = "容器";

        [Header("掉落")]
        [Tooltip("权重随机掉落表")]
        public LootTable lootTable;

        [Tooltip("空容器概率（搜刮后没有物品）")]
        [Range(0f, 1f)]
        public float emptyChance = 0.1f;

        [Tooltip("最少掉落物品种类数")]
        [Range(0, 10)]
        public int minLootTypes = 1;

        [Tooltip("最多掉落物品种类数")]
        [Range(1, 10)]
        public int maxLootTypes = 3;

        [Header("容器规格")]
        [Tooltip("容器格子列数")]
        [Range(1, 10)]
        public int gridWidth = 4;

        [Tooltip("容器格子行数")]
        [Range(1, 10)]
        public int gridHeight = 3;

        [Header("交互")]
        [Tooltip("搜索耗时（秒），进度条时长")]
        [Range(0.1f, 10f)]
        public float searchTime = 1f;

        [Header("标签")]
        [Tooltip("容器类型标签，L4 区域系统按标签分配刷新规则")]
        public string containerTag = "CABINET";

        [Header("刷新")]
        [Tooltip("搜过后多少天重新刷新（0 = 每次区块加载都刷新）")]
        [Range(0f, 30f)]
        public float refreshCooldownDays = 7f;

        [Tooltip("是否开启刷新（玩家自建存储容器设为 false）")]
        public bool refreshEnabled = true;

        void OnValidate()
        {
            gridWidth = Mathf.Max(1, gridWidth);
            gridHeight = Mathf.Max(1, gridHeight);
            searchTime = Mathf.Max(0.1f, searchTime);
            emptyChance = Mathf.Clamp01(emptyChance);
            minLootTypes = Mathf.Clamp(minLootTypes, 0, maxLootTypes);
            maxLootTypes = Mathf.Max(1, maxLootTypes);
        }
    }
}
