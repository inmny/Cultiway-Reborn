using System.Collections.Generic;
using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3.Components;

/// <summary>
/// 记录一个法术实体已经命中的对象，供单次挥砍等不可重复命中的轨迹使用。
/// </summary>
public struct SkillHitMemory : IComponent
{
    public HashSet<long> TargetIds;
    public Dictionary<long, float> NextHitTimes;

    public static SkillHitMemory Create()
    {
        return new SkillHitMemory
        {
            TargetIds = new HashSet<long>(),
            NextHitTimes = new Dictionary<long, float>()
        };
    }
}
