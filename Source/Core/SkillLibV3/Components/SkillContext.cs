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
    public BaseSimObject TargetObj;
    public Kingdom AttackKingdom;
    public Vector3 TargetPos;
    public Vector3 TargetDir;
}
