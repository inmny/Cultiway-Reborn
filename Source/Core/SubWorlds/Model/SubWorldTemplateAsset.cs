using System;

namespace Cultiway.Core.SubWorlds.Model;

/// <summary>
/// 定义小世界实例创建时使用的静态模板。
/// </summary>
public sealed class SubWorldTemplateAsset : Asset
{
    /// <summary>地图宽度，单位为格。</summary>
    public int width;

    /// <summary>地图高度，单位为格。</summary>
    public int height;

    /// <summary>用于创建初始场景的 <see cref="Generation.SubWorldGeneratorAsset"/> ID。</summary>
    public string generator_id;

    /// <summary>实例时钟使用的 <see cref="SubWorldClockProfileAsset"/> ID。</summary>
    public string clock_profile_id;

    /// <summary>实例视图使用的 <see cref="SubWorldVisualProfileAsset"/> ID。</summary>
    public string visual_profile_id;

    /// <summary>
    /// 验证模板注册所需的尺寸和资产引用。
    /// </summary>
    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(id)) throw new InvalidOperationException("SubWorld Template 缺少 ID");
        if (width < 8) throw new InvalidOperationException($"SubWorld Template 宽度不能小于 8: {id}");
        if (height < 8) throw new InvalidOperationException($"SubWorld Template 高度不能小于 8: {id}");
        if (string.IsNullOrWhiteSpace(generator_id))
            throw new InvalidOperationException($"SubWorld Template 缺少 Generator ID: {id}");
        if (string.IsNullOrWhiteSpace(clock_profile_id))
            throw new InvalidOperationException($"SubWorld Template 缺少 ClockProfile ID: {id}");
        if (string.IsNullOrWhiteSpace(visual_profile_id))
            throw new InvalidOperationException($"SubWorld Template 缺少 VisualProfile ID: {id}");
    }
}

/// <summary>
/// 表示小世界入口在主世界中的锚点坐标。
/// </summary>
internal readonly struct SubWorldAnchor
{
    /// <summary>
    /// 创建主世界锚点。
    /// </summary>
    /// <param name="x">主世界横坐标。</param>
    /// <param name="y">主世界纵坐标。</param>
    internal SubWorldAnchor(int x, int y)
    {
        X = x;
        Y = y;
    }

    /// <summary>主世界横坐标。</summary>
    internal int X { get; }

    /// <summary>主世界纵坐标。</summary>
    internal int Y { get; }
}

/// <summary>
/// 承载一次小世界创建请求的可选参数。
/// </summary>
internal sealed class SubWorldCreationParameters
{
    /// <summary>不带额外参数的共享创建参数。</summary>
    internal static SubWorldCreationParameters Empty { get; } = new();
}
