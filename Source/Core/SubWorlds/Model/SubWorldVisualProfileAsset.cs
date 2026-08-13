using System;

namespace Cultiway.Core.SubWorlds.Model;

/// <summary>
/// 定义小世界视图使用的静态视觉配置。
/// </summary>
/// <remarks>
/// 第一阶段仅建立资产边界，具体相机、光照和覆盖层字段由后续视图实现添加。
/// </remarks>
public sealed class SubWorldVisualProfileAsset : Asset
{
    /// <summary>WORLD 神力分区实例按钮使用的图标路径。</summary>
    public string navigation_icon_path;

    /// <summary>测试 Pawn 复用的原版 Actor Asset ID。</summary>
    public string pawn_actor_asset_id;

    /// <summary>验证视觉配置具备可注册的资产 ID。</summary>
    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(id)) throw new InvalidOperationException("SubWorld VisualProfile 缺少 ID");
        if (string.IsNullOrWhiteSpace(navigation_icon_path))
            throw new InvalidOperationException($"SubWorld VisualProfile 缺少导航图标: {id}");
        if (string.IsNullOrWhiteSpace(pawn_actor_asset_id))
            throw new InvalidOperationException($"SubWorld VisualProfile 缺少 Pawn Actor Asset: {id}");
    }
}
