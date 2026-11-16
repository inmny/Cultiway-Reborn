using System.IO;
using Newtonsoft.Json;

namespace Cultiway.Core.Skills;

/// <summary>真实命中时一次分送的普通球形作用；结果不能再次声明范围。</summary>
public sealed class ConstructionContactSpread
{
    /// <summary>实际三维球体的世界半径，零表示不分送。</summary>
    public float Radius;

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
    /// <summary>与显示共用的真实世界半径。</summary>
    public float Radius { get; }

    /// <summary>普通人物结果；空值允许仅作用于建筑。</summary>
    public CompiledConstructionContactOutcome Outcome { get; }

    /// <summary>实际建筑伤害比例。</summary>
    public float BuildingDamagePerForce { get; }

    /// <summary>有限显示使用的世界秒数。</summary>
    public float VisualSeconds { get; }

    /// <summary>零半径或完全空结果不建立范围作用。</summary>
    public bool HasEffect => Radius > 0f && (Outcome?.HasEffect == true || BuildingDamagePerForce > 0f);

    /// <summary>在保存、编辑和编译的资料边界拒绝非法范围及倍率。</summary>
    /// <param name="source">可保存的普通资料。</param><param name="path">错误所在位置。</param>
    internal CompiledConstructionContactSpread(ConstructionContactSpread source, string path)
    {
        if (source == null || !Valid(source.Radius, 10000f) || !Valid(source.BuildingDamagePerForce, 10000f) || !Valid(source.VisualSeconds, 10f))
            throw new InvalidDataException(path + "：半径和建筑比例需要零至10000内有限值，显示需要零至10世界秒");
        Radius = source.Radius;
        BuildingDamagePerForce = source.BuildingDamagePerForce;
        VisualSeconds = source.VisualSeconds;
        if (source.Outcome != null) Outcome = new CompiledConstructionContactOutcome(source.Outcome, path + "/Outcome");
    }

    /// <summary>有限非负值只在外部资料边界检查。</summary>
    /// <param name="value">外部值。</param><param name="maximum">明确上限。</param>
    private static bool Valid(float value, float maximum) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f && value <= maximum;
}
