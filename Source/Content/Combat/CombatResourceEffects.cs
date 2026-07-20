using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Combat;

/// <summary>内容系统共用的生命与灵气转移原语。</summary>
public static class CombatResourceEffects
{
    /// <summary>恢复目标生命，并把正数小数至少折算为一点治疗。</summary>
    public static void RestoreHealth(Actor target, float amount)
    {
        if (target == null || target.isRekt() || amount <= 0f) return;
        target.restoreHealth(Mathf.Max(1, Mathf.RoundToInt(amount)));
    }

    /// <summary>在目标灵气上限内恢复灵气，并返回实际恢复量。</summary>
    public static float RestoreWakan(Actor target, float amount)
    {
        if (target == null || target.isRekt() || amount <= 0f) return 0f;
        ActorExtend extend = target.GetExtend();
        if (!extend.HasCultisys<Xian>()) return 0f;

        ref Xian xian = ref extend.GetCultisys<Xian>();
        float capacity = Mathf.Max(0f, target.stats[BaseStatses.MaxWakan.id]);
        float restored = Mathf.Min(amount, Mathf.Max(0f, capacity - xian.wakan));
        xian.wakan += restored;
        return restored;
    }

    /// <summary>扣除目标现有灵气，并返回实际扣除量。</summary>
    public static float DrainWakan(Actor target, float amount)
    {
        if (target == null || target.isRekt() || amount <= 0f) return 0f;
        ActorExtend extend = target.GetExtend();
        if (!extend.HasCultisys<Xian>()) return 0f;

        ref Xian xian = ref extend.GetCultisys<Xian>();
        float drained = Mathf.Min(amount, Mathf.Max(0f, xian.wakan));
        xian.wakan -= drained;
        return drained;
    }

    /// <summary>仅在目标拥有足额灵气时一次性支付固定消耗。</summary>
    public static bool TrySpendWakan(Actor target, float amount)
    {
        if (target == null || target.isRekt() || amount < 0f) return false;
        if (amount == 0f) return true;
        ActorExtend extend = target.GetExtend();
        if (!extend.HasCultisys<Xian>()) return false;
        ref Xian xian = ref extend.GetCultisys<Xian>();
        if (xian.wakan + 0.0001f < amount) return false;
        xian.wakan -= amount;
        return true;
    }

    /// <summary>把来源灵气转移给目标，并把目标无法容纳的部分退还来源。</summary>
    public static float TransferWakan(Actor source, Actor target, float amount)
    {
        float drained = DrainWakan(source, amount);
        float restored = RestoreWakan(target, drained);
        if (restored < drained) RestoreWakan(source, drained - restored);
        return restored;
    }
}
