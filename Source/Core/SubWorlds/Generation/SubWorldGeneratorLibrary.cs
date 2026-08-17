using System;

namespace Cultiway.Core.SubWorlds.Generation;

/// <summary>
/// 注册可由小世界模板引用的场景生成器资产。
/// </summary>
public sealed class SubWorldGeneratorLibrary : AssetLibrary<SubWorldGeneratorAsset>
{
    /// <summary>最小可玩测试场景生成器的资产 ID。</summary>
    public const string TestSubWorldId = "Cultiway.SubWorld.Generator.TestSubWorld";

    /// <summary>最小可玩测试场景生成器。</summary>
    public SubWorldGeneratorAsset TestSubWorld { get; private set; }

    /// <summary>注册内置场景生成器。</summary>
    public override void init()
    {
        base.init();
        TestSubWorld = add(new TestSubWorldGeneratorAsset
        {
            id = TestSubWorldId
        });
    }

    /// <summary>
    /// 验证并注册一个场景生成器。
    /// </summary>
    /// <param name="asset">待注册的生成器。</param>
    /// <returns>完成注册的生成器。</returns>
    public override SubWorldGeneratorAsset add(SubWorldGeneratorAsset asset)
    {
        asset.Validate();
        return base.add(asset);
    }

    /// <summary>
    /// 按 ID 获取已注册的场景生成器。
    /// </summary>
    /// <param name="id">生成器 Asset ID。</param>
    /// <returns>匹配的生成器。</returns>
    /// <exception cref="InvalidOperationException">指定 ID 未注册时抛出。</exception>
    internal SubWorldGeneratorAsset GetRequired(string id)
    {
        SubWorldGeneratorAsset asset = get(id);
        if (asset == null)
            throw new InvalidOperationException($"SubWorld Generator 未注册: {id}");
        return asset;
    }
}
