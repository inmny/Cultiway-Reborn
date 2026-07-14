using System;
using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Libraries;
using Cultiway.Core.Progression;
using Cultiway.Utils.Extension;

namespace Cultiway.Content;

[Dependency(typeof(BaseStatses))]
public partial class Cultisyses : ExtendLibrary<BaseCultisysAsset, Cultisyses>
{
    private sealed class AcquisitionRule
    {
        public string CultisysId;
        public Func<ActorExtend, bool> TryAcquire;
    }

    private static readonly List<AcquisitionRule> AcquisitionRules = new();

    protected override bool AutoRegisterAssets() => false;

    /// <summary>
    /// 查询生物所属种族可用的修炼体系 id 集合。
    /// 未配置（集合为空）时返回默认 {Xian}，保持向后兼容。
    /// </summary>
    public static HashSet<string> GetAvailableCultisysIds(ActorExtend ae)
    {
        var configured = ae.Base.asset.GetExtend<ActorAssetExtend>().available_cultisys_ids;
        return configured.Count > 0 ? configured : _default_xian;
    }

    /// <summary>
    /// 按优先级(Xian > Magic > ...)返回生物实际拥有的第一个体系资产，用于 UI 展示。
    /// 无任何体系返回 null。
    /// </summary>
    public static BaseCultisysAsset GetDisplayCultisys(ActorExtend ae)
    {
        if (ae.HasCultisys<Xian>()) return Xian;
        if (ae.HasCultisys<Magic>()) return Magic;
        return null;
    }

    /// <summary>检查角色是否拥有任意一个已注册到统一进阶服务的修炼体系。</summary>
    public static bool HasAnyCultisys(ActorExtend actor)
    {
        if (actor == null) return false;
        var registered = ProgressionService.RegisteredCultisyses;
        for (var i = 0; i < registered.Count; i++)
        {
            if (registered[i].IsOwnedBy(actor)) return true;
        }
        return false;
    }

    /// <summary>
    /// 注册一个修炼体系的幂等准入规则。同一体系再次注册时替换规则但保留执行顺序。
    /// 规则必须自行检查角色是否已拥有该体系，并在成功接入后返回 true。
    /// </summary>
    public static void RegisterAcquisitionRule(string cultisysId, Func<ActorExtend, bool> tryAcquire)
    {
        if (string.IsNullOrEmpty(cultisysId)) throw new ArgumentException("修炼体系 ID 不能为空。", nameof(cultisysId));
        if (tryAcquire == null) throw new ArgumentNullException(nameof(tryAcquire));

        for (var i = 0; i < AcquisitionRules.Count; i++)
        {
            if (AcquisitionRules[i].CultisysId != cultisysId) continue;
            AcquisitionRules[i].TryAcquire = tryAcquire;
            return;
        }
        AcquisitionRules.Add(new AcquisitionRule { CultisysId = cultisysId, TryAcquire = tryAcquire });
    }

    /// <summary>
    /// 按注册顺序重新检查角色能够接入但尚未拥有的全部修炼体系。
    /// 用于角色初生，以及后续获得灵根等会改变体系准入结果的场景。
    /// </summary>
    public static bool RecheckAvailableCultisyses(ActorExtend actor)
    {
        if (actor?.Base == null) return false;
        var changed = false;
        for (var i = 0; i < AcquisitionRules.Count; i++)
            changed |= AcquisitionRules[i].TryAcquire(actor);
        return changed;
    }

    protected override void OnInit()
    {
        InitXian();
        InitMagic();
        ActorExtend.RegisterActionOnNewCreature(actor => RecheckAvailableCultisyses(actor));
        ProgressionLifecycle.RegisterCommitted(BreakthroughVisualTrigger.OnProgressionCommitted);
    }

    public override void OnReload()
    {
        LoadStatsForXian();
        LoadStatsForMagic();
    }
}
