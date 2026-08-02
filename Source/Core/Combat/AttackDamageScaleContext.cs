using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace Cultiway.Core.Combat;

/// <summary>
/// 在不绕过原版攻击流程的前提下，临时缩放当前线程的一次攻击伤害。
/// 投射物通过稳定资产 ID 进入作用域，近战执行体则显式进入和退出作用域。
/// </summary>
public static class AttackDamageScaleContext
{
    private static readonly ConcurrentDictionary<string, float> ProjectileMultipliers =
        new(StringComparer.Ordinal);

    [ThreadStatic]
    private static float currentMultiplier;

    [ThreadStatic]
    private static bool hasCurrentMultiplier;

    /// <summary>登记一种需要按固定倍率结算的投射物资产。</summary>
    public static void RegisterProjectile(string projectileAssetId, float multiplier)
    {
        if (string.IsNullOrWhiteSpace(projectileAssetId))
            throw new ArgumentException("投射物资产 ID 不能为空", nameof(projectileAssetId));
        ProjectileMultipliers[projectileAssetId] = Mathf.Max(0f, multiplier);
    }

    /// <summary>根据攻击数据进入对应投射物倍率作用域，并返回进入前的倍率。</summary>
    public static float Enter(AttackData attackData)
    {
        float multiplier = 1f;
        if (attackData.is_projectile && !string.IsNullOrEmpty(attackData.projectile_id) &&
            ProjectileMultipliers.TryGetValue(attackData.projectile_id, out float registered))
        {
            multiplier = registered;
        }
        return Enter(multiplier);
    }

    /// <summary>进入显式倍率作用域，并返回进入前的倍率供 finally 恢复。</summary>
    public static float Enter(float multiplier)
    {
        float previous = ResolveCurrent();
        currentMultiplier = previous * Mathf.Max(0f, multiplier);
        hasCurrentMultiplier = true;
        return previous;
    }

    /// <summary>恢复到进入作用域前的倍率。</summary>
    public static void Restore(float previousMultiplier)
    {
        currentMultiplier = Mathf.Max(0f, previousMultiplier);
        hasCurrentMultiplier = true;
    }

    /// <summary>把当前线程的攻击倍率应用到即将交给目标的伤害值。</summary>
    public static float Apply(float damage)
    {
        return damage * ResolveCurrent();
    }

    /// <summary>把线程默认的零值解释为未缩放。</summary>
    private static float ResolveCurrent()
    {
        return hasCurrentMultiplier ? currentMultiplier : 1f;
    }
}
