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
    /// <summary>验证视觉配置具备可注册的资产 ID。</summary>
    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(id)) throw new InvalidOperationException("SubWorld VisualProfile 缺少 ID");
    }
}
