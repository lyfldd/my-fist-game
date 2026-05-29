namespace _Game.Config
{
    /// <summary>
    /// 产业链类型 — 决定物品归属哪条生产线
    /// </summary>
    public enum ChainType
    {
        Metal,        // 金属链：矿石→锭→零件→武器/建筑
        Electronics,  // 电子链：沙→玻璃→电路板→芯片→AI
        Chemical,     // 化学链：煤→精炼煤→煤焦油→汽油 / 硫磺→硫酸→试剂
        Biological,   // 生物医疗链：草药→药品→疫苗
        Food,         // 食品链：生肉→熟食→熏制→罐头
        Energy        // 能源链：铀矿→浓缩铀→聚变核心
    }
}
