using UnityEditor;
using UnityEngine;
using _Game.Config;

/// <summary>
/// 一键生成9个职业模板 .asset 文件。
/// 用法：菜单栏 → Game Tools → Create Profession Templates
/// </summary>
public static class CreateProfessionTemplates
{
    const string Dir = "Assets/_Game/Config/Character/Templates";

    // 物品路径常量
    const string EquipDir = "Assets/_Game/Config/Equipment";
    const string WeaponDir = "Assets/_Game/Config/Weapons";
    const string ConsumeDir = "Assets/_Game/Config/Consumables";
    const string MaterialDir = "Assets/_Game/Config/Materials";

    [MenuItem("Game Tools/Create Profession Templates")]
    public static void CreateAll()
    {
        EnsureDir();

        var templates = new System.Collections.Generic.List<CharacterTemplate>();
        templates.Add(CreateFirefighter());
        templates.Add(CreatePoliceOfficer());
        templates.Add(CreateDoctor());
        templates.Add(CreateEngineer());
        templates.Add(CreateSoldier());
        templates.Add(CreateLumberjack());
        templates.Add(CreateVagrant());
        templates.Add(CreateMechanic());
        templates.Add(CreateCreator());

        foreach (var t in templates)
            if (t != null) EditorUtility.SetDirty(t);

        FixDefaultCharacterReference(templates);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("9个职业模板已生成！路径: " + Dir);
    }

    static void FixDefaultCharacterReference(System.Collections.Generic.List<CharacterTemplate> templates)
    {
        const string DefaultCharPath = "Assets/_Game/Config/Character/DefaultCharacter.asset";
        var charData = AssetDatabase.LoadAssetAtPath<CharacterData>(DefaultCharPath);
        if (charData == null)
        {
            Debug.LogWarning($"[CreateProfessionTemplates] 未找到 {DefaultCharPath}");
            return;
        }

        // 如果当前 profession 引用有效则不动
        if (charData.profession != null)
        {
            var currentPath = AssetDatabase.GetAssetPath(charData.profession);
            if (!string.IsNullOrEmpty(currentPath))
            {
                Debug.Log($"[CreateProfessionTemplates] DefaultCharacter.profession 已设置: {charData.profession.templateName}，跳过自动绑定");
                return;
            }
        }

        // 找一个有效的模板作为默认职业（优先流浪者）
        CharacterTemplate fallback = null;
        foreach (var t in templates)
        {
            if (t != null && t.templateName == "流浪者") { fallback = t; break; }
        }
        if (fallback == null)
        {
            foreach (var t in templates)
                if (t != null) { fallback = t; break; }
        }
        if (fallback == null) return;

        charData.profession = fallback;
        EditorUtility.SetDirty(charData);
        Debug.Log($"[CreateProfessionTemplates] DefaultCharacter.profession 已自动绑定: {fallback.templateName}");
    }

    static void EnsureDir()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Game/Config/Character"))
            AssetDatabase.CreateFolder("Assets/_Game/Config", "Character");
        if (!AssetDatabase.IsValidFolder(Dir))
            AssetDatabase.CreateFolder("Assets/_Game/Config/Character", "Templates");
    }

    static ItemData LoadItem(string path) => AssetDatabase.LoadAssetAtPath<ItemData>(path);

    // ── helpers ──
    /// <returns>新模板，已存在则返回 null</returns>
    static CharacterTemplate Create(string name)
    {
        string path = $"{Dir}/{name}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<CharacterTemplate>(path);
        if (existing != null)
        {
            Debug.Log($"[CreateProfessionTemplates] 跳过（已存在）: {path}");
            return null;
        }
        var t = ScriptableObject.CreateInstance<CharacterTemplate>();
        t.templateName = name;
        AssetDatabase.CreateAsset(t, path);
        return t;
    }

    static void AddAttr(CharacterTemplate t, AttributeType type, int mod)
    {
        t.attributeMods.Add(new AttributeMod { attributeType = type, mod = mod });
    }

    static void AddSkill(CharacterTemplate t, SkillType type, int bonus)
    {
        t.skillBoosts.Add(new SkillBoost { skillType = type, bonus = bonus });
    }

    // ================================================================
    // 1. 消防员
    // ================================================================
    static CharacterTemplate CreateFirefighter()
    {
        var t = Create("消防员");
        if (t == null) return null;
        t.description = "消防员出身，力大体壮，近战见长。";

        AddAttr(t, AttributeType.力量, 2);
        AddAttr(t, AttributeType.体质, 1);
        AddSkill(t, SkillType.近战专精, 2);
        AddSkill(t, SkillType.防御专精, 1);

        t.startingEquipment = new ItemData[0];
        t.startingItems = new ItemRequirement[]
        {
            new ItemRequirement { itemData = LoadItem($"{ConsumeDir}/Bandage.asset"), count = 2 },
            new ItemRequirement { itemData = LoadItem($"{ConsumeDir}/EnergyBar.asset"), count = 3 },
            new ItemRequirement { itemData = LoadItem($"{ConsumeDir}/Water.asset"), count = 2 },
        };
        return t;
    }

    // ================================================================
    // 2. 警察
    // ================================================================
    static CharacterTemplate CreatePoliceOfficer()
    {
        var t = Create("警察");
        if (t == null) return null;
        t.description = "退役警员，枪法精准，开局配枪。";

        AddAttr(t, AttributeType.敏捷, 1);
        AddSkill(t, SkillType.枪械专精, 3);
        AddSkill(t, SkillType.防御专精, 1);

        t.startingEquipment = new ItemData[]
        {
            LoadItem($"{EquipDir}/Pants/Jeans.asset"),
        };
        t.startingItems = new ItemRequirement[]
        {
            new ItemRequirement { itemData = LoadItem($"{ConsumeDir}/Bandage.asset"), count = 2 },
            new ItemRequirement { itemData = LoadItem($"{ConsumeDir}/EnergyBar.asset"), count = 2 },
            new ItemRequirement { itemData = LoadItem($"{ConsumeDir}/Water.asset"), count = 1 },
        };
        return t;
    }

    // ================================================================
    // 3. 医生
    // ================================================================
    static CharacterTemplate CreateDoctor()
    {
        var t = Create("医生");
        if (t == null) return null;
        t.description = "战地医生，精通医疗，开局大量药品。";

        AddSkill(t, SkillType.医疗生存, 4);
        AddSkill(t, SkillType.智力, 2);

        t.startingEquipment = new ItemData[]
        {
            LoadItem($"{EquipDir}/Tops/TShirt.asset"),
        };
        t.startingItems = new ItemRequirement[]
        {
            new ItemRequirement { itemData = LoadItem($"{ConsumeDir}/Bandage.asset"), count = 5 },
            new ItemRequirement { itemData = LoadItem($"{ConsumeDir}/Painkiller.asset"), count = 5 },
            new ItemRequirement { itemData = LoadItem($"{ConsumeDir}/Antibiotics.asset"), count = 3 },
            new ItemRequirement { itemData = LoadItem($"{ConsumeDir}/Splint.asset"), count = 3 },
            new ItemRequirement { itemData = LoadItem($"{ConsumeDir}/EnergyBar.asset"), count = 3 },
            new ItemRequirement { itemData = LoadItem($"{ConsumeDir}/Water.asset"), count = 3 },
        };
        return t;
    }

    // ================================================================
    // 4. 工程师
    // ================================================================
    static CharacterTemplate CreateEngineer()
    {
        var t = Create("工程师");
        if (t == null) return null;
        t.description = "工科出身，擅长制造建造，开局大量材料。";

        AddSkill(t, SkillType.工匠制作, 3);
        AddSkill(t, SkillType.建造拆解, 3);
        AddSkill(t, SkillType.智力, 2);

        t.startingEquipment = new ItemData[]
        {
            LoadItem($"{EquipDir}/Tops/TShirt.asset"),
        };
        t.startingItems = new ItemRequirement[]
        {
            new ItemRequirement { itemData = LoadItem($"{MaterialDir}/Wood.asset"), count = 20 },
            new ItemRequirement { itemData = LoadItem($"{MaterialDir}/Nails.asset"), count = 30 },
            new ItemRequirement { itemData = LoadItem($"{MaterialDir}/Cloth.asset"), count = 10 },
            new ItemRequirement { itemData = LoadItem($"{ConsumeDir}/EnergyBar.asset"), count = 3 },
            new ItemRequirement { itemData = LoadItem($"{ConsumeDir}/Water.asset"), count = 2 },
        };
        return t;
    }

    // ================================================================
    // 5. 士兵
    // ================================================================
    static CharacterTemplate CreateSoldier()
    {
        var t = Create("士兵");
        if (t == null) return null;
        t.description = "特种部队退役，战斗全能，开局全套作战装备。";

        AddAttr(t, AttributeType.力量, 2);
        AddAttr(t, AttributeType.敏捷, 1);
        AddAttr(t, AttributeType.耐力, 1);
        AddSkill(t, SkillType.近战专精, 2);
        AddSkill(t, SkillType.枪械专精, 2);
        AddSkill(t, SkillType.防御专精, 2);

        t.startingEquipment = new ItemData[]
        {
            LoadItem($"{EquipDir}/Tops/TacticalJacket.asset"),
            LoadItem($"{EquipDir}/Pants/TacticalPants.asset"),
            LoadItem($"{EquipDir}/Belts/TacticalBelt.asset"),
            LoadItem($"{EquipDir}/Helmets/CombatHelmet.asset"),
        };
        t.startingItems = new ItemRequirement[]
        {
            new ItemRequirement { itemData = LoadItem($"{ConsumeDir}/Bandage.asset"), count = 3 },
            new ItemRequirement { itemData = LoadItem($"{ConsumeDir}/EnergyBar.asset"), count = 5 },
            new ItemRequirement { itemData = LoadItem($"{ConsumeDir}/Water.asset"), count = 3 },
            new ItemRequirement { itemData = LoadItem($"{ConsumeDir}/CanFood.asset"), count = 3 },
        };
        return t;
    }

    // ================================================================
    // 6. 伐木工
    // ================================================================
    static CharacterTemplate CreateLumberjack()
    {
        var t = Create("伐木工");
        if (t == null) return null;
        t.description = "深山伐木工，力大无穷，采集专精。";

        AddAttr(t, AttributeType.力量, 3);
        AddAttr(t, AttributeType.耐力, 2);
        AddSkill(t, SkillType.资源采集, 3);
        AddSkill(t, SkillType.近战专精, 2);

        t.startingEquipment = new ItemData[]
        {
            LoadItem($"{EquipDir}/Pants/Jeans.asset"),
        };
        t.startingItems = new ItemRequirement[]
        {
            new ItemRequirement { itemData = LoadItem($"{ConsumeDir}/Bandage.asset"), count = 2 },
            new ItemRequirement { itemData = LoadItem($"{ConsumeDir}/EnergyBar.asset"), count = 3 },
            new ItemRequirement { itemData = LoadItem($"{MaterialDir}/Wood.asset"), count = 10 },
            new ItemRequirement { itemData = LoadItem($"{ConsumeDir}/Water.asset"), count = 2 },
        };
        return t;
    }

    // ================================================================
    // 7. 流浪者
    // ================================================================
    static CharacterTemplate CreateVagrant()
    {
        var t = Create("流浪者");
        if (t == null) return null;
        t.description = "四海为家的流浪者，耐力惊人，野外生存专家。";

        AddAttr(t, AttributeType.耐力, 3);
        AddAttr(t, AttributeType.体质, 2);
        AddSkill(t, SkillType.野外求生, 4);
        AddSkill(t, SkillType.近战专精, 1);

        t.startingEquipment = new ItemData[0];
        t.startingItems = new ItemRequirement[]
        {
            new ItemRequirement { itemData = LoadItem($"{ConsumeDir}/Bandage.asset"), count = 2 },
            new ItemRequirement { itemData = LoadItem($"{ConsumeDir}/EnergyBar.asset"), count = 2 },
            new ItemRequirement { itemData = LoadItem($"{ConsumeDir}/Coffee.asset"), count = 3 },
            new ItemRequirement { itemData = LoadItem($"{ConsumeDir}/Whiskey.asset"), count = 2 },
            new ItemRequirement { itemData = LoadItem($"{ConsumeDir}/Water.asset"), count = 1 },
        };
        return t;
    }

    // ================================================================
    // 8. 技工
    // ================================================================
    static CharacterTemplate CreateMechanic()
    {
        var t = Create("技工");
        if (t == null) return null;
        t.description = "汽修厂技工，精通载具改装，未来车神。";

        AddSkill(t, SkillType.汽车改造, 4);
        AddSkill(t, SkillType.工匠制作, 2);
        AddSkill(t, SkillType.智力, 1);

        t.startingEquipment = new ItemData[]
        {
            LoadItem($"{EquipDir}/Tops/TShirt.asset"),
            LoadItem($"{EquipDir}/Belts/LeatherBelt.asset"),
        };
        t.startingItems = new ItemRequirement[]
        {
            new ItemRequirement { itemData = LoadItem($"{ConsumeDir}/Bandage.asset"), count = 2 },
            new ItemRequirement { itemData = LoadItem($"{ConsumeDir}/EnergyBar.asset"), count = 3 },
            new ItemRequirement { itemData = LoadItem($"{MaterialDir}/Nails.asset"), count = 20 },
            new ItemRequirement { itemData = LoadItem($"{ConsumeDir}/Water.asset"), count = 2 },
        };
        return t;
    }

    // ================================================================
    // 9. 制作人 (Producer) — 开局AI机器人 + 浓缩铀
    // ================================================================
    static CharacterTemplate CreateCreator()
    {
        var t = Create("制作人");
        if (t == null) return null;
        t.description = "游戏制作人。开局即带AI战斗伙伴+浓缩铀×10，初始智力Lv8。";

        // 属性
        AddAttr(t, AttributeType.力量, 2);
        AddAttr(t, AttributeType.敏捷, 2);
        AddAttr(t, AttributeType.体质, 2);
        AddAttr(t, AttributeType.耐力, 2);

        // 技能：以智力/建造/工匠为核心
        AddSkill(t, SkillType.近战专精, 1);
        AddSkill(t, SkillType.枪械专精, 2);
        AddSkill(t, SkillType.防御专精, 1);
        AddSkill(t, SkillType.资源采集, 1);
        AddSkill(t, SkillType.医疗生存, 1);
        AddSkill(t, SkillType.工匠制作, 3);
        AddSkill(t, SkillType.建造拆解, 3);
        AddSkill(t, SkillType.汽车改造, 1);
        AddSkill(t, SkillType.智力, 8);

        // 基础装备
        t.startingEquipment = new ItemData[]
        {
            LoadItem($"{EquipDir}/Tops/TShirt.asset"),
            LoadItem($"{EquipDir}/Belts/LeatherBelt.asset"),
        };

        // 初始物品：浓缩铀×10 + 基础生存物资
        t.startingItems = new ItemRequirement[]
        {
            new ItemRequirement { itemData = LoadItem("Assets/_Game/Config/Items/SemiFinished/EnrichedUranium.asset"), count = 10 },
            new ItemRequirement { itemData = LoadItem($"{ConsumeDir}/Bandage.asset"), count = 3 },
            new ItemRequirement { itemData = LoadItem($"{ConsumeDir}/EnergyBar.asset"), count = 5 },
            new ItemRequirement { itemData = LoadItem($"{ConsumeDir}/Water.asset"), count = 3 },
        };

        // 特殊：开局自带AI机器人（通过建造系统直接放置）
        t.startWithAIBot = true;
        t.startingAIBotBuildable = AssetDatabase.LoadAssetAtPath<BuildableData>(
            "Assets/_Game/Config/BuildableData/Buildable_AIBot.asset");

        return t;
    }
}
