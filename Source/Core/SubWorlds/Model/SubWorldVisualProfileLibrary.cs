using System;

namespace Cultiway.Core.SubWorlds.Model;

/// <summary>
/// 注册小世界可使用的视觉配置。
/// </summary>
public sealed class SubWorldVisualProfileLibrary : AssetLibrary<SubWorldVisualProfileAsset>
{
    /// <summary>标准视觉配置的资产 ID。</summary>
    public const string StandardId = "Cultiway.SubWorld.Visual.Standard";

    /// <summary>第一阶段使用的标准视觉配置。</summary>
    public SubWorldVisualProfileAsset Standard { get; private set; }

    /// <summary>注册内置视觉配置。</summary>
    public override void init()
    {
        base.init();
        Standard = add(new SubWorldVisualProfileAsset
        {
            id = StandardId
        });
    }

    /// <summary>
    /// 验证并注册一个视觉配置。
    /// </summary>
    /// <param name="asset">待注册的视觉配置。</param>
    /// <returns>完成注册的视觉配置。</returns>
    public override SubWorldVisualProfileAsset add(SubWorldVisualProfileAsset asset)
    {
        asset.Validate();
        return base.add(asset);
    }

    /// <summary>
    /// 按 ID 获取已注册的视觉配置。
    /// </summary>
    /// <param name="id">视觉配置 ID。</param>
    /// <returns>匹配的视觉配置。</returns>
    /// <exception cref="InvalidOperationException">指定 ID 未注册时抛出。</exception>
    internal SubWorldVisualProfileAsset GetRequired(string id)
    {
        SubWorldVisualProfileAsset asset = get(id);
        if (asset == null)
            throw new InvalidOperationException($"SubWorld VisualProfile 未注册: {id}");
        return asset;
    }
}
