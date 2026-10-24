using Cultiway.Core.SkillLibV3.Components;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3;

/// <summary>一个已经受理的发射步骤；收费和步骤数由原来源维护，执行入口只能接管一次。</summary>
public struct SkillEmission
{
    /// <summary>实际已学或来源授予的容器。</summary>
    public Entity SkillContainer;

    /// <summary>没有施法人物的世界来源。</summary>
    public bool Sourceless;

    /// <summary>实际归属者与空间载体。</summary>
    public BaseSimObject Source, SpatialSource;

    /// <summary>已经确定的出生位置。</summary>
    public Vector3 SourcePos;

    /// <summary>是否已有步骤固定坐标或实际释放时解析出的身体出口。</summary>
    public bool HasSourcePosition;

    /// <summary>实际跟踪对象及本步骤确定的目标位置。</summary>
    public BaseSimObject Target;

    /// <summary>本步骤确定的目标位置。</summary>
    public Vector3 TargetPos;

    /// <summary>来源给出的实际强度与功率。</summary>
    public float Strength, PowerLevel;

    /// <summary>齐射的实际偏角。</summary>
    public float InitialAngleOffsetDegrees;

    /// <summary>本次行动的攻击归属。</summary>
    public Kingdom AttackKingdom;

    /// <summary>本次关联、来源方式和世界任务边界。</summary>
    public SkillCastRuntimeData RuntimeData;
}
