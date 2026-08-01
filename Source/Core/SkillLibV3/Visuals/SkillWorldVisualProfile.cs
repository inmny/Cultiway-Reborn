using System;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3.Visuals;

/// <summary>技能落点完成结算时播放的范围视觉类型。</summary>
public enum SkillAreaImpactVisualKind
{
    /// <summary>不播放额外范围动画。</summary>
    None,

    /// <summary>播放两道保持固定线宽的扩散净化波。</summary>
    PurificationWave,
}

/// <summary>单个对象或地块实际发生变化后播放的局部视觉类型。</summary>
public enum SkillLocalEffectVisualKind
{
    /// <summary>不播放局部反馈。</summary>
    None,

    /// <summary>生命恢复光点与嫩叶反馈。</summary>
    Healing,

    /// <summary>回春法阵的目标恢复反馈。</summary>
    Rejuvenation,

    /// <summary>净化目标身上的负面状态。</summary>
    Purification,

    /// <summary>战意祝福的赤色火星反馈。</summary>
    BattleBlessing,

    /// <summary>守护祝福的金属碎片反馈。</summary>
    GuardBlessing,

    /// <summary>迅捷祝福的青色流线反馈。</summary>
    HasteBlessing,

    /// <summary>抬升地形时向外上扬的尘土与碎石。</summary>
    RaiseTerrain,

    /// <summary>降低地形时向内下沉的尘土与碎石。</summary>
    LowerTerrain,

    /// <summary>填水时向外扩散的水纹与气泡。</summary>
    FillWater,

    /// <summary>排水时收缩的三层漩涡与下沉气泡。</summary>
    DrainWater,

    /// <summary>植被实际生成时播放的萌芽与叶片反馈。</summary>
    NatureGrowth,

    /// <summary>根据实际移除的污染类型播放对应碎屑。</summary>
    CleanLand,

    /// <summary>麦田催熟时播放落下的肥料颗粒与短促生长脉冲。</summary>
    Fertilize,
}

/// <summary>一个持续法阵中可独立旋转的环形图元配置。</summary>
public sealed class SkillGlyphRingVisualProfile
{
    /// <summary>沿圆环循环使用的图元资源路径。</summary>
    public string[] GlyphPaths = Array.Empty<string>();

    /// <summary>沿圆周均匀放置的图元数量。</summary>
    public int Count;

    /// <summary>图元中心相对法阵半径的位置。</summary>
    public float RadiusRatio;

    /// <summary>每秒旋转角度；正数为逆时针，负数为顺时针。</summary>
    public float RotationSpeed;

    /// <summary>图元世界尺寸相对法阵半径的倍率。</summary>
    public float SizeRadiusFactor = 0.18f;

    /// <summary>图元允许使用的最小世界尺寸。</summary>
    public float MinWorldSize = 0.32f;

    /// <summary>图元允许使用的最大世界尺寸。</summary>
    public float MaxWorldSize = 0.55f;

    /// <summary>图元基础颜色。</summary>
    public Color Color = Color.white;
}

/// <summary>持续法阵的边界、语义图元、内层结构和题字配置。</summary>
public sealed class SkillFieldVisualProfile
{
    /// <summary>法阵外边界分段数量。</summary>
    public int BoundarySegmentCount = 12;

    /// <summary>单段外边界占据的角度。</summary>
    public float BoundarySegmentDegrees = 24f;

    /// <summary>相邻边界段之间的角度。</summary>
    public float BoundaryGapDegrees = 6f;

    /// <summary>外边界保持不随半径缩放的世界线宽。</summary>
    public float BoundaryWidth = 0.05f;

    /// <summary>法阵出现时顺时针绘制完外边界所需的时间。</summary>
    public float BoundaryDrawDuration = 0.18f;

    /// <summary>法阵中心必须保持完全空白的半径比例。</summary>
    public float ClearCenterRatio = 0.3f;

    /// <summary>外边界颜色。</summary>
    public Color BoundaryColor = Color.white;

    /// <summary>主要语义图元环。</summary>
    public SkillGlyphRingVisualProfile PrimaryRing;

    /// <summary>可选的内层结构图元环。</summary>
    public SkillGlyphRingVisualProfile SecondaryRing;

    /// <summary>可选的可识别题字环。</summary>
    public SkillGlyphRingVisualProfile InscriptionRing;

    /// <summary>半径低于该值时隐藏题字，避免文字互相覆盖。</summary>
    public float InscriptionMinRadius = 2.2f;
}

/// <summary>技能本体声明的世界视觉配置；图标、运行时动画和结算反馈彼此独立。</summary>
public sealed class SkillWorldVisualProfile
{
    /// <summary>持续法阵配置；为空表示该技能不维持法阵。</summary>
    public SkillFieldVisualProfile Field;

    /// <summary>范围结算完成后播放的动画。</summary>
    public SkillAreaImpactVisualKind AreaImpact;

    /// <summary>每个成功对象或地块结算采用的局部反馈。</summary>
    public SkillLocalEffectVisualKind LocalEffect;

    /// <summary>是否关闭元素系统原有的通用地面反馈。</summary>
    public bool SuppressDefaultGroundImpact = true;

    /// <summary>是否关闭飞行期间原有的通用白色粒子与地块扫掠反馈。</summary>
    public bool SuppressDefaultFlyOver = true;

    /// <summary>该技能世界表现的主色。</summary>
    public Color PrimaryColor = Color.white;

    /// <summary>该技能世界表现的辅色。</summary>
    public Color SecondaryColor = Color.white;

    /// <summary>高亮与成功脉冲颜色。</summary>
    public Color GlowColor = Color.white;
}
