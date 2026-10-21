using Cultiway.Core.SkillLibV3.ActiveAbilities;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>一次真实施放受理的记录，保留来源与调用方式，供实际交战验证使用。</summary>
public readonly struct ConstructedCastStarted
{
    /// <summary>本次施放编号。</summary>
    public readonly long CastId;
    /// <summary>完整构造编号。</summary>
    public readonly string AbilityId;
    /// <summary>人物的稳定编号。</summary>
    public readonly long SourceId;
    /// <summary>本次为玩家、脚本或真正自主出手。</summary>
    public readonly ActiveAbilityUseOrigin Origin;
    /// <summary>记录已经受理的一次施放。</summary>
    /// <param name="cast">施放编号。</param>
    /// <param name="ability">构造编号。</param>
    /// <param name="source">人物编号。</param>
    /// <param name="origin">实际调用方式。</param>
    internal ConstructedCastStarted(long cast, string ability, long source, ActiveAbilityUseOrigin origin)
    { CastId = cast; AbilityId = ability; SourceId = source; Origin = origin; }
}

/// <summary>由共同作用形状确认的真实接触；原版伤害仍按正常抗性和防御结算。</summary>
public readonly struct ConstructedContact
{
    /// <summary>本次施放编号。</summary>
    public readonly long CastId;
    /// <summary>构造及实际来源。</summary>
    public readonly string AbilityId;
    /// <summary>实际来源人物。</summary>
    public readonly long SourceId;
    /// <summary>实际接触人物。</summary>
    public readonly long TargetId;
    /// <summary>发生接触的展开部位，负一为实际身体。</summary>
    public readonly int Node;
    /// <summary>地图空间的真实接触位置。</summary>
    public readonly Vector3 Position;
    /// <summary>由攻击作用面指向目标的实际接触方向，初始深度重叠时可为零。</summary>
    public readonly Vector3 Normal;
    /// <summary>单次施放已经过去的世界秒数。</summary>
    public readonly float Age;
    /// <summary>原版近战结果；生成法术使用Hit表示进入正常伤害事件。</summary>
    public readonly ApplyAttackState Result;

    /// <summary>记录已经确认的接触，不把目标选择或动画时点冒充接触。</summary>
    /// <param name="cast">施放编号。</param>
    /// <param name="ability">构造编号。</param>
    /// <param name="source">来源。</param>
    /// <param name="target">目标。</param>
    /// <param name="node">作用部位。</param>
    /// <param name="position">实际接触位置。</param>
    /// <param name="normal">实际接触方向。</param>
    /// <param name="age">世界年龄。</param>
    /// <param name="result">原版实际受理结果。</param>
    internal ConstructedContact(long cast, string ability, long source, long target, int node,
        Vector3 position, Vector3 normal, float age, ApplyAttackState result)
    { CastId = cast; AbilityId = ability; SourceId = source; TargetId = target; Node = node; Position = position; Normal = normal; Age = age; Result = result; }
}
