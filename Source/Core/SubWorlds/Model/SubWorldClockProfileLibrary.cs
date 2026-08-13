using System;

namespace Cultiway.Core.SubWorlds.Model;

/// <summary>
/// 注册小世界可使用的时钟配置。
/// </summary>
public sealed class SubWorldClockProfileLibrary : AssetLibrary<SubWorldClockProfileAsset>
{
    /// <summary>标准固定时钟配置的资产 ID。</summary>
    public const string StandardId = "Cultiway.SubWorld.Clock.Standard";

    /// <summary>支持暂停、1x、2x 和 4x 的标准时钟配置。</summary>
    public SubWorldClockProfileAsset Standard { get; private set; }

    /// <summary>注册内置时钟配置。</summary>
    public override void init()
    {
        base.init();
        Standard = add(new SubWorldClockProfileAsset
        {
            id = StandardId,
            fixed_step = 0.1f,
            default_local_rate = 1f,
            allowed_local_speed_options = [0f, 1f, 2f, 4f],
            runs_while_parent_paused = true,
            max_ticks_per_frame = 4
        });
    }

    /// <summary>
    /// 验证并注册一个时钟配置。
    /// </summary>
    /// <param name="asset">待注册的时钟配置。</param>
    /// <returns>完成注册的时钟配置。</returns>
    public override SubWorldClockProfileAsset add(SubWorldClockProfileAsset asset)
    {
        asset.Validate();
        return base.add(asset);
    }

    /// <summary>
    /// 按 ID 获取已注册的时钟配置。
    /// </summary>
    /// <param name="id">时钟配置 ID。</param>
    /// <returns>匹配的时钟配置。</returns>
    /// <exception cref="InvalidOperationException">指定 ID 未注册时抛出。</exception>
    internal SubWorldClockProfileAsset GetRequired(string id)
    {
        SubWorldClockProfileAsset asset = get(id);
        if (asset == null)
            throw new InvalidOperationException($"SubWorld ClockProfile 未注册: {id}");
        return asset;
    }
}
