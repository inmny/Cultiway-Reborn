using System;

namespace Cultiway.Core.SkillLibV3.ActiveAbilities;

/// <summary>一次技能释放实际使用的战斗载体类型。</summary>
public enum SkillCarrierKind : byte
{
    /// <summary>普通物质肉身。</summary>
    Physical,

    /// <summary>临时命魂人物。</summary>
    Soul,

    /// <summary>没有人物身份的元神节点。</summary>
    Node,
}

/// <summary>把技能所有者和实际执行技能的载体放在同一个冻结上下文中。</summary>
public readonly struct SkillCasterContext
{
    /// <summary>技能、资源、冷却和最终归属所属的人物。</summary>
    public readonly ActorExtend Owner;

    /// <summary>实际提供位置、碰撞和自身目标的战斗载体。</summary>
    public readonly ActorExtend Carrier;

    /// <summary>当前载体的心神效果倍率。</summary>
    public readonly float EffectScale;

    /// <summary>当前载体的类型。</summary>
    public readonly SkillCarrierKind Kind;

    /// <summary>当前载体是否仍然拥有物质肉身。</summary>
    public readonly bool HasPhysicalBody;

    /// <summary>创建一次技能使用上下文。</summary>
    public SkillCasterContext(
        ActorExtend owner,
        ActorExtend carrier,
        float effectScale = 1f,
        SkillCarrierKind kind = SkillCarrierKind.Physical,
        bool hasPhysicalBody = true)
    {
        Owner = owner;
        Carrier = carrier ?? owner;
        EffectScale = Math.Max(0f, effectScale);
        Kind = kind;
        HasPhysicalBody = hasPhysicalBody;
    }

    /// <summary>判断上下文中的人物和载体仍可执行技能。</summary>
    public bool IsValid => Owner?.Base != null && !Owner.Base.isRekt() &&
                           Carrier?.Base != null && !Carrier.Base.isRekt();
}

/// <summary>内容系统为临时载体注册所有者解析器，核心技能库不依赖具体元神组件。</summary>
public static class SkillCasterContextService
{
    private static Func<ActorExtend, SkillCasterContext?> ownerResolver;

    [ThreadStatic]
    private static SkillCasterContext? current;

    /// <summary>注册内容侧的载体解析器。</summary>
    /// <param name="resolver">不是临时载体时返回空；载体失去所有者时也返回空。</param>
    public static void RegisterResolver(Func<ActorExtend, SkillCasterContext?> resolver)
    {
        ownerResolver += resolver;
    }

    /// <summary>把请求对象解析为技能所有者和实际载体。</summary>
    /// <param name="requested">能力栏或战斗入口传入的对象。</param>
    /// <returns>有效的冻结上下文；普通人物使用自身作为两种来源。</returns>
    public static SkillCasterContext Resolve(ActorExtend requested)
    {
        if (requested == null) return default;
        if (current.HasValue && current.Value.Owner == requested && current.Value.IsValid)
            return current.Value;
        if (ownerResolver != null)
        {
            Delegate[] resolvers = ownerResolver.GetInvocationList();
            for (int i = 0; i < resolvers.Length; i++)
            {
                SkillCasterContext? resolved = ((Func<ActorExtend, SkillCasterContext?>)resolvers[i])(requested);
                if (resolved.HasValue) return resolved.Value;
            }
        }

        return new SkillCasterContext(requested, requested);
    }

    /// <summary>取得当前调用栈已经冻结的载体上下文。</summary>
    /// <param name="owner">需要匹配的技能所有者。</param>
    /// <param name="context">返回当前载体上下文。</param>
    /// <returns>当前调用正在为该所有者处理临时载体时返回真。</returns>
    public static bool TryGetCurrent(ActorExtend owner, out SkillCasterContext context)
    {
        if (current.HasValue && current.Value.Owner == owner && current.Value.IsValid)
        {
            context = current.Value;
            return true;
        }

        context = default;
        return false;
    }

    /// <summary>进入一个已经冻结的载体调用范围，退出时恢复外层范围。</summary>
    /// <param name="context">本次调用要使用的完整载体上下文。</param>
    /// <returns>范围令牌。</returns>
    public static Scope Enter(in SkillCasterContext context)
    {
        SkillCasterContext? previous = current;
        current = context.IsValid ? context : null;
        return new Scope(previous);
    }

    /// <summary>进入一次载体调用范围，退出时恢复外层范围。</summary>
    /// <param name="requested">主动能力调用方。</param>
    /// <returns>范围令牌。</returns>
    public static Scope Enter(ActorExtend requested)
    {
        SkillCasterContext? previous = current;
        SkillCasterContext resolved = Resolve(requested);
        current = resolved.IsValid ? resolved : null;
        return new Scope(previous);
    }

    /// <summary>一次载体调用范围的恢复令牌。</summary>
    public readonly struct Scope : IDisposable
    {
        private readonly SkillCasterContext? previous;

        /// <summary>创建恢复令牌。</summary>
        /// <param name="previous">进入前的上下文。</param>
        public Scope(SkillCasterContext? previous)
        {
            this.previous = previous;
        }

        /// <summary>恢复进入前的上下文。</summary>
        public void Dispose()
        {
            current = previous;
        }
    }
}
