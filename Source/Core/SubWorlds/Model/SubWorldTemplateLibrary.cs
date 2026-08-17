using System;
using Cultiway.Core.SubWorlds.Generation;

namespace Cultiway.Core.SubWorlds.Model;

/// <summary>
/// 注册可供 <see cref="SubWorldManager"/> 创建的小世界模板。
/// </summary>
public sealed class SubWorldTemplateLibrary : AssetLibrary<SubWorldTemplateAsset>
{
    /// <summary>最小可玩测试小世界的模板 ID。</summary>
    public const string TestSubWorldId = "Cultiway.SubWorld.Template.TestSubWorld";

    /// <summary>最小可玩测试小世界模板。</summary>
    public SubWorldTemplateAsset TestSubWorld { get; private set; }

    /// <summary>注册内置小世界模板。</summary>
    public override void init()
    {
        base.init();
        TestSubWorld = add(new SubWorldTemplateAsset
        {
            id = TestSubWorldId,
            width = 32,
            height = 32,
            generator_id = SubWorldGeneratorLibrary.TestSubWorldId,
            clock_profile_id = SubWorldClockProfileLibrary.StandardId,
            visual_profile_id = SubWorldVisualProfileLibrary.StandardId
        });
    }

    /// <summary>
    /// 验证并注册一个小世界模板。
    /// </summary>
    /// <param name="asset">待注册的模板。</param>
    /// <returns>完成注册的模板。</returns>
    public override SubWorldTemplateAsset add(SubWorldTemplateAsset asset)
    {
        asset.Validate();
        return base.add(asset);
    }

    /// <summary>
    /// 按 ID 获取已注册的小世界模板。
    /// </summary>
    /// <param name="id">模板 ID。</param>
    /// <returns>匹配的模板。</returns>
    /// <exception cref="InvalidOperationException">指定 ID 未注册时抛出。</exception>
    internal SubWorldTemplateAsset GetRequired(string id)
    {
        SubWorldTemplateAsset asset = get(id);
        if (asset == null)
            throw new InvalidOperationException($"SubWorld Template 未注册: {id}");
        return asset;
    }
}
