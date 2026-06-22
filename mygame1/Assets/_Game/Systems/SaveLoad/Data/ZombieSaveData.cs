using System;

namespace _Game.Systems.SaveLoad
{
    /// <summary>
    /// 僵尸存档数据。仅保存玩家 30m 半径内的僵尸。
    /// </summary>
    [Serializable]
    public class ZombieSaveData : ICloneable
    {
        public string guid;
        public string zombieType;
        public float posX, posY, posZ;
        public float rotY;
        public float hp, maxHp;
        public string currentState;
        public string targetGuid;           // 追击目标 GUID，"PLAYER" = 玩家

        public object Clone()
        {
            return new ZombieSaveData
            {
                guid = this.guid, zombieType = this.zombieType,
                posX = this.posX, posY = this.posY, posZ = this.posZ, rotY = this.rotY,
                hp = this.hp, maxHp = this.maxHp,
                currentState = this.currentState, targetGuid = this.targetGuid,
            };
        }
    }
}
