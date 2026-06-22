using System;
using System.Collections.Generic;

namespace _Game.Systems.SaveLoad
{
    [Serializable]
    public class ResearchSaveData : ICloneable
    {
        public List<string> completedResearchIds;

        public object Clone()
        {
            return new ResearchSaveData
            {
                completedResearchIds = this.completedResearchIds != null
                    ? new List<string>(this.completedResearchIds) : null,
            };
        }
    }
}
