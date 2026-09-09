using System;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Core;
using UnityEngine;

namespace Cultiway.Content.YaoBeasts;

/// <summary>世界秒时钟：把原版以年为单位的世界时间换算成稳定可比较的秒。</summary>
public static class YaoTime
{
    /// <summary>当前世界时间对应的世界秒。</summary>
    public static float Now => (float)(World.world.getCurWorldTime() * TimeScales.SecPerYear);
}

/// <summary>妖力资源变化的类型。</summary>
public enum YaoPowerMutationKind : byte
{
    /// <summary>增加妖力。</summary>
    Gain,

    /// <summary>支付消耗。</summary>
    Spend,

    /// <summary>校准为指定数值。</summary>
    Set,

    /// <summary>明确清空。</summary>
    Clear
}

/// <summary>妖力资源服务的唯一写入口，负责上限约束与变化通知。</summary>
public static class YaoResourceService
{
    /// <summary>妖力变化提交后的观察者；观察者不得在回调中再次修改同一单位的妖力。</summary>
    public static event Action<ActorExtend, YaoPowerMutation> MutationCommitted;

    /// <summary>在妖力上限内增加妖力，返回实际增加量。</summary>
    public static float Gain(ActorExtend actor, ref Yao yao, float amount)
    {
        if (amount <= 0f) return 0f;
        float previous = Mathf.Max(0f, yao.yao_power);
        float maximum = ResolveMaximum(actor);
        float actual = Mathf.Min(amount, Mathf.Max(0f, maximum - previous));
        Commit(actor, ref yao, previous, previous + actual, YaoPowerMutationKind.Gain);
        return actual;
    }

    /// <summary>消耗现有妖力，返回实际消耗量。</summary>
    public static float Spend(ActorExtend actor, ref Yao yao, float amount)
    {
        if (amount <= 0f) return 0f;
        float previous = Mathf.Max(0f, yao.yao_power);
        float spent = Mathf.Min(amount, previous);
        Commit(actor, ref yao, previous, previous - spent, YaoPowerMutationKind.Spend);
        return spent;
    }

    /// <summary>仅在妖力足够时提交完整支付。</summary>
    public static bool TrySpend(ActorExtend actor, float amount)
    {
        if (actor?.Base == null || amount < 0f || !actor.HasCultisys<Yao>()) return false;
        ref Yao yao = ref actor.GetCultisys<Yao>();
        if (yao.yao_power + 0.001f < amount) return false;
        Spend(actor, ref yao, amount);
        return true;
    }

    /// <summary>按当前妖力上限把妖力校准为指定数值。</summary>
    public static void Set(ActorExtend actor, ref Yao yao, float amount)
    {
        float previous = Mathf.Max(0f, yao.yao_power);
        float current = Mathf.Clamp(amount, 0f, ResolveMaximum(actor));
        Commit(actor, ref yao, previous, current, YaoPowerMutationKind.Set);
    }

    /// <summary>明确清空当前妖力。</summary>
    public static void Clear(ActorExtend actor, ref Yao yao)
    {
        float previous = Mathf.Max(0f, yao.yao_power);
        Commit(actor, ref yao, previous, 0f, YaoPowerMutationKind.Clear);
    }

    private static void Commit(
        ActorExtend actor, ref Yao yao, float previous, float current, YaoPowerMutationKind kind)
    {
        yao.yao_power = current;
        MutationCommitted?.Invoke(actor, new YaoPowerMutation(previous, current, kind));
    }

    /// <summary>读取角色当前有效妖力上限。</summary>
    public static float ResolveMaximum(ActorExtend actor)
    {
        return Mathf.Max(0f, actor.Base.stats[BaseStatses.MaxYaoPower.id]);
    }
}

/// <summary>一条已经提交的妖力资源变化记录。</summary>
public readonly struct YaoPowerMutation
{
    /// <summary>创建一条包含变化前后数值及原因的资源记录。</summary>
    public YaoPowerMutation(float previous, float current, YaoPowerMutationKind kind)
    {
        Previous = previous;
        Current = current;
        Kind = kind;
    }

    /// <summary>变化前的妖力。</summary>
    public float Previous { get; }

    /// <summary>变化后的妖力。</summary>
    public float Current { get; }

    /// <summary>本次变化的资源语义。</summary>
    public YaoPowerMutationKind Kind { get; }

    /// <summary>变化后的数值减去变化前数值。</summary>
    public float Delta => Current - Previous;
}
