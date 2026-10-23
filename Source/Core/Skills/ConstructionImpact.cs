using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Cultiway.Core.SkillLibV3.Usage;

namespace Cultiway.Core.Skills;

/// <summary>实际接触后产生的作用，与显示和选取距离分开保存。</summary>
public sealed class ConstructionImpact
{
    /// <summary>接触允许作用的人物关系；友方包含本人。</summary>
    public SkillUseTargetRelation Targets;

    /// <summary>真实伤害倍率；零表示只接触和推拉，不调用伤害或近战攻击。</summary>
    public float DamageScale = 1f;

    /// <summary>同一实际接触面附带的世界操作；不额外扩大成圆形作用区。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionWorldEffect[] WorldEffects;

    /// <summary>向外的实际受力，负值为牵引；沿用境界和抗击退等规则。</summary>
    public float Force;

    /// <summary>沿用原版受力需要的向上初速度；存在推拉时必须大于零，默认轻微离地。</summary>
    public float Lift = .5f;

    /// <summary>推拉中心采用当前归属者或实际作用形体。</summary>
    public ConstructionForceOrigin Origin;
}

/// <summary>实际推拉的中心，不根据画面或材料推测。</summary>
public enum ConstructionForceOrigin
{
    /// <summary>当前归属者在接触时点的脚下，反射后采用新归属者。</summary>
    Owner = 0,

    /// <summary>实际形体的原点；身体采用真实手部位置。</summary>
    Shape = 1
}

/// <summary>复制及检查后的接触作用，不持有可编辑资料。</summary>
public sealed class CompiledConstructionImpact
{
    /// <summary>正式首版缺省规则。</summary>
    internal static readonly CompiledConstructionImpact Default = new(null, "默认接触作用");

    /// <summary>实际人物筛选。</summary>
    public SkillUseTargetRelation Targets { get; }

    /// <summary>已解析的有限世界操作。</summary>
    public IReadOnlyList<CompiledConstructionWorldEffect> WorldEffects { get; }

    /// <summary>真实伤害倍率。</summary>
    public float DamageScale { get; }

    /// <summary>已检查的有向推拉力。</summary>
    public float Force { get; }

    /// <summary>实际向上初速度，与水平推拉一起经过抗击退。</summary>
    public float Lift { get; }

    /// <summary>实际推拉中心。</summary>
    public ConstructionForceOrigin Origin { get; }

    /// <summary>在创建、编辑和恢复边界统一检查资料。</summary>
    /// <param name="source">可选资料。</param>
    /// <param name="path">错误对应的位置。</param>
    internal CompiledConstructionImpact(ConstructionImpact source, string path)
    {
        if (source?.WorldEffects?.Length > 8) throw new InvalidDataException(path + "：每个作用面最多声明八种世界操作");
        var effects = source?.WorldEffects?.Select((effect, index) =>
            new CompiledConstructionWorldEffect(effect, path + "/WorldEffects/" + index)).ToArray();
        WorldEffects = effects == null || effects.Length == 0
            ? Array.Empty<CompiledConstructionWorldEffect>()
            : Array.AsReadOnly(effects);
        Targets = source?.Targets ?? SkillUseTargetRelation.Hostile;
        DamageScale = source?.DamageScale ?? 1f;
        Force = source?.Force ?? 0f;
        Lift = source?.Lift ?? .5f;
        Origin = source?.Origin ?? ConstructionForceOrigin.Owner;
        if (Targets is not (SkillUseTargetRelation.Hostile or SkillUseTargetRelation.Friendly or SkillUseTargetRelation.Self) ||
            float.IsNaN(DamageScale) || float.IsInfinity(DamageScale) || DamageScale < 0f || DamageScale > 1000f ||
            float.IsNaN(Force) || float.IsInfinity(Force) || Math.Abs(Force) > 1000f ||
            float.IsNaN(Lift) || float.IsInfinity(Lift) || Lift < 0f || Lift > 100f || Force != 0f && Lift == 0f ||
            !Enum.IsDefined(typeof(ConstructionForceOrigin), Origin))
            throw new InvalidDataException(path + "：接触作用需要有效人物关系、0至1000伤害倍率、正负1000以内推拉力及0至100离地力度；推拉时离地力度必须大于零");
    }
}
