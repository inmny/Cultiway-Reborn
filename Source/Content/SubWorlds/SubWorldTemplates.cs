using System.Reflection;
using Cultiway.Abstract;
using Cultiway.Content.SubWorlds.Ruins.Generation;
using Cultiway.Core.SubWorlds.Model;

namespace Cultiway.Content.SubWorlds;

/// <summary>注册内容层的小世界模板。</summary>
[Dependency(typeof(SubWorldGenerators), typeof(SubWorldVisualProfiles))]
public sealed class SubWorldTemplates : ExtendLibrary<SubWorldTemplateAsset, SubWorldTemplates>
{
    /// <summary>残破古修道场模板。</summary>
    public static SubWorldTemplateAsset RuinedDaoGround { get; private set; }

    protected override bool AutoRegisterAssets() => true;
    protected override string Prefix() => "Cultiway.SubWorld.Template";

    protected override void ActionAfterCreation(PropertyInfo _, SubWorldTemplateAsset asset)
    {
        asset.width = RuinedDaoGroundGeneratorAsset.MapWidth;
        asset.height = RuinedDaoGroundGeneratorAsset.MapHeight;
        asset.generator_id = SubWorldGenerators.RuinedDaoGround.id;
        asset.clock_profile_id = SubWorldClockProfileLibrary.StandardId;
        asset.visual_profile_id = SubWorldVisualProfiles.RuinedDaoGround.id;
    }

    protected override void OnInit()
    {
    }
}
