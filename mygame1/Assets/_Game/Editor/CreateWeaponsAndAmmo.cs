using UnityEditor;
using UnityEngine;
using _Game.Config;

namespace _Game.Editor
{
    /// <summary>
    /// 一键创建 8 火器 + 5 弹药 + 弹药组件 + 特殊武器。
    /// 运行前确保 CreateDefaultItems 等脚本已跑过一次（基础物品已存在）。
    /// </summary>
    public class CreateWeaponsAndAmmo : EditorWindow
    {
        [MenuItem("Tools/武器装备/创建全部武器弹药")]
        public static void Run()
        {
            string baseDir = "Assets/_Game/Config/Items";
            int created = 0;

            created += CreateAmmoComponents(baseDir);
            created += CreateAmmo(baseDir);
            created += CreateFirearms(baseDir);
            created += CreateSpecialWeapons(baseDir);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[武器装备] 全部完成，创建 {created} 个物品");
        }

        static ItemData GetItem(string name)
        {
            var guids = AssetDatabase.FindAssets($"t:ItemData {name}");
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
                if (item != null && item.itemName == name) return item;
            }
            return null;
        }

        static ItemData CreateItem(string dir, string fileName, string itemName, ItemCategory cat, int w, int h, float weight, int maxStack = 1)
        {
            AssetDatabase.CreateFolder(dir.Substring(0, dir.LastIndexOf('/')), dir.Substring(dir.LastIndexOf('/') + 1));
            var so = ScriptableObject.CreateInstance<ItemData>();
            so.itemName = itemName;
            so.category = cat;
            so.gridWidth = w; so.gridHeight = h;
            so.weight = weight; so.maxStack = maxStack;
            string path = $"{dir}/{fileName}.asset";
            AssetDatabase.CreateAsset(so, path);
            Debug.Log($"  创建: {itemName}");
            return so;
        }

        // ===== 弹药组件 =====
        static int CreateAmmoComponents(string baseDir)
        {
            int c = 0;
            string dir = baseDir + "/AmmoComponents";
            var cu = GetItem("铜锭"); var pb = GetItem("铅锭");
            var chem = GetItem("化学试剂"); var coal = GetItem("煤矿");
            var salt = GetItem("硝石"); var sulfur = GetItem("硫磺");

            var casing = CreateItem(dir, "Ammo_Casing", "弹壳", ItemCategory.SemiFinished, 1, 1, 0.01f, 50);
            var bullet = CreateItem(dir, "Ammo_Bullet", "弹头", ItemCategory.SemiFinished, 1, 1, 0.01f, 50);
            var primer = CreateItem(dir, "Ammo_Primer", "底火", ItemCategory.SemiFinished, 1, 1, 0.01f, 50);
            c += 3;
            return c;
        }

        // ===== 5 种弹药 =====
        static int CreateAmmo(string baseDir)
        {
            string dir = baseDir + "/Ammo_New";
            var c9mm = CreateItem(dir, "9mm", "9mm手枪弹", ItemCategory.Ammo, 1, 1, 0.02f, 50);
            var c45 = CreateItem(dir, "45ACP", ".45 ACP", ItemCategory.Ammo, 1, 1, 0.03f, 30);
            var c762 = CreateItem(dir, "762mm", "7.62mm步枪弹", ItemCategory.Ammo, 1, 1, 0.04f, 30);
            var c556 = CreateItem(dir, "556mm", "5.56mm步枪弹", ItemCategory.Ammo, 1, 1, 0.03f, 30);
            var c12g = CreateItem(dir, "12Gauge", "12号霰弹", ItemCategory.Ammo, 1, 1, 0.05f, 20);
            return 5;
        }

        // ===== 8 火器 =====
        static int CreateFirearms(string baseDir)
        {
            string dir = baseDir + "/Firearms";
            var steel = GetItem("钢锭"); var parts = GetItem("通用零件");
            var advParts = GetItem("高级零件"); var wood = GetItem("木板");
            var lens = GetItem("光学透镜");

            var m1911 = CreateItem(dir, "M1911", "M1911 手枪", ItemCategory.Equipment, 1, 2, 1.5f);
            m1911.isFirearm = true; m1911.equipSlot = EquipSlot.SidearmBelt;
            m1911.weaponDamage = 45; m1911.fireRate = 0.4f; m1911.magazineSize = 7;
            m1911.ammoItemName = ".45 ACP"; m1911.baseSpread = 2f; m1911.maxSpread = 10f;
            m1911.spreadPerShot = 2f; m1911.spreadRecovery = 6f; m1911.reloadTime = 1.5f;
            m1911.hasDurability = true; m1911.maxDurability = 150;
            // ammoItemData will be set after assets are created
            EditorUtility.SetDirty(m1911);

            var m9 = CreateItem(dir, "M9", "M9 手枪", ItemCategory.Equipment, 1, 2, 1.3f);
            m9.isFirearm = true; m9.equipSlot = EquipSlot.SidearmBelt;
            m9.weaponDamage = 25; m9.fireRate = 0.3f; m9.magazineSize = 15;
            m9.ammoItemName = "9mm手枪弹"; m9.baseSpread = 1.5f; m9.maxSpread = 8f;
            m9.spreadPerShot = 1.5f; m9.spreadRecovery = 5f; m9.reloadTime = 1.3f;
            m9.hasDurability = true; m9.maxDurability = 150;
            EditorUtility.SetDirty(m9);

            var ak47 = CreateItem(dir, "AK47", "AK-47 突击步枪", ItemCategory.Equipment, 1, 4, 4.5f);
            ak47.isFirearm = true; ak47.equipSlot = EquipSlot.RightHand;
            ak47.weaponDamage = 55; ak47.fireRate = 0.15f; ak47.magazineSize = 30;
            ak47.ammoItemName = "7.62mm步枪弹"; ak47.baseSpread = 3f; ak47.maxSpread = 15f;
            ak47.spreadPerShot = 2.5f; ak47.spreadRecovery = 4f; ak47.reloadTime = 2.5f;
            ak47.hasDurability = true; ak47.maxDurability = 300;
            EditorUtility.SetDirty(ak47);

            var m16 = CreateItem(dir, "M16A1", "M16A1 突击步枪", ItemCategory.Equipment, 1, 4, 4f);
            m16.isFirearm = true; m16.equipSlot = EquipSlot.RightHand;
            m16.weaponDamage = 40; m16.fireRate = 0.1f; m16.magazineSize = 20;
            m16.ammoItemName = "5.56mm步枪弹"; m16.baseSpread = 2f; m16.maxSpread = 10f;
            m16.spreadPerShot = 1.5f; m16.spreadRecovery = 5f; m16.reloadTime = 2f;
            m16.hasDurability = true; m16.maxDurability = 280;
            EditorUtility.SetDirty(m16);

            var sks = CreateItem(dir, "SKS", "SKS 半自动步枪", ItemCategory.Equipment, 1, 3, 3.5f);
            sks.isFirearm = true; sks.equipSlot = EquipSlot.RightHand;
            sks.weaponDamage = 60; sks.fireRate = 0.5f; sks.magazineSize = 10;
            sks.ammoItemName = "7.62mm步枪弹"; sks.baseSpread = 1f; sks.maxSpread = 5f;
            sks.spreadPerShot = 1f; sks.spreadRecovery = 7f; sks.reloadTime = 2f;
            sks.hasDurability = true; sks.maxDurability = 250;
            EditorUtility.SetDirty(sks);

            var rem = CreateItem(dir, "Remington870", "雷明顿 870", ItemCategory.Equipment, 1, 4, 4f);
            rem.isFirearm = true; rem.equipSlot = EquipSlot.RightHand;
            rem.weaponDamage = 80; rem.fireRate = 1f; rem.magazineSize = 6;
            rem.ammoItemName = "12号霰弹"; rem.baseSpread = 8f; rem.maxSpread = 20f;
            rem.spreadPerShot = 3f; rem.spreadRecovery = 3f; rem.reloadTime = 3f;
            rem.hasDurability = true; rem.maxDurability = 250;
            EditorUtility.SetDirty(rem);

            var svd = CreateItem(dir, "SVD", "SVD 狙击步枪", ItemCategory.Equipment, 1, 5, 5f);
            svd.isFirearm = true; svd.equipSlot = EquipSlot.RightHand;
            svd.weaponDamage = 100; svd.fireRate = 1.5f; svd.magazineSize = 10;
            svd.ammoItemName = "7.62mm步枪弹"; svd.baseSpread = 0.5f; svd.maxSpread = 3f;
            svd.spreadPerShot = 0.5f; svd.spreadRecovery = 8f; svd.reloadTime = 3f;
            svd.hasDurability = true; svd.maxDurability = 350;
            EditorUtility.SetDirty(svd);

            var uzi = CreateItem(dir, "Uzi", "乌兹冲锋枪", ItemCategory.Equipment, 1, 3, 3f);
            uzi.isFirearm = true; uzi.equipSlot = EquipSlot.RightHand;
            uzi.weaponDamage = 20; uzi.fireRate = 0.05f; uzi.magazineSize = 32;
            uzi.ammoItemName = "9mm手枪弹"; uzi.baseSpread = 5f; uzi.maxSpread = 18f;
            uzi.spreadPerShot = 2f; uzi.spreadRecovery = 3f; uzi.reloadTime = 2f;
            uzi.hasDurability = true; uzi.maxDurability = 250;
            EditorUtility.SetDirty(uzi);

            return 8;
        }

        static int CreateSpecialWeapons(string baseDir)
        {
            string dir = baseDir + "/Firearms";
            var carbon = GetItem("碳纤维"); var coil = GetItem("线圈");
            var cap = GetItem("电容组"); var cb = GetItem("电路板");
            var titanium = GetItem("钛合金");

            var bow = CreateItem(dir, "CompoundBow", "复合弓", ItemCategory.Equipment, 1, 2, 2f);
            bow.isFirearm = false; bow.equipSlot = EquipSlot.RightHand;
            bow.weaponDamage = 35; bow.hasDurability = true; bow.maxDurability = 200;
            EditorUtility.SetDirty(bow);

            return 3; // bow + arrow counted
        }
    }
}
