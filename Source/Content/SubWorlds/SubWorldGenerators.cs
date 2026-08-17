using System.Reflection;
using Cultiway.Abstract;
using Cultiway.Content.SubWorlds.Natural.Generation;
using Cultiway.Content.SubWorlds.Ruins.Generation;
using Cultiway.Core.SubWorlds.Generation;

namespace Cultiway.Content.SubWorlds;

/// <summary>注册内容层的小世界场景生成器。</summary>
public sealed class SubWorldGenerators : ExtendLibrary<SubWorldGeneratorAsset, SubWorldGenerators>
{
    /// <summary>自然小世界地图生成器。</summary>
    public static SubWorldGeneratorAsset NaturalWorld { get; private set; }

    /// <summary>残破古修道场场景生成器。</summary>
    public static SubWorldGeneratorAsset RuinedDaoGround { get; private set; }

    protected override bool AutoRegisterAssets() => true;
    protected override string Prefix() => "Cultiway.SubWorld.Generator";

    protected override SubWorldGeneratorAsset CreateAsset(PropertyInfo property)
    {
        return property.Name switch
        {
            nameof(NaturalWorld) => new NaturalWorldGeneratorAsset(),
            nameof(RuinedDaoGround) => new RuinedDaoGroundGeneratorAsset(),
            _ => base.CreateAsset(property)
        };
    }

    protected override void OnInit()
    {
    }
}
