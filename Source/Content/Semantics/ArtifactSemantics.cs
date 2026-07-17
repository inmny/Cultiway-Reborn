using Cultiway.Core.Semantics;

namespace Cultiway.Content.Semantics;

/// <summary>
/// 法器定义使用的语义入口。共享概念直接返回 Core 资产，修仙概念返回 Content 扩展资产。
/// </summary>
public static class ArtifactSemantics
{
    /// <summary>法器使用的元素属性。</summary>
    public static class Element
    {
        /// <summary>表示法器能力具有火焰或灼热属性。</summary>
        public static SemanticAsset Fire => SkillSemantics.Element.Fire;
        /// <summary>表示法器能力具有风或气流属性。</summary>
        public static SemanticAsset Wind => SkillSemantics.Element.Wind;
    }

    /// <summary>法器能力的空间与规则形态。</summary>
    public static class Form
    {
        /// <summary>表示法器通过阵纹、阵位或多个节点共同作用。</summary>
        public static SemanticAsset Array => CultivationSemantics.Form.Array;
        /// <summary>表示法器具有锋刃或斩切形态。</summary>
        public static SemanticAsset Blade => CultivationSemantics.Form.Blade;
        /// <summary>表示法器能力覆盖锥形或扇形范围。</summary>
        public static SemanticAsset Cone => CultivationSemantics.Form.Cone;
        /// <summary>表示法器能力以法术结构释放。</summary>
        public static SemanticAsset Spell => SkillSemantics.Form.Spell;
        /// <summary>表示法器能力需要持续维持。</summary>
        public static SemanticAsset Sustain => SkillSemantics.Form.Sustain;
    }

    /// <summary>法器能力抵达作用位置的传递方式。</summary>
    public static class Delivery
    {
        /// <summary>表示将法器或其作用部署到世界中的指定位置。</summary>
        public static SemanticAsset Deployment => CultivationSemantics.Delivery.Deployment;
        /// <summary>表示法器通过持续存在的场域传递作用。</summary>
        public static SemanticAsset Field => SkillSemantics.Delivery.Field;
        /// <summary>表示法器从本体向外投射映像或力量。</summary>
        public static SemanticAsset Projection => CultivationSemantics.Delivery.Projection;
    }

    /// <summary>法器本体或投影的运动模式。</summary>
    public static class Motion
    {
        /// <summary>表示法器或投影围绕主体持续环绕。</summary>
        public static SemanticAsset Orbit => SkillSemantics.Motion.Orbit;
    }

    /// <summary>法器对目标、环境或规则产生的结果。</summary>
    public static class Effect
    {
        /// <summary>表示法器吸收外部能量、物质或效果。</summary>
        public static SemanticAsset Absorption => CultivationSemantics.Effect.Absorption;
        /// <summary>表示法器放大既有效果、属性或产出。</summary>
        public static SemanticAsset Amplification => CultivationSemantics.Effect.Amplification;
        /// <summary>表示法器削弱、穿透或破坏目标防御。</summary>
        public static SemanticAsset ArmorBreak => CultivationSemantics.Effect.ArmorBreak;
        /// <summary>表示法器隐藏自身、持有者或相关气息。</summary>
        public static SemanticAsset Concealment => CultivationSemantics.Effect.Concealment;
        /// <summary>表示法器限制目标行动或能力。</summary>
        public static SemanticAsset Control => SkillSemantics.Effect.Control;
        /// <summary>表示法器针对来袭作用进行反制。</summary>
        public static SemanticAsset Counter => CultivationSemantics.Effect.Counter;
        /// <summary>表示法器将伤害转换为其他结果。</summary>
        public static SemanticAsset DamageConversion => CultivationSemantics.Effect.DamageConversion;
        /// <summary>表示法器向目标施加不利状态。</summary>
        public static SemanticAsset Debuff => SkillSemantics.Effect.Debuff;
        /// <summary>表示法器吞噬目标并取得其力量或资源。</summary>
        public static SemanticAsset Devouring => CultivationSemantics.Effect.Devouring;
        /// <summary>表示法器移除已有法术、状态或场域。</summary>
        public static SemanticAsset Dispel => CultivationSemantics.Effect.Dispel;
        /// <summary>表示法器持续抽取目标资源。</summary>
        public static SemanticAsset Drain => CultivationSemantics.Effect.Drain;
        /// <summary>表示法器施加推、压、震等直接力量。</summary>
        public static SemanticAsset Force => CultivationSemantics.Effect.Force;
        /// <summary>表示法器促进生命、实体或器灵成长。</summary>
        public static SemanticAsset Growth => SkillSemantics.Effect.Growth;
        /// <summary>表示法器主动守护单位、区域或组织。</summary>
        public static SemanticAsset Guardian => CultivationSemantics.Effect.Guardian;
        /// <summary>表示法器作用依赖或产生有效命中。</summary>
        public static SemanticAsset Hit => CultivationSemantics.Effect.Hit;
        /// <summary>表示法器以冲撞、坠击或震荡产生冲击。</summary>
        public static SemanticAsset Impact => CultivationSemantics.Effect.Impact;
        /// <summary>表示法器将目标困在特定空间或范围。</summary>
        public static SemanticAsset Imprisonment => CultivationSemantics.Effect.Imprisonment;
        /// <summary>表示法器提升或改变跨越空间的能力。</summary>
        public static SemanticAsset Mobility => CultivationSemantics.Effect.Mobility;
        /// <summary>表示法器直接驱动自身、持有者或目标移动。</summary>
        public static SemanticAsset Movement => CultivationSemantics.Effect.Movement;
        /// <summary>表示法器能够连续或重复命中。</summary>
        public static SemanticAsset MultiHit => CultivationSemantics.Effect.MultiHit;
        /// <summary>表示法器能够感知、洞察或识别目标。</summary>
        public static SemanticAsset Perception => CultivationSemantics.Effect.Perception;
        /// <summary>表示法器清除污染、邪祟或不利状态。</summary>
        public static SemanticAsset Purification => CultivationSemantics.Effect.Purification;
        /// <summary>表示法器恢复生命、能量、耐久或状态。</summary>
        public static SemanticAsset Recovery => CultivationSemantics.Effect.Recovery;
        /// <summary>表示法器将来袭作用反射回去。</summary>
        public static SemanticAsset Reflection => CultivationSemantics.Effect.Reflection;
        /// <summary>表示法器释放储存、封存或积蓄的力量。</summary>
        public static SemanticAsset Release => CultivationSemantics.Effect.Release;
        /// <summary>表示法器与其他实体或力量产生共鸣。</summary>
        public static SemanticAsset Resonance => CultivationSemantics.Effect.Resonance;
        /// <summary>表示法器封禁目标的力量、行动或通道。</summary>
        public static SemanticAsset Sealing => CultivationSemantics.Effect.Sealing;
        /// <summary>表示法器生成可吸收或阻挡作用的护盾。</summary>
        public static SemanticAsset Shield => CultivationSemantics.Effect.Shield;
        /// <summary>表示法器禁止目标施法或使用相关能力。</summary>
        public static SemanticAsset Silence => SkillSemantics.Effect.Silence;
        /// <summary>表示法器施加、读取或改变目标状态。</summary>
        public static SemanticAsset Status => CultivationSemantics.Effect.Status;
        /// <summary>表示法器在内部空间收纳实体、物质或力量。</summary>
        public static SemanticAsset Storage => CultivationSemantics.Effect.Storage;
        /// <summary>表示法器召来、创造或具现独立实体。</summary>
        public static SemanticAsset Summon => CultivationSemantics.Effect.Summon;
        /// <summary>表示法器压制目标的行动、力量或规则效力。</summary>
        public static SemanticAsset Suppression => CultivationSemantics.Effect.Suppression;
        /// <summary>表示法器改变实体、物质或力量的形态与性质。</summary>
        public static SemanticAsset Transformation => CultivationSemantics.Effect.Transformation;
        /// <summary>表示法器建立持续阻隔或保护作用的边界。</summary>
        public static SemanticAsset Ward => CultivationSemantics.Effect.Ward;
    }

    /// <summary>法器具备的炼制与生产能力。</summary>
    public static class Craft
    {
        /// <summary>表示法器能够炼制丹药或辅助炼丹。</summary>
        public static SemanticAsset Alchemy => CultivationSemantics.Craft.Alchemy;
        /// <summary>表示法器能够提纯、重塑或精炼材料与造物。</summary>
        public static SemanticAsset Refinement => CultivationSemantics.Craft.Refinement;
    }

    /// <summary>法器在战斗、辅助或生产中的主要用途。</summary>
    public static class Role
    {
        /// <summary>表示法器主要用于伤害敌人和取得战斗优势。</summary>
        public static SemanticAsset Offensive => SkillSemantics.Role.Offensive;
        /// <summary>表示法器主要用于防护自身、友方或区域。</summary>
        public static SemanticAsset Defensive => SkillSemantics.Role.Defensive;
        /// <summary>表示法器主要用于强化、恢复或协助其他单位。</summary>
        public static SemanticAsset Support => SkillSemantics.Role.Support;
        /// <summary>表示法器主要用于制造、炼制或资源产出。</summary>
        public static SemanticAsset Production => SkillSemantics.Role.Production;
        /// <summary>表示法器用于载人、运输或高速移动。</summary>
        public static SemanticAsset Vehicle => CultivationSemantics.Role.Vehicle;
    }

    /// <summary>法器的意象、力量来源或题材。</summary>
    public static class Theme
    {
        /// <summary>表示法器与一种或多种元素力量相关。</summary>
        public static SemanticAsset Elemental => CultivationSemantics.Theme.Elemental;
        /// <summary>表示法器作用于魂魄、神魂或灵魂。</summary>
        public static SemanticAsset Soul => CultivationSemantics.Theme.Soul;
        /// <summary>表示法器以声音、音律或震鸣为主要意象。</summary>
        public static SemanticAsset Sound => CultivationSemantics.Theme.Sound;
        /// <summary>表示法器与空间、距离或传送相关。</summary>
        public static SemanticAsset Space => CultivationSemantics.Theme.Space;
        /// <summary>表示法器与灵体、器灵或有灵存在相关。</summary>
        public static SemanticAsset Spirit => CultivationSemantics.Theme.Spirit;
    }

    /// <summary>法器材料或构造具有的性质。</summary>
    public static class Material
    {
        /// <summary>表示法器材料抵抗压入、切割与形变的能力。</summary>
        public static SemanticAsset Hardness => CultivationSemantics.Material.Hardness;
        /// <summary>表示法器材料或构造维持既有状态的能力。</summary>
        public static SemanticAsset Stability => CultivationSemantics.Material.Stability;
        /// <summary>表示法器材料或能量容易剧烈变化、爆发或失控。</summary>
        public static SemanticAsset Volatility => CultivationSemantics.Material.Volatility;
    }

    /// <summary>法器能够储存或调配的资源。</summary>
    public static class Resource
    {
        /// <summary>表示法器可供能力消耗或调配的通用储备。</summary>
        public static SemanticAsset Reserve => CultivationSemantics.Resource.Reserve;
        /// <summary>表示法器所含灵性、灵韵或灵智基础。</summary>
        public static SemanticAsset Spirituality => CultivationSemantics.Resource.Spirituality;
    }

    /// <summary>法器所服务或归属的组织。</summary>
    public static class Organization
    {
        /// <summary>表示法器与宗门组织、成员或领地相关。</summary>
        public static SemanticAsset Sect => CultivationSemantics.Organization.Sect;
    }
}
