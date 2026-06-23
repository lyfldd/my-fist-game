using UnityEngine;
using _Game.Config;
using _Game.Core;

namespace _Game.Systems.WorldContainer
{
    /// <summary>
    /// 地面物品组件（2D 贴片 + Billboard + Collider）
    /// 挂在由 DropItem 动态生成的 GameObject 上
    /// </summary>
    public class WorldItem : MonoBehaviour
    {
        /// <summary> 唯一实例 ID（存档系统用，跨存读定位地面物品） </summary>
        public int instanceId;

        public ItemData itemData;
        public int count = 1;
        public float itemDurability;  // 前置A2：地面物品耐久（0=满耐久/无耐久）
        public int repairCount;       // 前置A2：修理次数

        [Header("外观")]
        public float itemScale = GameConstants.WORLD_ITEM_SCALE;  // 地面显示大小

        private Transform _billboard;

        void Awake()
        {
            // 创建 billboard Quad
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.transform.SetParent(transform, false);
            quad.transform.localPosition = Vector3.up * GameConstants.WORLD_ITEM_Y_OFFSET;
            quad.transform.localScale = Vector3.one * itemScale;
            _billboard = quad.transform;

            // 移除 Quad 的 Collider（用父级 Trigger 代替）
            Destroy(quad.GetComponent<Collider>());

            // 设置贴图
            if (itemData != null && itemData.icon != null)
            {
                var renderer = quad.GetComponent<Renderer>();
                var mat = new Material(Shader.Find("Unlit/Transparent"));
                mat.mainTexture = itemData.icon.texture;
                renderer.material = mat;
            }

            // 父级 Collider 作为交互触发器
            var col = gameObject.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = GameConstants.WORLD_ITEM_COLLIDER_SIZE;

            // 添加交互接口
            gameObject.AddComponent<GroundItemInteract>();
        }

        void LateUpdate()
        {
            // Billboard：始终面向摄像机
            if (_billboard != null && Camera.main != null)
            {
                _billboard.forward = Camera.main.transform.forward;
            }
        }
    }
}
