using System;

namespace Cultiway.Core.SubWorlds.Model;

/// <summary>
/// 一次自然地图生成使用的独立参数快照。
/// </summary>
public sealed class SubWorldGenerationSettings
{
    /// <summary>第一层地形噪声强度。</summary>
    public int perlin_scale_stage_1 = 5;

    /// <summary>第二层地形噪声强度。</summary>
    public int perlin_scale_stage_2 = 5;

    /// <summary>第三层地形噪声强度。</summary>
    public int perlin_scale_stage_3 = 5;

    /// <summary>是否启用第一层噪声。</summary>
    public bool main_perlin_noise_stage = true;

    /// <summary>是否启用第二层噪声。</summary>
    public bool perlin_noise_stage_2 = true;

    /// <summary>是否启用第三层噪声。</summary>
    public bool perlin_noise_stage_3 = true;

    /// <summary>随机形状数量。</summary>
    public int random_shapes_amount = 5;

    /// <summary>方格地形的单元尺寸。</summary>
    public int cubicle_size = 2;

    /// <summary>是否为陆地添加随机生态地块。</summary>
    public bool random_biomes = true;

    /// <summary>是否在地图边缘增加山地。</summary>
    public bool add_mountain_edges;

    /// <summary>是否生成地表植被地块。</summary>
    public bool add_vegetation = true;

    /// <summary>是否在中心生成湖泊。</summary>
    public bool add_center_lake;

    /// <summary>是否提高中心陆地。</summary>
    public bool add_center_gradient_land = true;

    /// <summary>是否使用圆形边缘渐变。</summary>
    public bool gradient_round_edges = true;

    /// <summary>是否使用方形边缘。</summary>
    public bool square_edges;

    /// <summary>是否叠加环形地形效果。</summary>
    public bool ring_effect;

    /// <summary>是否整体压低地势。</summary>
    public bool low_ground;

    /// <summary>是否整体抬高地势。</summary>
    public bool high_ground;

    /// <summary>是否移除高山地形。</summary>
    public bool remove_mountains;

    /// <summary>
    /// 创建独立副本，保证窗口编辑不会修改注册模板或其他创建请求。
    /// </summary>
    public SubWorldGenerationSettings Clone()
    {
        return new SubWorldGenerationSettings
        {
            perlin_scale_stage_1 = perlin_scale_stage_1,
            perlin_scale_stage_2 = perlin_scale_stage_2,
            perlin_scale_stage_3 = perlin_scale_stage_3,
            main_perlin_noise_stage = main_perlin_noise_stage,
            perlin_noise_stage_2 = perlin_noise_stage_2,
            perlin_noise_stage_3 = perlin_noise_stage_3,
            random_shapes_amount = random_shapes_amount,
            cubicle_size = cubicle_size,
            random_biomes = random_biomes,
            add_mountain_edges = add_mountain_edges,
            add_vegetation = add_vegetation,
            add_center_lake = add_center_lake,
            add_center_gradient_land = add_center_gradient_land,
            gradient_round_edges = gradient_round_edges,
            square_edges = square_edges,
            ring_effect = ring_effect,
            low_ground = low_ground,
            high_ground = high_ground,
            remove_mountains = remove_mountains
        };
    }

    /// <summary>把当前快照限制到生成器和 UI 允许的范围。</summary>
    internal void Clamp()
    {
        perlin_scale_stage_1 = Math.Max(0, Math.Min(30, perlin_scale_stage_1));
        perlin_scale_stage_2 = Math.Max(0, Math.Min(30, perlin_scale_stage_2));
        perlin_scale_stage_3 = Math.Max(0, Math.Min(30, perlin_scale_stage_3));
        random_shapes_amount = Math.Max(0, Math.Min(40, random_shapes_amount));
        cubicle_size = Math.Max(2, Math.Min(15, cubicle_size));
    }
}
