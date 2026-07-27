using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3.Components;

public struct SkillContext : IComponent
{
    /// <summary>未额外指定强度时，一次标准技能释放使用的基础强度。</summary>
    public const float DefaultStrength = 100f;

    public float Strength;
    public float PowerLevel;
    public BaseSimObject SourceObj;
    /// <summary>技能创建时记录的来源对象 ID，不受来源对象后续销毁影响。</summary>
    public long SourceId;
    /// <summary>技能创建时记录的来源阵营，不受来源对象后续销毁或转属影响。</summary>
    public Kingdom SourceKingdom;
    public BaseSimObject TargetObj;
    public Kingdom AttackKingdom;
    public Vector3 TargetPos;
    public Vector3 TargetDir;
    public SkillCastRuntimeData RuntimeData;

    /// <summary>返回本次释放规范化后的效果倍率。</summary>
    public readonly float EffectScale => RuntimeData.ResolveEffectScale();

    /// <summary>解析本次释放实际采用的元素构成。</summary>
    public readonly ElementComposition ResolveElement(ElementComposition fallback)
    {
        return RuntimeData.ResolveElement(fallback);
    }

    /// <summary>
    /// 绑定技能来源，并同时保存需要跨越来源对象生命周期使用的稳定信息。
    /// </summary>
    public void BindSource(BaseSimObject source)
    {
        SourceObj = source;
        SourceId = source.getID();
        SourceKingdom = source.kingdom;
    }

    /// <summary>
    /// 返回本次释放用于敌我判定的阵营；显式攻击阵营优先于来源阵营快照。
    /// </summary>
    public readonly Kingdom ResolveAttackKingdom()
    {
        return AttackKingdom ?? SourceKingdom;
    }
}
