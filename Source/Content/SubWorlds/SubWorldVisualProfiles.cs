using System.Reflection;
using Cultiway.Abstract;
using Cultiway.Core.SubWorlds.Model;

namespace Cultiway.Content.SubWorlds;

/// <summary>注册内容层的小世界视觉配置。</summary>
public sealed class SubWorldVisualProfiles : ExtendLibrary<SubWorldVisualProfileAsset, SubWorldVisualProfiles>
{
    /// <summary>残破古修道场视觉配置。</summary>
    public static SubWorldVisualProfileAsset RuinedDaoGround { get; private set; }

    protected override bool AutoRegisterAssets() => true;
    protected override string Prefix() => "Cultiway.SubWorld.Visual";

    protected override void ActionAfterCreation(PropertyInfo _, SubWorldVisualProfileAsset asset)
    {
        asset.navigation_icon_path = "ui/icons/iconCustomWorld";
    }

    protected override void OnInit()
    {
    }
}
