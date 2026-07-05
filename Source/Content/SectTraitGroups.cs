using Cultiway.Abstract;
using Cultiway.Core.Libraries;

namespace Cultiway.Content;

/// <summary>
/// 宗门特质分组集合。
/// </summary>
public class SectTraitGroups : ExtendLibrary<SectTraitGroupAsset, SectTraitGroups>
{
    /// <summary>
    /// 驻地策略分组。
    /// </summary>
    public static SectTraitGroupAsset ResidenceStrategy { get; private set; }

    protected override bool AutoRegisterAssets() => true;
    protected override string Prefix() => "Cultiway.Sect.TraitGroup";

    protected override void OnInit()
    {
        ResidenceStrategy.name = "sect_trait_group_residence_strategy";
        ResidenceStrategy.color = "#8fd17f";
    }
}
