using Cultiway.Content.Components;
using Cultiway.Core;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>一次已经提交的灵气资源变化类型。</summary>
public enum WakanMutationKind : byte
{
    /// <summary>从修炼、恢复或其他来源增加灵气。</summary>
    Gain,

    /// <summary>支付法术、法宝或其他明确消耗。</summary>
    Spend,

    /// <summary>把灵气校准为一个指定数值。</summary>
    Set,

    /// <summary>明确清空当前灵气。</summary>
    Clear
}

/// <summary>灵气资源服务提交后发送给观察者的不可变变化记录。</summary>
public readonly struct WakanMutation
{
    /// <summary>创建一条包含变化前后数值及原因的资源记录。</summary>
    public WakanMutation(float previous, float current, WakanMutationKind kind)
    {
        Previous = previous;
        Current = current;
        Kind = kind;
    }

    /// <summary>变化前的灵气。</summary>
    public float Previous { get; }

    /// <summary>变化后的灵气。</summary>
    public float Current { get; }

    /// <summary>本次变化的资源语义。</summary>
    public WakanMutationKind Kind { get; }

    /// <summary>变化后的数值减去变化前数值。</summary>
    public float Delta => Current - Previous;
}

/// <summary>监听一次已经提交的灵气资源变化。</summary>
public delegate void WakanMutationHandler(ActorExtend actor, in WakanMutation mutation);

/// <summary>
/// 仙道灵气的唯一通用写入口，负责上限约束、实际变化量和资源变化通知。
/// 境界系统只能观察变化，不应拥有法术、法宝等通用灵气消耗。
/// </summary>
public static class WakanResourceService
{
    private static event WakanMutationHandler mutationCommitted;

    /// <summary>注册资源变化观察者；观察者不得在回调中再次修改同一角色的灵气。</summary>
    public static void RegisterMutationCommitted(WakanMutationHandler handler)
    {
        mutationCommitted += handler;
    }

    /// <summary>在角色灵气上限内增加灵气，并返回实际增加量。</summary>
    public static float Gain(ActorExtend actor, ref Xian xian, float amount)
    {
        if (amount <= 0f) return 0f;
        float previous = Mathf.Max(0f, xian.wakan);
        float maximum = ResolveMaximum(actor);
        float actual = Mathf.Min(amount, Mathf.Max(0f, maximum - previous));
        float current = previous + actual;
        Commit(actor, ref xian, previous, current, WakanMutationKind.Gain);
        return actual;
    }

    /// <summary>消耗现有灵气，并返回实际消耗量。</summary>
    public static float Spend(ActorExtend actor, ref Xian xian, float amount)
    {
        if (amount <= 0f) return 0f;
        float previous = Mathf.Max(0f, xian.wakan);
        float spent = Mathf.Min(amount, previous);
        Commit(actor, ref xian, previous, previous - spent, WakanMutationKind.Spend);
        return spent;
    }

    /// <summary>仅在现有灵气足以完整支付时提交指定消耗。</summary>
    /// <param name="actor">资源支付者。</param>
    /// <param name="amount">需要完整支付的灵气数值。</param>
    /// <returns>人物有效且灵气足够时返回真。</returns>
    public static bool TrySpend(ActorExtend actor, float amount)
    {
        if (actor?.Base == null || amount < 0f || !actor.HasCultisys<Xian>()) return false;
        ref Xian xian = ref actor.GetCultisys<Xian>();
        if (xian.wakan + 0.001f < amount) return false;
        Spend(actor, ref xian, amount);
        return true;
    }

    /// <summary>仅在现有灵气足以支付上限的指定比例时提交消耗。</summary>
    /// <param name="actor">资源支付者。</param>
    /// <param name="maximumRatio">人物灵气上限的支付比例。</param>
    /// <returns>人物有效且灵气足够时返回真。</returns>
    public static bool TrySpendMaximumRatio(ActorExtend actor, float maximumRatio)
    {
        return actor?.Base != null && maximumRatio >= 0f &&
               TrySpend(actor, ResolveMaximum(actor) * maximumRatio);
    }

    /// <summary>按角色当前灵气上限把灵气设置为指定数值。</summary>
    public static void Set(ActorExtend actor, ref Xian xian, float amount)
    {
        float previous = Mathf.Max(0f, xian.wakan);
        float current = Mathf.Clamp(amount, 0f, ResolveMaximum(actor));
        Commit(actor, ref xian, previous, current, WakanMutationKind.Set);
    }

    /// <summary>明确清空当前灵气。</summary>
    public static void Clear(ActorExtend actor, ref Xian xian)
    {
        float previous = Mathf.Max(0f, xian.wakan);
        Commit(actor, ref xian, previous, 0f, WakanMutationKind.Clear);
    }

    /// <summary>提交资源数值后同步通知所有只读观察者。</summary>
    private static void Commit(
        ActorExtend actor,
        ref Xian xian,
        float previous,
        float current,
        WakanMutationKind kind)
    {
        xian.wakan = current;
        var mutation = new WakanMutation(previous, current, kind);
        mutationCommitted?.Invoke(actor, in mutation);
    }

    /// <summary>读取角色当前有效灵气上限。</summary>
    private static float ResolveMaximum(ActorExtend actor)
    {
        return Mathf.Max(0f, actor.Base.stats[BaseStatses.MaxWakan.id]);
    }
}
