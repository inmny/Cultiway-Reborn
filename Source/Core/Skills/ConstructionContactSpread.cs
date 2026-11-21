using System.IO;
using Newtonsoft.Json;

namespace Cultiway.Core.Skills;

/// <summary>真实命中时一次分送的普通球形作用；结果不能再次声明范围。</summary>
public sealed class ConstructionContactSpread
{
    /// <summary>本体伤害确实扣血才分送。</summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public bool AfterDamage;

    /// <summary>沿地图平面距离取人，空中高度不改变半径。</summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public bool GroundPlane;

    /// <summary>跳过本体接触的人物，只作用于其附近对象。</summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public bool ExcludeContactActor;

    /// <summary>实际三维球体的世界半径；选择介质时须为零，不选择介质时零表示不分送。</summary>
    public float Radius;

    /// <summary>可选的实际介质范围；空值沿用球形范围。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionSpreadMedium Medium;

    /// <summary>范围内每个有效人物接收的普通结果，不复制本体其他词条。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionContactOutcome Outcome;

    /// <summary>范围内每座敌方建筑承受的本体余量比例。</summary>
    public float BuildingDamagePerForce;

    /// <summary>已确认球体保留的世界秒数；零表示只结算。</summary>
    public float VisualSeconds = .35f;
}

/// <summary>独立编译的有限球形作用，不持有玩家可修改的数组或结果。</summary>
public sealed class CompiledConstructionContactSpread
{
    internal bool AfterDamage { get; }
    internal bool GroundPlane { get; }
    internal bool ExcludeContactActor { get; }

    /// <summary>与显示共用的真实世界半径。</summary>
    public float Radius { get; }

    /// <summary>可选的活动介质选择。</summary>
    public CompiledSpreadMedium Medium { get; }

    /// <summary>普通人物结果；空值允许仅作用于建筑。</summary>
    public CompiledConstructionContactOutcome Outcome { get; }

    /// <summary>实际建筑伤害比例。</summary>
    public float BuildingDamagePerForce { get; }

    /// <summary>有限显示使用的世界秒数。</summary>
    public float VisualSeconds { get; }

    /// <summary>真实范围且带有直接或继承结果时才建立分送。</summary>
    public bool HasEffect => (Radius > 0f || Medium != null) && (Outcome?.HasEffect == true || BuildingDamagePerForce > 0f || Medium?.InheritContactEffects == true);

    /// <summary>在保存、编辑和编译的资料边界拒绝非法范围及倍率。</summary>
    /// <param name="source">可保存的普通资料。</param><param name="path">错误所在位置。</param>
    internal CompiledConstructionContactSpread(ConstructionContactSpread source, string path)
    {
        if (source == null || !Valid(source.Radius, 10000f) || !Valid(source.BuildingDamagePerForce, 10000f) || !Valid(source.VisualSeconds, 10f))
            throw new InvalidDataException(path + "：半径和建筑比例需要零至10000内有限值，显示需要零至10世界秒");
        if (source.Medium != null)
        {
            if (source.Radius != 0f || source.AfterDamage || source.GroundPlane || source.ExcludeContactActor)
                throw new InvalidDataException(path + "：介质分送不同时声明圆面、扣血触发或圆面目标排除");
            Medium = new CompiledSpreadMedium(source.Medium, path + "/Medium");
        }
        Radius = source.Radius;
        AfterDamage = source.AfterDamage;
        GroundPlane = source.GroundPlane;
        ExcludeContactActor = source.ExcludeContactActor;
        BuildingDamagePerForce = source.BuildingDamagePerForce;
        VisualSeconds = source.VisualSeconds;
        if (source.Outcome != null) Outcome = new CompiledConstructionContactOutcome(source.Outcome, path + "/Outcome");
    }

    /// <summary>有限非负值只在外部资料边界检查。</summary>
    /// <param name="value">外部值。</param><param name="maximum">明确上限。</param>
    private static bool Valid(float value, float maximum) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f && value <= maximum;
}
