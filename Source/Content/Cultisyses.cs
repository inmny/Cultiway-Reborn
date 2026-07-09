using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Libraries;
using Cultiway.Utils.Extension;

namespace Cultiway.Content;

[Dependency(typeof(BaseStatses))]
public partial class Cultisyses : ExtendLibrary<BaseCultisysAsset, Cultisyses>
{
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

    protected override void OnInit()
    {
        InitXian();
        InitMagic();
    }

    public override void OnReload()
    {
        LoadStatsForXian();
    }
}
