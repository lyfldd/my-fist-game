using System.Collections.Generic;
using _Game.Config;
using UnityEngine;
using ItemGraphAsset = _Game.Config.ItemGraph;

namespace _Game.Systems.ItemGraph
{
    /// <summary>
    /// 物品图谱运行时管理器 — 加载 ItemGraph.asset 并提供查询接口
    /// </summary>
    public class ItemGraphManager : MonoBehaviour
    {
        public static ItemGraphManager Instance { get; private set; }

        [SerializeField] private ItemGraphAsset graphAsset;
        [SerializeField] private RecipeCatalog recipeCatalog;

        private Dictionary<string, ItemGraphNode> _nodeDict;

        void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (graphAsset != null)
                BuildLookup();
        }

        void BuildLookup()
        {
            _nodeDict = new Dictionary<string, ItemGraphNode>();
            if (graphAsset.nodes == null) return;

            for (int i = 0; i < graphAsset.nodes.Length; i++)
            {
                var node = graphAsset.nodes[i];
                if (node != null && !string.IsNullOrEmpty(node.itemName))
                    _nodeDict[node.itemName] = node;
            }
        }

        /// <summary>
        /// 获取物品节点（null = 不在图谱中）
        /// </summary>
        public ItemGraphNode GetNode(string itemName)
        {
            if (_nodeDict == null) return null;
            _nodeDict.TryGetValue(itemName, out var node);
            return node;
        }

        /// <summary>
        /// 获取下游物品名称列表（哪些配方消费它）
        /// </summary>
        public string[] GetDownstream(string itemName)
        {
            var node = GetNode(itemName);
            return node?.downstreamItemNames ?? new string[0];
        }

        /// <summary>
        /// 获取上游物品名称列表（它由哪些材料做成）
        /// </summary>
        public string[] GetUpstream(string itemName)
        {
            var node = GetNode(itemName);
            return node?.upstreamItemNames ?? new string[0];
        }

        /// <summary>
        /// 获取最浅生产深度
        /// </summary>
        public int GetMinDepth(string itemName)
        {
            var node = GetNode(itemName);
            return node?.MinDepth ?? 0;
        }

        /// <summary>
        /// 是否断头路（无下游消费）
        /// </summary>
        public bool IsDeadEnd(string itemName)
        {
            var node = GetNode(itemName);
            return node?.isDeadEnd ?? false;
        }

        /// <summary>
        /// 是否原材料（无配方产出，只能从世界获取）
        /// </summary>
        public bool IsRawMaterial(string itemName)
        {
            var node = GetNode(itemName);
            return node?.isRawMaterial ?? false;
        }

        /// <summary>
        /// 获取生效的工作台等级
        /// </summary>
        public WorkstationTier GetEffectiveStation(string itemName)
        {
            var node = GetNode(itemName);
            return node?.EffectiveStation ?? WorkstationTier.Hands;
        }

        /// <summary>
        /// 获取所有断头路物品名称
        /// </summary>
        public string[] GetAllDeadEnds()
        {
            if (_nodeDict == null) return new string[0];
            var list = new List<string>();
            foreach (var kv in _nodeDict)
                if (kv.Value.isDeadEnd)
                    list.Add(kv.Key);
            return list.ToArray();
        }

        /// <summary>
        /// 获取所有核心材料（消费热度 >= 10）
        /// </summary>
        public string[] GetCoreMaterials()
        {
            if (_nodeDict == null) return new string[0];
            var list = new List<string>();
            foreach (var kv in _nodeDict)
                if (kv.Value.consumerCount >= 10)
                    list.Add(kv.Key);
            return list.ToArray();
        }

        /// <summary>
        /// 获取指定工作台的所有手工配方
        /// </summary>
        public RecipeData[] GetRecipesForStation(WorkstationTier tier)
        {
            if (recipeCatalog == null) return new RecipeData[0];
            return recipeCatalog.GetByStationAndMode(tier, industrial: false);
        }

        /// <summary>
        /// 获取指定设备的所有工业配方
        /// </summary>
        public RecipeData[] GetRecipesForDevice(string deviceName)
        {
            if (recipeCatalog == null) return new RecipeData[0];
            return recipeCatalog.GetByDevice(deviceName);
        }

        /// <summary>
        /// 获取某物品的所有产出配方（手工+工业）
        /// </summary>
        public RecipeData[] GetRecipesForItem(string itemName)
        {
            if (recipeCatalog == null || recipeCatalog.recipes == null) return new RecipeData[0];
            var list = new List<RecipeData>();
            foreach (var r in recipeCatalog.recipes)
            {
                if (r != null && r.resultItem != null && r.resultItem.itemName == itemName)
                    list.Add(r);
            }
            return list.ToArray();
        }

        /// <summary>
        /// 获取某物品可以被哪些设备生产（工业配方）
        /// </summary>
        public string[] GetDevicesForItem(string itemName)
        {
            var recipes = GetRecipesForItem(itemName);
            var devices = new HashSet<string>();
            foreach (var r in recipes)
                if (r.isIndustrial && !string.IsNullOrEmpty(r.productionDeviceName))
                    devices.Add(r.productionDeviceName);
            var result = new string[devices.Count];
            devices.CopyTo(result);
            return result;
        }
    }
}
