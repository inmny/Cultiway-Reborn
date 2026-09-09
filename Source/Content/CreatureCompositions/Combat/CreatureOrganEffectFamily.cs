using System;
using Cultiway.Core;

namespace Cultiway.Content.CreatureCompositions.Combat;

/// <summary>器官被动效果可以响应的稳定事件。</summary>
public enum CreatureOrganEventKind : byte
{
    /// <summary>常规适应阶段，用于器官减伤等数值修正。</summary>
    Adaptation,

    /// <summary>护盾和单次伤害上限之后、致命伤确认时的保命阶段。</summary>
    Survival,

    /// <summary>常规保命失败后的最后手段阶段。</summary>
    LastResort,

    /// <summary>伤害结算已经完成，生命值已经扣除。</summary>
    DamageResolved,

    /// <summary>一次技能施放序列完成。</summary>
    SkillCastCompleted,

    /// <summary>本生物击杀了其他单位。</summary>
    Kill,

    /// <summary>本生物死亡。</summary>
    Death,

    /// <summary>低频生命过程更新，例如脱离战斗后的再生。</summary>
    Upkeep
}

/// <summary>器官效果类别声明响应的事件集合。</summary>
[Flags]
public enum CreatureOrganEventMask : ushort
{
    /// <summary>不响应任何事件。</summary>
    None = 0,

    Adaptation = 1 << CreatureOrganEventKind.Adaptation,
    Survival = 1 << CreatureOrganEventKind.Survival,
    LastResort = 1 << CreatureOrganEventKind.LastResort,
    DamageResolved = 1 << CreatureOrganEventKind.DamageResolved,
    SkillCastCompleted = 1 << CreatureOrganEventKind.SkillCastCompleted,
    Kill = 1 << CreatureOrganEventKind.Kill,
    Death = 1 << CreatureOrganEventKind.Death,
    Upkeep = 1 << CreatureOrganEventKind.Upkeep
}

/// <summary>一次器官被动效果执行时收到的可变上下文。</summary>
public struct CreatureOrganEffectContext
{
    /// <summary>效果所属的生物单位。</summary>
    public ActorExtend Owner;

    /// <summary>伤害或击杀事件的来源；没有来源时为 null。</summary>
    public BaseSimObject Attacker;

    /// <summary>击杀事件中的死亡单位。</summary>
    public Actor Victim;

    /// <summary>技能施放完成事件中的技能容器。</summary>
    public Friflo.Engine.ECS.Entity SkillContainer;

    /// <summary>技能施放完成事件中的发射数量。</summary>
    public int EmittedCount;

    /// <summary>本次伤害数值；数值修正类效果直接改写该字段。</summary>
    public float Damage;

    /// <summary>本次执行的效果类别编号。</summary>
    public string EffectFamilyId;

    /// <summary>触发本次执行的器官等级。</summary>
    public int Rank;

    /// <summary>触发本次执行的器官占用位置。</summary>
    public string SlotId;

    /// <summary>触发本次执行的器官编号。</summary>
    public string OrganId;
}

/// <summary>单个器官效果的实际执行体；按稳定顺序被分发器调用。</summary>
public delegate void CreatureOrganEffectHandler(ref CreatureOrganEffectContext context);

/// <summary>
///     一个可被器官等级引用的被动效果类别。类别本身只声明响应哪些事件；
///     具体等级和器官信息由分发器在执行时提供。
/// </summary>
public sealed class CreatureOrganEffectFamily
{
    /// <summary>创建一个效果类别定义。</summary>
    /// <param name="id">稳定唯一类别编号。</param>
    /// <param name="events">声明响应的事件集合。</param>
    /// <param name="handler">实际执行体。</param>
    public CreatureOrganEffectFamily(string id, CreatureOrganEventMask events, CreatureOrganEffectHandler handler)
    {
        Id = id;
        Events = events;
        Handler = handler;
    }

    /// <summary>类别的稳定唯一编号。</summary>
    public string Id { get; }

    /// <summary>声明响应的事件集合。</summary>
    public CreatureOrganEventMask Events { get; }

    /// <summary>实际执行体。</summary>
    public CreatureOrganEffectHandler Handler { get; }
}

/// <summary>器官被动效果类别的中央登记处；分发器只执行这里注册过的类别。</summary>
public static class CreatureOrganEffectFamilies
{
    private static readonly System.Collections.Generic.Dictionary<string, CreatureOrganEffectFamily> families =
        new(StringComparer.Ordinal);

    /// <summary>登记一个效果类别；重复编号会被拒绝。</summary>
    public static void Register(CreatureOrganEffectFamily family)
    {
        if (family == null || string.IsNullOrEmpty(family.Id)) return;
        families.Add(family.Id, family);
    }

    /// <summary>按编号读取效果类别。</summary>
    public static bool TryGet(string effectFamilyId, out CreatureOrganEffectFamily family)
    {
        return families.TryGetValue(effectFamilyId, out family);
    }
}
