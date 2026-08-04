using System;

namespace Cultiway.Core.Combat;

/// <summary>标识一次伤害属于主动伤害还是由其他效果派生的二次反应伤害。</summary>
public enum DamageOrigin : byte
{
    /// <summary>普通攻击、主动技能或其他允许继续触发被动效果的主伤害。</summary>
    Primary,

    /// <summary>反击、持续伤害和追加伤害等不应再次触发同类被动的二次伤害。</summary>
    Reaction,
}

/// <summary>
/// 保存当前实际受击结算栈的伤害来源类别和跨事件外部作用域。
/// 来源必须在事件出队并调用 <see cref="ActorExtend.GetHit"/> 时建立，不能只包围事件入队。
/// </summary>
public static class DamageResolutionContext
{
    [ThreadStatic]
    private static DamageOrigin currentOrigin;

    [ThreadStatic]
    private static int depth;

    [ThreadStatic]
    private static long currentSourceScopeId;

    /// <summary>当前结算栈的伤害来源；没有活动结算时返回主动伤害。</summary>
    public static DamageOrigin CurrentOrigin => depth > 0 ? currentOrigin : DamageOrigin.Primary;

    /// <summary>当前是否正在结算二次反应伤害。</summary>
    public static bool IsReaction => CurrentOrigin == DamageOrigin.Reaction;

    /// <summary>当前伤害入队时冻结的外部来源作用域；没有活动结算时为零。</summary>
    public static long CurrentSourceScopeId => depth > 0 ? currentSourceScopeId : 0;

    /// <summary>进入一次伤害结算，并在释放返回值时恢复外层上下文。</summary>
    internal static Scope Enter(DamageOrigin origin, long sourceScopeId = 0)
    {
        var scope = new Scope(currentOrigin, currentSourceScopeId, depth);
        currentOrigin = origin;
        currentSourceScopeId = sourceScopeId;
        depth++;
        return scope;
    }

    /// <summary>负责恢复嵌套伤害结算上下文的作用域。</summary>
    internal readonly struct Scope : IDisposable
    {
        private readonly DamageOrigin previousOrigin;
        private readonly long previousSourceScopeId;
        private readonly int previousDepth;

        /// <summary>保存进入当前结算前的上下文。</summary>
        internal Scope(DamageOrigin origin, long sourceScopeId, int contextDepth)
        {
            previousOrigin = origin;
            previousSourceScopeId = sourceScopeId;
            previousDepth = contextDepth;
        }

        /// <summary>恢复外层伤害来源和嵌套深度。</summary>
        public void Dispose()
        {
            currentOrigin = previousOrigin;
            currentSourceScopeId = previousSourceScopeId;
            depth = previousDepth;
        }
    }
}
