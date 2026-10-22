using System;
using Cultiway.Content.Components;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>已确认收束的实际结果；请求、实际入账和材料转化分别记录。</summary>
public readonly struct ConstructedRecovery
{
    /// <summary>原始施放及能力编号。</summary>
    public readonly long CastId;
    /// <summary>开始时的完整能力编号。</summary>
    public readonly string AbilityId;
    /// <summary>实际支付资源的原人物，不随反射更改。</summary>
    public readonly long ProviderId;
    /// <summary>本次支付的资源种类。</summary>
    public readonly string Resource;
    /// <summary>本次新增请求及实际入账；入账可包含先前未能表示的小数，累计不超过请求。</summary>
    public readonly double Requested, Granted;
    /// <summary>本次确实转化的材料体积。</summary>
    public readonly double ConvertedVolume;

    /// <summary>只由主线程在完整数值确认后建立。</summary>
    /// <param name="cast">实际施放。</param><param name="ability">实际能力。</param>
    /// <param name="provider">原支付者。</param><param name="resource">原资源。</param>
    /// <param name="requested">请求量。</param><param name="granted">实际入账。</param>
    /// <param name="volume">实际转化体积。</param>
    internal ConstructedRecovery(long cast, string ability, long provider, string resource, double requested, double granted, double volume)
    { CastId = cast; AbilityId = ability; ProviderId = provider; Resource = resource; Requested = requested; Granted = granted; ConvertedVolume = volume; }
}

/// <summary>只在真实提交处返还原支付者，不在预测、渲染或工作线程修改资源。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>真实收束及入账之后发布，便于验证资源去向。</summary>
    public static event Action<ConstructedRecovery> RecoveryConfirmed;

    /// <summary>累计份额与已确认份额求差，整数资源只对累计结果取整。</summary>
    /// <param name="running">已推进到真正共同时间的施放。</param>
    private static void ConfirmRecovery(RunningCast running)
    {
        double total = running.Cast.RecoveredResource;
        if (running.Resource is "mana" or "stamina") total = Math.Floor(total + 1e-8);
        double requested = Math.Max(0, total - running.ConfirmedRecovery);
        double volume = Math.Max(0, running.Cast.RecoveredVolume - running.ConfirmedVolume);
        if (requested == 0 && volume == 0) return;
        running.ConfirmedRecovery = total; running.ConfirmedVolume = running.Cast.RecoveredVolume;
        double available = requested + running.RecoveryFraction; running.RecoveryFraction = 0;
        double granted = 0;
        if (available > 0 && running.InitialOwner.IsLive)
        {
            var owner = running.Source.GetExtend();
            switch (running.Resource)
            {
                case "wakan" when owner.HasCultisys<Xian>():
                    granted = AddBoundedResource(ref owner.GetCultisys<Xian>().wakan, available, owner.Base.stats[BaseStatses.MaxWakan.id], out running.RecoveryFraction); break;
                case "vigor" when owner.HasCultisys<Knight>():
                    granted = AddBoundedResource(ref owner.GetCultisys<Knight>().vigor, available, owner.Base.stats[BaseStatses.MaxVigor.id], out running.RecoveryFraction); break;
                case "mana":
                    int mana = owner.Base.getMana();
                    owner.Base.setMana(mana + Mathf.Min((int)requested, Mathf.Max(0, owner.Base.getMaxMana() - mana)), false);
                    granted = owner.Base.getMana() - mana; break;
                case "stamina":
                    int stamina = owner.Base.data.stamina;
                    owner.Base.data.stamina += Mathf.Min((int)requested, Mathf.Max(0, owner.Base.getMaxStamina() - stamina));
                    granted = owner.Base.data.stamina - stamina; break;
            }
        }
        RecoveryConfirmed?.Invoke(new ConstructedRecovery(running.Id, running.Cast.Plan.Id, running.SourceId,
            running.Resource, requested, granted, volume));
    }

    /// <summary>向下取可表示值，保留小数余额，避免多次浮点加法超出真实预算。</summary>
    /// <param name="balance">原玩法的单精度资源。</param>
    /// <param name="amount">本次可入账的累计余额。</param>
    /// <param name="maximum">真实容量，不夹掉已经存在的超容量存量。</param>
    /// <param name="fraction">只保留精度原因未能入账的部分，容量不足的部分当次损失。</param>
    private static unsafe double AddBoundedResource(ref float balance, double amount, float maximum, out double fraction)
    {
        double before = balance, allowed = Math.Min(amount, Math.Max(0, (double)maximum - before));
        double target = before + allowed; float after = (float)target;
        if (after > target)
        { int bits = *(int*)&after - 1; after = *(float*)&bits; }
        balance = after;
        double granted = (double)after - before;
        fraction = Math.Max(0, allowed - granted); return granted;
    }

}
