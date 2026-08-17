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

    /// <summary>是否允许创建时覆盖模板默认尺寸。</summary>
    public bool allow_custom_size;

    /// <summary>是否允许在 WORLD 创建面板中由用户选择。</summary>
    public bool allow_user_creation;

    /// <summary>模板缩略图的原版资源路径。</summary>
    public string icon_path;

    /// <summary>模板默认设置的生成配置 ID。</summary>
    public string generation_profile_id;

    /// <summary>模板在创建网格中的显示顺序。</summary>
    public int display_order;

    /// <summary>模板在创建面板中显示的本地化键。</summary>
    public string display_name_key;

    /// <summary>模板说明的本地化键。</summary>
    public string description_key;

    /// <summary>模板打开创建面板时使用的默认参数。</summary>
    public SubWorldGenerationSettings generation_settings;

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
        if (!allow_user_creation) return;
        if (string.IsNullOrWhiteSpace(icon_path))
            throw new InvalidOperationException($"用户模板缺少缩略图路径: {id}");
        if (string.IsNullOrWhiteSpace(generation_profile_id))
            throw new InvalidOperationException($"用户模板缺少生成配置 ID: {id}");
        if (string.IsNullOrWhiteSpace(display_name_key))
            throw new InvalidOperationException($"用户模板缺少显示名称键: {id}");
        if (generation_settings == null)
            throw new InvalidOperationException($"用户模板缺少默认生成参数: {id}");
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

    internal SubWorldCreationParameters()
    {
    }

    internal SubWorldCreationParameters(int width, int height)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        Width = width;
        Height = height;
    }

    internal SubWorldCreationParameters(int width, int height, SubWorldGenerationSettings settings)
        : this(width, height)
    {
        Settings = settings?.Clone();
        Settings?.Clamp();
    }

    /// <summary>本次创建请求覆盖的地图宽度；零表示使用模板默认值。</summary>
    internal int Width { get; }

    /// <summary>本次创建请求覆盖的地图高度；零表示使用模板默认值。</summary>
    internal int Height { get; }

    /// <summary>本次创建请求冻结的生成参数；空表示使用模板默认参数。</summary>
    internal SubWorldGenerationSettings Settings { get; }
}
