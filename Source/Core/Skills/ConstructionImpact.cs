using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Cultiway.Core.SkillLibV3.Usage;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>实际命中被抓人物后，解除已经存在的身体抓持。</summary>
public enum ConstructionGrabRelease
{
    /// <summary>不解除抓持。</summary>
    None = 0,
    /// <summary>只放开当前施术者自己抓住的人。</summary>
    Own = 1,
    /// <summary>通过余力与抗性比较，解除当前人物受到的实际抓持。</summary>
    Rescue = 2
}

/// <summary>真实接触时决定灵气交付方向。</summary>
public enum ConstructionWakanTransferMode
{
    /// <summary>从接触对象转给当前归属者，保留原来的汲取行为。</summary>
    Draw = 0,
    /// <summary>每段从灵气比例较高的一端渡向较低的一端，到持平为止。</summary>
    Balance = 1
}

/// <summary>实际接触后产生的作用，与显示和选取距离分开保存。</summary>
public sealed class ConstructionImpact
{
    /// <summary>接触允许作用的人物关系；友方包含本人。</summary>
    public SkillUseTargetRelation Targets;

    /// <summary>仅作用于本次明确选中的人物，仍需真实接触及关系匹配。</summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public bool SelectedTargetOnly;

    /// <summary>真实接触附带的即时恢复、持续恢复及净化。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionRestoration Restoration;

    /// <summary>明确解除实际抓持，省略时与旧接触作用相同。</summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public ConstructionGrabRelease ReleaseGrab;

    /// <summary>实际接触附带的已登记属性状态，空值表示不施加。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionStatus[] Statuses;

    /// <summary>本作用面改用真实身体表面接触，并在碰到的可见部件上留下薄霜。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionFrostCoating FrostCoating;

    /// <summary>真实伤害倍率；零表示只接触和推拉，不调用伤害或近战攻击。</summary>
    public float DamageScale = 1f;

    /// <summary>每次真实命中推进的整数年数，零表示没有生灵老化。</summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public int AgeYears;

    /// <summary>每次实际命中可转移的灵气上限。</summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public float WakanTransfer;

    /// <summary>缺省保持单向汲取；选择平衡时由两个现有余额决定送出方。</summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public ConstructionWakanTransferMode WakanTransferMode;

    /// <summary>本次真实命中腐蚀的明确装备栏，空值表示没有装备腐蚀。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionEquipmentErosion EquipmentErosion;

    /// <summary>真实命中后用本次借用的材料修复明确装备栏中的原物品。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionEquipmentRepair EquipmentRepair;

    /// <summary>真正接触后送到收件人物的讯号。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionSignal Signal;

    /// <summary>真实命中后推进当前库存物件的已有有限寿命。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionInventoryAging InventoryAging;

    /// <summary>可选的明确本体伤害组成；省略时随当时实际材料，零组成表示通用伤害。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ElementComposition? Element;

    /// <summary>伤害采用当前剩余量或本次部位的初始量；防御容量仍单独消耗。</summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public ConstructionDamageBasis DamageBasis;

    /// <summary>同一实际接触面附带的世界操作；不额外扩大成圆形作用区。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionWorldEffect[] WorldEffects;

    /// <summary>向外的实际受力，负值为牵引；沿用境界和抗击退等规则。</summary>
    public float Force;

    /// <summary>按真实接触中心和目标当前质量换算的击退与腾起，空值表示没有此作用。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionKnockback Knockback;

    /// <summary>真实接触后与当前控制人物交换地面位置，空值不改变位置。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionExchange Exchange;

    /// <summary>同一次真实命中后按顺序追加的独立元素伤害。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionContactDamage[] AdditionalDamage;

    /// <summary>真实命中后至多抽取一个的有限候选作用。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionRandomContact RandomContact;

    /// <summary>按真实生命及本条伤害结算结果顺序执行的有限条件作用。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionConditionalContact[] ConditionalContacts;

    /// <summary>每次真实命中后在当时位置分送的有限球形作用，不递归触发本体词条。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionContactSpread[] Spreads;

    /// <summary>精确定向速度变化：依次为离开作用中心、侧向和向上；负值用于回拉、下压。空值使用原版推力。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public float[] Impulse;

    /// <summary>明确发力接触后的有限转移与卸力，空值沿用本体承受全部来力。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionForceRouting ForceRouting;

    /// <summary>腾起使用的向上初速度，默认轻微离地；沿地推拉不使用此值。</summary>
    public float Lift = .5f;

    /// <summary>没有水平推拉时仍明确施加离地力度；默认关闭，保持已有资料的含义。</summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public bool Launch;

    /// <summary>大于零时改为沿地面推拉的世界秒数，此时不使用离地力度。</summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public float SlideSeconds;

    /// <summary>受击停顿的世界秒数；停止自身行动，但允许外力推动。</summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public float StaggerSeconds;

    /// <summary>束缚的世界秒数；停止水平运动及自身行动，已有腾起仍会自然下落。</summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public float BindSeconds;

    /// <summary>接触造成攻击能力受损的世界秒数，不附加烟团。</summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public float StrainSeconds;

    /// <summary>接触造成实际护甲降低的世界秒数，不绕过格挡。</summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public float ExposureSeconds;

    /// <summary>推拉中心采用当前归属者或实际作用形体。</summary>
    public ConstructionForceOrigin Origin;
}

/// <summary>接触伤害的强度来源，不改变屏障容量或交锋损耗。</summary>
public enum ConstructionDamageBasis
{
    /// <summary>随交锋后剩余作用量衰减，沿用已有资料。</summary>
    RemainingForce = 0,

    /// <summary>采用本部位本次生成时的作用量，容量受损不降低伤害。</summary>
    InitialForce = 1
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

    /// <summary>是否只接受本次选中人物。</summary>
    public bool SelectedTargetOnly { get; }

    /// <summary>实际部件接触和附着霜渍资料；空值沿用普通人物接触。</summary>
    public CompiledFrostCoating FrostCoating { get; }

    /// <summary>已检查并复制的恢复与净化资料。</summary>
    public CompiledConstructionRestoration Restoration { get; }

    /// <summary>已检查的抓持解除方式。</summary>
    public ConstructionGrabRelease ReleaseGrab { get; }

    /// <summary>已解析并复制的属性状态作用。</summary>
    public IReadOnlyList<CompiledConstructionStatus> Statuses { get; }
    /// <summary>这份作用包含等待本次实际伤害的状态。</summary>
    internal bool HasStatusesAfterDamage { get; }

    /// <summary>真实伤害倍率。</summary>
    public float DamageScale { get; }

    /// <summary>每次真实命中推进的整数年数，接触衰减后向下取整。</summary>
    public int AgeYears { get; }

    /// <summary>已经核对并复制的明确装备腐蚀规则。</summary>
    public CompiledEquipmentErosion EquipmentErosion { get; }

    /// <summary>已核对的装备修复规则。</summary>
    public CompiledEquipmentRepair EquipmentRepair { get; }

    /// <summary>已经检查的实际送达规则。</summary>
    public CompiledConstructionSignal Signal { get; }

    /// <summary>已复制并检查的库存物件寿命推进。</summary>
    public CompiledInventoryAging InventoryAging { get; }

    /// <summary>经检查的单次灵气转移上限；零表示不转移。</summary>
    public float WakanTransfer { get; }

    /// <summary>已核对的单向汲取或双端平衡规则。</summary>
    public ConstructionWakanTransferMode WakanTransferMode { get; }

    /// <summary>已经独立复制的明确组成；空值继续读取接触当时的真实材料。</summary>
    public ElementComposition? Element { get; }

    /// <summary>已检查的伤害强度来源。</summary>
    public ConstructionDamageBasis DamageBasis { get; }

    /// <summary>已检查的有向推拉力。</summary>
    public float Force { get; }

    /// <summary>已独立复制的击退与腾起幅度。</summary>
    public CompiledConstructionKnockback Knockback { get; }

    /// <summary>实际接触后的双方地面换位资料。</summary>
    public CompiledConstructionExchange Exchange { get; }

    /// <summary>已经检查并独立复制的追加伤害。</summary>
    public IReadOnlyList<CompiledConstructionContactDamage> AdditionalDamage { get; }

    /// <summary>已经独立编译的有限随机接触候选。</summary>
    public CompiledConstructionRandomContact RandomContact { get; }

    /// <summary>已经独立编译的有限条件作用。</summary>
    public IReadOnlyList<CompiledConstructionConditionalContact> ConditionalContacts { get; }

    /// <summary>已检查并独立复制的球形范围作用。</summary>
    public IReadOnlyList<CompiledConstructionContactSpread> Spreads { get; }

    /// <summary>已复制的三向世界速度变化，不持有可编辑数组。</summary>
    public Vector3? Impulse { get; }

    /// <summary>已复制的真实接触传力规则。</summary>
    public CompiledForceRouting ForceRouting { get; }

    /// <summary>实际向上初速度，与水平推拉一起经过抗击退。</summary>
    public float Lift { get; }

    /// <summary>是否明确施加独立腾起。</summary>
    public bool Launch { get; }

    /// <summary>沿地面推拉的持续世界秒数，零表示沿用腾起轨迹。</summary>
    public float SlideSeconds { get; }

    /// <summary>受击停顿的持续世界秒数。</summary>
    public float StaggerSeconds { get; }

    /// <summary>束缚的持续世界秒数。</summary>
    public float BindSeconds { get; }

    /// <summary>实际攻击能力损耗的持续世界秒数。</summary>
    public float StrainSeconds { get; }

    /// <summary>实际护甲损耗的持续世界秒数。</summary>
    public float ExposureSeconds { get; }

    /// <summary>伤害、恢复、老化、物件损耗、移动或行动限制中至少一种实际人物作用。</summary>
    public bool HasActorEffect => HasAgingEffect || HasOtherActorEffect;

    /// <summary>明确推进年龄、原装备耐久或原库存寿命。</summary>
    public bool HasAgingEffect => InventoryAging != null || EquipmentErosion != null || AgeYears > 0;

    /// <summary>沿用原自主用途，不因新增损耗当前无效而屏蔽混合作用。</summary>
    public bool HasOtherActorEffect => Statuses.Count > 0 || HasActorEffectBesidesStatuses;

    /// <summary>状态以外的实际人物作用，用于辨别只凝神的构造。</summary>
    internal bool HasActorEffectBesidesStatuses => EquipmentRepair != null || HasActorEffectBesidesStatusesAndRepair;

    /// <summary>扣除状态和修复后的作用，用于排除混合用途。</summary>
    internal bool HasActorEffectBesidesStatusesAndRepair => WakanTransfer > 0f || Signal != null || ReleaseGrab != ConstructionGrabRelease.None || Restoration?.HasEffect == true || DamageScale > 0f || Force != 0f || Launch || Impulse?.sqrMagnitude > 0f || Knockback?.HasEffect == true || Exchange?.Chance > 0f || AdditionalDamage.Count > 0 || RandomContact?.HasEffect == true || ConditionalContacts.Any(rule => rule.HasEffect) || Spreads.Any(spread => spread.HasEffect) ||
                                  StaggerSeconds > 0f || BindSeconds > 0f || StrainSeconds > 0f || ExposureSeconds > 0f;

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
        if (source?.Statuses?.Length > 16) throw new InvalidDataException(path + "：每个作用面最多声明十六种属性状态");
        Statuses = source?.Statuses == null ? Array.Empty<CompiledConstructionStatus>() :
            Array.AsReadOnly(source.Statuses.Select((status, index) => new CompiledConstructionStatus(status,
                path + "/Statuses/" + index, source.DamageScale > 0f)).ToArray());
        HasStatusesAfterDamage = Statuses.Any(status => status.AfterDamage);
        SelectedTargetOnly = source?.SelectedTargetOnly == true;
        if (source?.FrostCoating != null)
            FrostCoating = new CompiledFrostCoating(source.FrostCoating, path + "/FrostCoating");
        if (source?.Restoration != null) Restoration = new CompiledConstructionRestoration(source.Restoration, path + "/Restoration");
        ReleaseGrab = source?.ReleaseGrab ?? ConstructionGrabRelease.None;
        if (!Enum.IsDefined(typeof(ConstructionGrabRelease), ReleaseGrab))
            throw new InvalidDataException(path + "/ReleaseGrab：需要有效的抓持解除方式");
        Targets = source?.Targets ?? SkillUseTargetRelation.Hostile;
        DamageScale = source?.DamageScale ?? 1f;
        WakanTransfer = source?.WakanTransfer ?? 0f;
        if (float.IsNaN(WakanTransfer) || WakanTransfer < 0f || WakanTransfer > 1000f)
            throw new InvalidDataException(path + "/WakanTransfer：单次灵气转移需在0到1000之间");
        WakanTransferMode = source?.WakanTransferMode ?? ConstructionWakanTransferMode.Draw;
        if (WakanTransferMode is not (ConstructionWakanTransferMode.Draw or ConstructionWakanTransferMode.Balance))
            throw new InvalidDataException(path + "/WakanTransferMode：未知灵气交付方式");
        AgeYears = source?.AgeYears ?? 0;
        if (AgeYears < 0 || AgeYears > 1000000)
            throw new InvalidDataException(path + "/AgeYears：每次老化需0至一百万整数年");
        if (source?.EquipmentErosion != null)
            EquipmentErosion = new CompiledEquipmentErosion(source.EquipmentErosion, path + "/EquipmentErosion");
        if (source?.EquipmentRepair != null)
            EquipmentRepair = new CompiledEquipmentRepair(source.EquipmentRepair, path + "/EquipmentRepair");
        if (source?.Signal != null)
            Signal = new CompiledConstructionSignal(source.Signal, path + "/Signal");
        if (source?.InventoryAging != null)
            InventoryAging = new CompiledInventoryAging(source.InventoryAging, path + "/InventoryAging");
        Element = source?.Element;
        if (Element.HasValue) CompiledConstructionContactDamage.ValidateElement(Element.Value, path + "/Element");
        DamageBasis = source?.DamageBasis ?? ConstructionDamageBasis.RemainingForce;
        if (!Enum.IsDefined(typeof(ConstructionDamageBasis), DamageBasis))
            throw new InvalidDataException(path + "/DamageBasis：伤害必须采用剩余或初始作用量");
        Force = source?.Force ?? 0f;
        if (source?.Knockback != null) Knockback = new CompiledConstructionKnockback(source.Knockback, path + "/Knockback");
        if (source?.Exchange != null) Exchange = new CompiledConstructionExchange(source.Exchange, path + "/Exchange");
        if (source?.AdditionalDamage?.Length > 8) throw new InvalidDataException(path + "：每个作用面最多八份追加伤害");
        AdditionalDamage = source?.AdditionalDamage == null ? Array.Empty<CompiledConstructionContactDamage>() :
            Array.AsReadOnly(source.AdditionalDamage.Select((damage, index) => new CompiledConstructionContactDamage(damage, path + "/AdditionalDamage/" + index)).Where(damage => damage.HasEffect).ToArray());
        if (source?.RandomContact != null) RandomContact = new CompiledConstructionRandomContact(source.RandomContact, path + "/RandomContact");
        if (source?.ConditionalContacts?.Length > 8) throw new InvalidDataException(path + "：每个作用面最多八条条件作用");
        ConditionalContacts = source?.ConditionalContacts == null ? Array.Empty<CompiledConstructionConditionalContact>() :
            Array.AsReadOnly(source.ConditionalContacts.Select((rule, index) => new CompiledConstructionConditionalContact(rule, path + "/ConditionalContacts/" + index)).ToArray());
        if (source?.Spreads?.Length > 8) throw new InvalidDataException(path + "：每个作用面最多八份范围分送");
        Spreads = source?.Spreads == null ? Array.Empty<CompiledConstructionContactSpread>() :
            Array.AsReadOnly(source.Spreads.Select((spread, index) => new CompiledConstructionContactSpread(spread, path + "/Spreads/" + index)).ToArray());
        if (source?.Impulse != null)
        {
            if (source.Impulse.Length != 3 || source.Impulse.Any(value =>
                    float.IsNaN(value) || float.IsInfinity(value) || Math.Abs(value) > 1000f))
                throw new InvalidDataException(path + "/Impulse：需要三个正负1000以内的有限速度，依次为前后、侧向和上下");
            Impulse = new Vector3(source.Impulse[0], source.Impulse[1], source.Impulse[2]);
        }

        Lift = source?.Lift ?? .5f;
        Launch = source?.Launch == true && Force == 0f;
        SlideSeconds = source?.SlideSeconds ?? 0f;
        StaggerSeconds = source?.StaggerSeconds ?? 0f;
        BindSeconds = source?.BindSeconds ?? 0f;
        StrainSeconds = source?.StrainSeconds ?? 0f;
        ExposureSeconds = source?.ExposureSeconds ?? 0f;
        Origin = source?.Origin ?? ConstructionForceOrigin.Owner;
        if (source?.ForceRouting != null)
        {
            if (!Impulse.HasValue || Impulse.Value.sqrMagnitude == 0f)
                throw new InvalidDataException(path + "/ForceRouting：传力需要明确且非零的三向发力");
            ForceRouting = new CompiledForceRouting(source.ForceRouting, path + "/ForceRouting");
        }

        if (Targets is not (SkillUseTargetRelation.Hostile or SkillUseTargetRelation.Friendly
                or SkillUseTargetRelation.Self) ||
            float.IsNaN(DamageScale) || float.IsInfinity(DamageScale) || DamageScale < 0f || DamageScale > 1000f ||
            float.IsNaN(Force) || float.IsInfinity(Force) || Math.Abs(Force) > 1000f ||
            float.IsNaN(Lift) || float.IsInfinity(Lift) || Lift < 0f || Lift > 100f ||
            !Seconds(SlideSeconds) || !Seconds(StaggerSeconds) || !Seconds(BindSeconds) || !Seconds(StrainSeconds) ||
            !Seconds(ExposureSeconds) ||
            Impulse.HasValue && (Force != 0f || Launch || SlideSeconds > 0f && Impulse.Value.z != 0f) ||
            SlideSeconds == 0f && (Force != 0f || Launch) && Lift == 0f || SlideSeconds > 0f && Launch ||
            !Enum.IsDefined(typeof(ConstructionForceOrigin), Origin))
            throw new InvalidDataException(path +
                                           "：接触作用需要有效人物关系、0至1000伤害倍率、正负1000以内推拉力、0至100离地力度及0至3600秒持续时间；腾起需要正离地力度，不能同时声明贴地和独立腾起");
    }

    /// <summary>持续时间只在编译资料的边界检查。</summary>
    /// <param name="seconds">资料中的世界秒数。</param>
    private static bool Seconds(float seconds) =>
        !float.IsNaN(seconds) && !float.IsInfinity(seconds) && seconds >= 0 && seconds <= 3600;
}
