using Cultiway.Abstract;
using Cultiway.Content.SubWorlds.Ruins.Generation;
using Cultiway.Core.SubWorlds.Generation;

namespace Cultiway.Content.SubWorlds;

/// <summary>注册内容层的小世界场景生成器。</summary>
public sealed class SubWorldGenerators : ExtendLibrary<SubWorldGeneratorAsset, SubWorldGenerators>
{
    /// <summary>残破古修道场场景生成器。</summary>
    public static RuinedDaoGroundGeneratorAsset RuinedDaoGround { get; private set; }

    protected override bool AutoRegisterAssets() => true;
    protected override string Prefix() => "Cultiway.SubWorld.Generator";

    protected override void OnInit()
    {
    }
}
