using System;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Core.Semantics;

namespace Cultiway.Content.Semantics;

/// <summary>
/// 修仙内容注册的规范语义。法器、功法、金丹等系统共享这些资产，但 Core 不依赖它们。
/// </summary>
public sealed class CultivationSemantics : ExtendLibrary<SemanticAsset, CultivationSemantics>
{
    /// <summary>对目标、环境或规则产生的修仙效果。</summary>
    public static class Effect
    {
        /// <summary>表示吸收外部能量、物质或效果并纳为己用。</summary>
        public static SemanticAsset Absorption { get; internal set; }
        /// <summary>表示放大既有效果、属性或产出的能力。</summary>
        public static SemanticAsset Amplification { get; internal set; }
        /// <summary>表示削弱、穿透或破坏目标防御。</summary>
        public static SemanticAsset ArmorBreak { get; internal set; }
        /// <summary>表示束缚目标并限制其行动。</summary>
        public static SemanticAsset Binding { get; internal set; }
        /// <summary>表示隐藏实体、气息或行为，使其难以被察觉。</summary>
        public static SemanticAsset Concealment { get; internal set; }
        /// <summary>表示针对来袭效果进行反制。</summary>
        public static SemanticAsset Counter { get; internal set; }
        /// <summary>表示将承受或造成的伤害转换为其他结果。</summary>
        public static SemanticAsset DamageConversion { get; internal set; }
        /// <summary>表示吞噬目标并吸收其力量或资源。</summary>
        public static SemanticAsset Devouring { get; internal set; }
        /// <summary>表示移除已有法术、状态或场域效果。</summary>
        public static SemanticAsset Dispel { get; internal set; }
        /// <summary>表示持续抽取目标的生命、能量或其他资源。</summary>
        public static SemanticAsset Drain { get; internal set; }
        /// <summary>表示施加推、压、震等直接力学作用。</summary>
        public static SemanticAsset Force { get; internal set; }
        /// <summary>表示主动守护某个单位、区域或组织。</summary>
        public static SemanticAsset Guardian { get; internal set; }
        /// <summary>表示一次有效命中或依赖命中触发的作用。</summary>
        public static SemanticAsset Hit { get; internal set; }
        /// <summary>表示以冲撞、坠击或震荡产生强烈冲击。</summary>
        public static SemanticAsset Impact { get; internal set; }
        /// <summary>表示将目标困在特定空间或范围内。</summary>
        public static SemanticAsset Imprisonment { get; internal set; }
        /// <summary>表示提升或改变实体跨越空间的能力。</summary>
        public static SemanticAsset Mobility { get; internal set; }
        /// <summary>表示直接驱动实体移动或改变移动状态。</summary>
        public static SemanticAsset Movement { get; internal set; }
        /// <summary>表示同一效果能够连续或重复命中。</summary>
        public static SemanticAsset MultiHit { get; internal set; }
        /// <summary>表示作用本身不会引发受影响者的反击。</summary>
        public static SemanticAsset Nonretaliation { get; internal set; }
        /// <summary>表示感知、洞察或识别目标与环境信息。</summary>
        public static SemanticAsset Perception { get; internal set; }
        /// <summary>表示清除污染、邪祟或不利状态。</summary>
        public static SemanticAsset Purification { get; internal set; }
        /// <summary>表示恢复生命、能量、耐久或正常状态。</summary>
        public static SemanticAsset Recovery { get; internal set; }
        /// <summary>表示将来袭作用按原方向或规则反射回去。</summary>
        public static SemanticAsset Reflection { get; internal set; }
        /// <summary>表示释放储存、封存或积蓄的力量。</summary>
        public static SemanticAsset Release { get; internal set; }
        /// <summary>表示不同实体、力量或频率之间产生共鸣。</summary>
        public static SemanticAsset Resonance { get; internal set; }
        /// <summary>表示揭露被隐藏的实体、气息或信息。</summary>
        public static SemanticAsset Revealing { get; internal set; }
        /// <summary>表示依靠刚性和稳定结构进行防护。</summary>
        public static SemanticAsset RigidGuard { get; internal set; }
        /// <summary>表示封禁目标的力量、行动或通道。</summary>
        public static SemanticAsset Sealing { get; internal set; }
        /// <summary>表示生成可吸收或阻挡作用的护盾。</summary>
        public static SemanticAsset Shield { get; internal set; }
        /// <summary>表示施加、读取或改变目标状态。</summary>
        public static SemanticAsset Status { get; internal set; }
        /// <summary>表示在内部空间中收纳实体、物质或力量。</summary>
        public static SemanticAsset Storage { get; internal set; }
        /// <summary>表示召来、创造或具现可独立存在的实体。</summary>
        public static SemanticAsset Summon { get; internal set; }
        /// <summary>表示压制目标的行动、力量或规则效力。</summary>
        public static SemanticAsset Suppression { get; internal set; }
        /// <summary>表示改变实体、物质或力量的形态与性质。</summary>
        public static SemanticAsset Transformation { get; internal set; }
        /// <summary>表示建立持续阻隔或保护作用的防护边界。</summary>
        public static SemanticAsset Ward { get; internal set; }
    }

    /// <summary>炼制、生产及法器构造所提供的专门能力。</summary>
    public static class Craft
    {
        /// <summary>表示炼制丹药或辅助炼丹的能力。</summary>
        public static SemanticAsset Alchemy { get; internal set; }
        /// <summary>表示提纯、重塑或精炼材料与造物的能力。</summary>
        public static SemanticAsset Refinement { get; internal set; }
        /// <summary>表示高速飞行并贯穿目标的法器能力。</summary>
        public static SemanticAsset PiercingFlight { get; internal set; }
        /// <summary>表示储存和供给灵性力量的法器能力。</summary>
        public static SemanticAsset SpiritReservoir { get; internal set; }
        /// <summary>表示将自身作用投射为持续场域的法器能力。</summary>
        public static SemanticAsset FieldProjection { get; internal set; }
        /// <summary>表示持续守护宗门及其范围的法器能力。</summary>
        public static SemanticAsset SectGuardian { get; internal set; }
        /// <summary>表示孕育、承载或召出器灵的法器能力。</summary>
        public static SemanticAsset ArtifactSpirit { get; internal set; }
    }

    /// <summary>修仙对象或作用在空间和规则上的基本形态。</summary>
    public static class Form
    {
        /// <summary>表示由节点、阵纹或阵基共同构成的阵法形态。</summary>
        public static SemanticAsset Array { get; internal set; }
        /// <summary>表示具有锋刃并以斩击为主要用途的形态。</summary>
        public static SemanticAsset Blade { get; internal set; }
        /// <summary>表示以肉身、躯体或实体本体作为作用载体。</summary>
        public static SemanticAsset Body { get; internal set; }
        /// <summary>表示从起点向外展开的锥形或扇形范围。</summary>
        public static SemanticAsset Cone { get; internal set; }
        /// <summary>表示承载修炼知识与传承的功法书形态。</summary>
        public static SemanticAsset Cultibook { get; internal set; }
    }

    /// <summary>修仙作用从来源抵达生效位置的传递方式。</summary>
    public static class Delivery
    {
        /// <summary>表示将实体或效果部署到世界中的指定位置。</summary>
        public static SemanticAsset Deployment { get; internal set; }
        /// <summary>表示从本体向外投射映像、力量或作用。</summary>
        public static SemanticAsset Projection { get; internal set; }
    }

    /// <summary>修仙对象在活动体系中的主要用途。</summary>
    public static class Role
    {
        /// <summary>表示服务于修炼、突破或积累修为。</summary>
        public static SemanticAsset Cultivation { get; internal set; }
        /// <summary>表示承担载人、运输或高速移动用途。</summary>
        public static SemanticAsset Vehicle { get; internal set; }
    }

    /// <summary>用于描述修仙对象意象、力量来源或题材的语义。</summary>
    public static class Theme
    {
        /// <summary>表示与一种或多种元素力量相关。</summary>
        public static SemanticAsset Elemental { get; internal set; }
        /// <summary>表示与魂魄、神魂或灵魂作用相关。</summary>
        public static SemanticAsset Soul { get; internal set; }
        /// <summary>表示以声音、音律或震鸣为主要意象。</summary>
        public static SemanticAsset Sound { get; internal set; }
        /// <summary>表示与空间、距离、传送或空间结构相关。</summary>
        public static SemanticAsset Space { get; internal set; }
        /// <summary>表示与灵体、器灵或有灵存在相关。</summary>
        public static SemanticAsset Spirit { get; internal set; }
        /// <summary>表示与幻象、迷惑或虚实变化相关。</summary>
        public static SemanticAsset Illusion { get; internal set; }
        /// <summary>表示与龙族、龙形或龙威意象相关。</summary>
        public static SemanticAsset Dragon { get; internal set; }
    }

    /// <summary>材料或实体自身的物理与构造性质。</summary>
    public static class Material
    {
        /// <summary>表示材料容易破裂、碎裂或失稳。</summary>
        public static SemanticAsset Brittle { get; internal set; }
        /// <summary>表示材料或容器能够承载的规模与上限。</summary>
        public static SemanticAsset Capacity { get; internal set; }
        /// <summary>表示材料能够弯曲、延展或适应形变。</summary>
        public static SemanticAsset Flexibility { get; internal set; }
        /// <summary>表示材料抵抗压入、切割与形变的能力。</summary>
        public static SemanticAsset Hardness { get; internal set; }
        /// <summary>表示实体难以被推动、牵引或改变位置。</summary>
        public static SemanticAsset Immoveable { get; internal set; }
        /// <summary>表示材料或实体质量轻、惯性低。</summary>
        public static SemanticAsset Lightweight { get; internal set; }
        /// <summary>表示材料或构造维持既有状态的能力。</summary>
        public static SemanticAsset Stability { get; internal set; }
        /// <summary>表示材料或能量容易剧烈变化、爆发或失控。</summary>
        public static SemanticAsset Volatility { get; internal set; }
        /// <summary>表示参与构造的材料品质。</summary>
        public static SemanticAsset Quality { get; internal set; }
        /// <summary>表示参与构造的材料总量。</summary>
        public static SemanticAsset Quantity { get; internal set; }
        /// <summary>表示参与构造的材料来源和种类丰富程度。</summary>
        public static SemanticAsset SourceDiversity { get; internal set; }
    }

    /// <summary>可积累、消耗、传递或恢复的修仙资源。</summary>
    public static class Resource
    {
        /// <summary>表示可供能力消耗或调配的通用储备。</summary>
        public static SemanticAsset Reserve { get; internal set; }
        /// <summary>表示对象所含灵性、灵韵或灵智基础。</summary>
        public static SemanticAsset Spirituality { get; internal set; }
        /// <summary>表示生命活力、生机与肉身恢复基础。</summary>
        public static SemanticAsset Vitality { get; internal set; }
    }

    /// <summary>修仙世界中的组织归属与组织形态。</summary>
    public static class Organization
    {
        /// <summary>表示与宗门组织、成员或领地相关。</summary>
        public static SemanticAsset Sect { get; internal set; }
    }

    /// <summary>生物或对象具有的先天、稳定特征。</summary>
    public static class Trait
    {
        /// <summary>表示生物拥有决定元素亲和的灵根。</summary>
        public static SemanticAsset ElementRoot { get; internal set; }
    }

    /// <summary>修炼体系中具有明确层次含义的境界。</summary>
    public static class Realm
    {
        /// <summary>表示修士已经凝结金丹的境界或实体。</summary>
        public static SemanticAsset Jindan { get; internal set; }
        /// <summary>表示修士已经孕育元婴的境界或实体。</summary>
        public static SemanticAsset Yuanying { get; internal set; }
    }

    /// <summary>修炼方法、道路或长期发展倾向。</summary>
    public static class Path
    {
        /// <summary>表示以静修、感悟和稳定积累为核心的道路。</summary>
        public static SemanticAsset Meditation { get; internal set; }
        /// <summary>表示以战斗磨炼和实战成长为核心的道路。</summary>
        public static SemanticAsset BattleCultivation { get; internal set; }
        /// <summary>表示以杀戮、煞气或掠夺成长为核心的道路。</summary>
        public static SemanticAsset SlaughterCultivation { get; internal set; }
        /// <summary>表示以气运、机缘或命数为核心的道路。</summary>
        public static SemanticAsset FortuneCultivation { get; internal set; }
        /// <summary>表示以剑、剑意和剑术为核心的修炼道路。</summary>
        public static SemanticAsset Sword { get; internal set; }
    }

    protected override bool AutoRegisterAssets() => false;

    protected override void OnInit()
    {
        AddMaterialElementAliases();

        Effect.Absorption = New("effect.absorption", "effect", "absorption");
        Craft.Alchemy = New("craft.alchemy", "craft", "alchemy", "affordance.alchemy", "capability.alchemy_vessel");
        Effect.Amplification = New("effect.amplification", "effect", "amplification", "affordance.amplification");
        Effect.ArmorBreak = New("effect.armor_break", "effect", "armor_break", implications: [SkillSemantics.Effect.ArmorDown]);
        Form.Array = New("form.array", "form", "array");
        Effect.Binding = New("effect.binding", "effect", "binding", "affordance.binding");
        Form.Blade = New("form.blade", "form", "blade", "affordance.edge", implications: [SkillSemantics.Form.Slash]);
        Form.Body = New("form.body", "form", "body");
        Material.Brittle = New("material.brittle", "material", "brittle");
        Material.Capacity = New("material.capacity", "material", "capacity", "affordance.capacity");
        Effect.Concealment = New("effect.concealment", "effect", "concealment", "affordance.concealment");
        Role.Cultivation = New("role.cultivation", "role", "cultivation");
        Form.Cone = New("form.cone", "form", "cone", implications: [SkillSemantics.Form.Aoe]);
        Effect.Counter = New("effect.counter", "effect", "counter");
        Effect.DamageConversion = New("effect.damage_conversion", "effect", "damage_conversion");
        Delivery.Deployment = New("delivery.deployment", "delivery", "deployment");
        Effect.Devouring = New("effect.devouring", "effect", "devouring", "affordance.devouring",
            implications: [Effect.Absorption]);
        Effect.Dispel = New("effect.dispel", "effect", "dispel");
        Effect.Drain = New("effect.drain", "effect", "drain", implications: [Effect.Absorption]);
        Theme.Elemental = New("theme.elemental", "theme", "elemental");
        Material.Flexibility = New("material.flexibility", "material", "flexibility", "affordance.flexibility");
        Effect.Force = New("effect.force", "effect", "force");
        Effect.Ward = New("effect.ward", "effect", "ward", "affordance.ward");
        Effect.Guardian = New("effect.guardian", "effect", "guardian", "capability.guardian_ward",
            parents: [Effect.Ward]);
        Material.Hardness = New("material.hardness", "material", "hardness", "affordance.hardness");
        Effect.Hit = New("effect.hit", "effect", "hit");
        Effect.Impact = New("effect.impact", "effect", "impact", "affordance.impact");
        Material.Immoveable = New("material.immoveable", "material", "immovable");
        Effect.Imprisonment = New("effect.imprisonment", "effect", "imprisonment",
            parents: [SkillSemantics.Effect.Control]);
        Material.Lightweight = New("material.lightweight", "material", "lightweight");
        Effect.Mobility = New("effect.mobility", "effect", "mobility", "affordance.mobility");
        Effect.Movement = New("effect.movement", "effect", "movement", parents: [Effect.Mobility]);
        Effect.MultiHit = New("effect.multi_hit", "effect", "multi_hit", implications: [SkillSemantics.Effect.Combo]);
        Effect.Nonretaliation = New("effect.nonretaliation", "effect", "nonretaliation");
        Effect.Perception = New("effect.perception", "effect", "perception", "affordance.perception",
            "capability.insight");
        Delivery.Projection = New("delivery.projection", "delivery", "projection", "affordance.projection");
        Effect.Purification = New("effect.purification", "effect", "purification", "affordance.purification");
        Effect.Recovery = New("effect.recovery", "effect", "recovery", "capability.renewal");
        Craft.Refinement = New("craft.refinement", "craft", "refinement");
        Effect.Reflection = New("effect.reflection", "effect", "reflection", "affordance.reflection",
            parents: [Effect.Counter]);
        Effect.Release = New("effect.release", "effect", "release");
        Effect.Resonance = New("effect.resonance", "effect", "resonance", "affordance.resonance");
        Resource.Reserve = New("resource.reserve", "resource", "resource");
        Effect.Revealing = New("effect.revealing", "effect", "revealing", parents: [Effect.Perception]);
        Effect.RigidGuard = New("effect.rigid_guard", "effect", "rigid_guard", parents: [Effect.Ward]);
        Effect.Sealing = New("effect.sealing", "effect", "sealing", "affordance.sealing",
            parents: [SkillSemantics.Effect.Control]);
        Organization.Sect = New("organization.sect", "organization", "sect");
        Effect.Shield = New("effect.shield", "effect", "shield", parents: [Effect.Ward]);
        Theme.Soul = New("theme.soul", "theme", "soul", "affordance.soul");
        Theme.Sound = New("theme.sound", "theme", "sound", "affordance.sound");
        Theme.Space = New("theme.space", "theme", "space", "spatial", "affordance.space");
        Resource.Spirituality = New("resource.spirituality", "resource", "spirituality", "essence.spirituality");
        Theme.Spirit = New("theme.spirit", "theme", "spirit", implications: [Resource.Spirituality]);
        Material.Stability = New("material.stability", "material", "stability", "material.stability");
        Effect.Status = New("effect.status", "effect", "status");
        Effect.Storage = New("effect.storage", "effect", "storage", "affordance.storage",
            parents: [Material.Capacity]);
        Effect.Summon = New("effect.summon", "effect", "summon");
        Effect.Suppression = New("effect.suppression", "effect", "suppression", "affordance.suppression",
            parents: [SkillSemantics.Effect.Control]);
        Effect.Transformation = New("effect.transformation", "effect", "transformation",
            "affordance.transformation");
        Role.Vehicle = New("role.vehicle", "role", "vehicle", "capability.vehicle", implications: [Effect.Mobility]);
        Resource.Vitality = New("resource.vitality", "resource", "vitality", "essence.vitality");
        Material.Volatility = New("material.volatility", "material", "volatility", "affordance.volatility");

        Material.Quality = New("material.quality", "material", "quality", "material.quality");
        Material.Quantity = New("material.quantity", "material", "quantity", "material.quantity");
        Material.SourceDiversity = New("material.source_diversity", "material", "source_diversity",
            "material.source_diversity");
        Craft.PiercingFlight = New("capability.piercing_flight", "craft", "piercing_flight", "capability.piercing_flight",
            implications: [SkillSemantics.Form.Pierce, SkillSemantics.Delivery.Projectile]);
        Craft.SpiritReservoir = New("capability.spirit_reservoir", "craft", "spirit_reservoir",
            "capability.spirit_reservoir", implications: [Resource.Reserve, Resource.Spirituality]);
        Craft.FieldProjection = New("capability.field_projection", "craft", "field_projection",
            "capability.field_projection", implications: [Delivery.Projection, SkillSemantics.Delivery.Field]);
        Craft.SectGuardian = New("capability.sect_guardian", "craft", "sect_guardian", "capability.sect_guardian",
            implications: [Organization.Sect, Effect.Guardian]);
        Craft.ArtifactSpirit = New("capability.artifact_spirit", "craft", "artifact_spirit",
            "capability.artifact_spirit", implications: [Theme.Spirit, Effect.Summon]);

        Trait.ElementRoot = New("trait.element_root", "trait", "element_root", implications: [Theme.Elemental]);
        Form.Cultibook = New("form.cultibook", "form", "cultibook", implications: [Role.Cultivation]);
        Realm.Jindan = New("realm.jindan", "realm", "jindan", implications: [Role.Cultivation]);
        Realm.Yuanying = New("realm.yuanying", "realm", "yuanying", implications: [Role.Cultivation]);
        Path.Meditation = New("path.meditation", "path", "meditation_path", implications: [Role.Cultivation]);
        Path.BattleCultivation = New("path.battle_cultivation", "path", "battle_cultivation_path",
            implications: [Role.Cultivation]);
        Path.SlaughterCultivation = New("path.slaughter_cultivation", "path", "slaughter_cultivation_path",
            implications: [Role.Cultivation]);
        Path.FortuneCultivation = New("path.fortune_cultivation", "path", "fortune_cultivation_path",
            implications: [Role.Cultivation]);
        Path.Sword = New("path.sword", "path", "sword_path", implications: [Form.Blade]);
        Theme.Illusion = New("theme.illusion", "theme", "illusion");
        Theme.Dragon = New("theme.dragon", "theme", "dragon");
    }

    private SemanticAsset New(
        string suffix,
        string facet,
        string displayKey,
        string alias1 = null,
        string alias2 = null,
        SemanticAsset[] parents = null,
        SemanticAsset[] implications = null)
    {
        var aliases = new[] { displayKey, alias1, alias2 }
            .Where(x => !string.IsNullOrEmpty(x))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return Add(new SemanticAsset
        {
            id = $"semantic.{suffix}",
            facet_id = facet,
            name_key = $"Cultiway.Semantic.{displayKey}",
            aliases = aliases,
            parent_ids = parents?.Select(x => x.id).ToArray() ?? System.Array.Empty<string>(),
            implications = implications?.Select(x => new SemanticImplication(x.id, 0.75f)).ToArray()
                           ?? System.Array.Empty<SemanticImplication>()
        });
    }

    private static void AddMaterialElementAliases()
    {
        AddAliases(SkillSemantics.Element.Iron, "element.iron");
        AddAliases(SkillSemantics.Element.Wood, "element.wood");
        AddAliases(SkillSemantics.Element.Water, "element.water");
        AddAliases(SkillSemantics.Element.Fire, "element.fire");
        AddAliases(SkillSemantics.Element.Earth, "element.earth");
        AddAliases(SkillSemantics.Element.Neg, "element.neg");
        AddAliases(SkillSemantics.Element.Pos, "element.pos");
        AddAliases(SkillSemantics.Element.Entropy, "element.entropy");
        AddAliases(SkillSemantics.Form.Sustain, "affordance.sustain");
    }

    private static void AddAliases(SemanticAsset semantic, params string[] aliases)
    {
        semantic.aliases = semantic.aliases.Concat(aliases).Distinct(StringComparer.Ordinal).ToArray();
    }
}
