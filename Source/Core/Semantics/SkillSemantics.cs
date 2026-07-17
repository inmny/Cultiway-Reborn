using System;

namespace Cultiway.Core.Semantics;

/// <summary>
/// Core 技能系统公开的规范语义。Content 可以直接复用这些资产，而不再复制字符串标签。
/// </summary>
public static class SkillSemantics
{
    /// <summary>技能涉及的元素或能量属性。</summary>
    public static class Element
    {
        /// <summary>表示铁或金属性质的元素语义。</summary>
        public static SemanticAsset Iron { get; internal set; }
        /// <summary>表示木与生长性质的元素语义。</summary>
        public static SemanticAsset Wood { get; internal set; }
        /// <summary>表示水性质的元素语义。</summary>
        public static SemanticAsset Water { get; internal set; }
        /// <summary>表示冰寒性质的元素语义。</summary>
        public static SemanticAsset Ice { get; internal set; }
        /// <summary>表示毒性力量的元素语义。</summary>
        public static SemanticAsset Poison { get; internal set; }
        /// <summary>表示火焰与灼热性质的元素语义。</summary>
        public static SemanticAsset Fire { get; internal set; }
        /// <summary>表示土石性质的元素语义。</summary>
        public static SemanticAsset Earth { get; internal set; }
        /// <summary>表示阴性或负向力量的元素语义。</summary>
        public static SemanticAsset Neg { get; internal set; }
        /// <summary>表示阳性或正向力量的元素语义。</summary>
        public static SemanticAsset Pos { get; internal set; }
        /// <summary>表示熵增、混乱或衰败性质的元素语义。</summary>
        public static SemanticAsset Entropy { get; internal set; }
        /// <summary>表示风与气流性质的元素语义。</summary>
        public static SemanticAsset Wind { get; internal set; }
        /// <summary>表示雷电性质的元素语义。</summary>
        public static SemanticAsset Lightning { get; internal set; }
        /// <summary>表示不归属于特定元素的通用力量。</summary>
        public static SemanticAsset Generic { get; internal set; }
    }

    /// <summary>技能效果在空间和规则上的基本形态。</summary>
    public static class Form
    {
        /// <summary>表示以斩切为主要作用形式。</summary>
        public static SemanticAsset Slash { get; internal set; }
        /// <summary>表示以贯穿或穿刺为主要作用形式。</summary>
        public static SemanticAsset Pierce { get; internal set; }
        /// <summary>表示球状或弹丸状的作用形式。</summary>
        public static SemanticAsset Ball { get; internal set; }
        /// <summary>表示同时覆盖一片区域的作用形式。</summary>
        public static SemanticAsset Aoe { get; internal set; }
        /// <summary>表示自上而下坠落生效的作用形式。</summary>
        public static SemanticAsset Falling { get; internal set; }
        /// <summary>表示效果需要持续维持而非瞬时结束。</summary>
        public static SemanticAsset Sustain { get; internal set; }
        /// <summary>表示以法术结构组织和释放的形式。</summary>
        public static SemanticAsset Spell { get; internal set; }
        /// <summary>表示主要作用于单个目标的形式。</summary>
        public static SemanticAsset Single { get; internal set; }
    }

    /// <summary>技能从施术者抵达作用位置的传递方式。</summary>
    public static class Delivery
    {
        /// <summary>表示通过可飞行的投射物传递效果。</summary>
        public static SemanticAsset Projectile { get; internal set; }
        /// <summary>表示通过近身接触或挥击传递效果。</summary>
        public static SemanticAsset Melee { get; internal set; }
        /// <summary>表示无需飞行过程便在目标处立即生效。</summary>
        public static SemanticAsset Instant { get; internal set; }
        /// <summary>表示通过持续存在的区域或场传递效果。</summary>
        public static SemanticAsset Field { get; internal set; }
    }

    /// <summary>技能实体或投射物在世界中的运动模式。</summary>
    public static class Motion
    {
        /// <summary>表示沿确定方向直线运动。</summary>
        public static SemanticAsset Direct { get; internal set; }
        /// <summary>表示持续修正方向以追踪目标。</summary>
        public static SemanticAsset Homing { get; internal set; }
        /// <summary>表示从高处向目标位置下落。</summary>
        public static SemanticAsset Falling { get; internal set; }
        /// <summary>表示贴近地面或沿地表运动。</summary>
        public static SemanticAsset Ground { get; internal set; }
        /// <summary>表示瞬间跃迁到目标位置或方向。</summary>
        public static SemanticAsset Snap { get; internal set; }
        /// <summary>表示围绕中心旋卷的涡流运动。</summary>
        public static SemanticAsset Vortex { get; internal set; }
        /// <summary>表示多个实体如雨点般连续降下。</summary>
        public static SemanticAsset Rain { get; internal set; }
        /// <summary>表示完成外出阶段后折返来源位置。</summary>
        public static SemanticAsset Return { get; internal set; }
        /// <summary>表示以折线或锯齿路径前进。</summary>
        public static SemanticAsset Zigzag { get; internal set; }
        /// <summary>表示以周期性波动路径前进。</summary>
        public static SemanticAsset Wave { get; internal set; }
        /// <summary>表示沿螺旋轨迹推进。</summary>
        public static SemanticAsset Spiral { get; internal set; }
        /// <summary>表示围绕某个主体或中心持续环绕。</summary>
        public static SemanticAsset Orbit { get; internal set; }
        /// <summary>表示直接在指定位置显现。</summary>
        public static SemanticAsset Appear { get; internal set; }
        /// <summary>表示围绕施术者完成近战扇扫。</summary>
        public static SemanticAsset MeleeSweep { get; internal set; }
    }

    /// <summary>技能对目标、环境或规则产生的结果。</summary>
    public static class Effect
    {
        /// <summary>表示限制目标行动或能力的控制效果。</summary>
        public static SemanticAsset Control { get; internal set; }
        /// <summary>表示降低目标移动或行动速度。</summary>
        public static SemanticAsset Slow { get; internal set; }
        /// <summary>表示在一段时间内持续结算伤害。</summary>
        public static SemanticAsset DamageOverTime { get; internal set; }
        /// <summary>表示以火焰灼烧造成持续影响。</summary>
        public static SemanticAsset Burn { get; internal set; }
        /// <summary>表示以冰寒冻结并限制目标。</summary>
        public static SemanticAsset Freeze { get; internal set; }
        /// <summary>表示以爆炸向周围释放作用。</summary>
        public static SemanticAsset Blast { get; internal set; }
        /// <summary>表示改变移动、攻击或行动速度。</summary>
        public static SemanticAsset Speed { get; internal set; }
        /// <summary>表示促进生命、实体或效果成长。</summary>
        public static SemanticAsset Growth { get; internal set; }
        /// <summary>表示直接提升力量或效果强度。</summary>
        public static SemanticAsset Power { get; internal set; }
        /// <summary>表示直接造成伤害。</summary>
        public static SemanticAsset Damage { get; internal set; }
        /// <summary>表示强制改变目标所处位置。</summary>
        public static SemanticAsset Displace { get; internal set; }
        /// <summary>表示在短时间内集中爆发效果。</summary>
        public static SemanticAsset Burst { get; internal set; }
        /// <summary>表示改变实体、范围或表现尺寸。</summary>
        public static SemanticAsset Size { get; internal set; }
        /// <summary>表示向目标施加不利状态。</summary>
        public static SemanticAsset Debuff { get; internal set; }
        /// <summary>表示降低目标的攻击能力。</summary>
        public static SemanticAsset AttackDown { get; internal set; }
        /// <summary>表示降低目标的防御或护甲能力。</summary>
        public static SemanticAsset ArmorDown { get; internal set; }
        /// <summary>表示将目标牵引向指定位置。</summary>
        public static SemanticAsset Pull { get; internal set; }
        /// <summary>表示使目标短时间无法行动。</summary>
        public static SemanticAsset Stun { get; internal set; }
        /// <summary>表示不属于常规效果分类的特殊规则。</summary>
        public static SemanticAsset Special { get; internal set; }
        /// <summary>表示结果或选择带有随机性。</summary>
        public static SemanticAsset Random { get; internal set; }
        /// <summary>表示交换两个实体、位置或状态。</summary>
        public static SemanticAsset Swap { get; internal set; }
        /// <summary>表示连续命中、连段或组合触发。</summary>
        public static SemanticAsset Combo { get; internal set; }
        /// <summary>表示禁止目标施法或使用相关能力。</summary>
        public static SemanticAsset Silence { get; internal set; }
        /// <summary>表示满足条件时直接处决目标。</summary>
        public static SemanticAsset Execute { get; internal set; }
        /// <summary>表示施加诅咒类持续负面影响。</summary>
        public static SemanticAsset Curse { get; internal set; }
        /// <summary>表示修改或利用技能的运动轨迹。</summary>
        public static SemanticAsset Trajectory { get; internal set; }
        /// <summary>表示在运行过程中切换运动模式。</summary>
        public static SemanticAsset MotionChange { get; internal set; }
        /// <summary>表示按批次或齐射方式释放多个实体。</summary>
        public static SemanticAsset Salvo { get; internal set; }
    }

    /// <summary>技能在战斗、辅助或生产中的主要用途。</summary>
    public static class Role
    {
        /// <summary>表示以伤害敌人和取得战斗优势为主。</summary>
        public static SemanticAsset Offensive { get; internal set; }
        /// <summary>表示以防护自身、友方或区域为主。</summary>
        public static SemanticAsset Defensive { get; internal set; }
        /// <summary>表示以强化、恢复或协助其他单位为主。</summary>
        public static SemanticAsset Support { get; internal set; }
        /// <summary>表示以制造、炼制或资源产出为主。</summary>
        public static SemanticAsset Production { get; internal set; }
    }

    /// <summary>不改变基础机制、但用于描述技能意象与题材的语义。</summary>
    public static class Theme
    {
        /// <summary>表示金属、锋锐或兵刃相关的主题。</summary>
        public static SemanticAsset Metal { get; internal set; }
    }

    internal static void Register(SemanticLibrary library)
    {
        Element.Iron = Add(library, "semantic.element.iron", "element", "iron");
        Element.Wood = Add(library, "semantic.element.wood", "element", "wood");
        Element.Water = Add(library, "semantic.element.water", "element", "water");
        Element.Ice = Add(library, "semantic.element.ice", "element", "ice");
        Element.Poison = Add(library, "semantic.element.poison", "element", "poison");
        Element.Fire = Add(library, "semantic.element.fire", "element", "fire");
        Element.Earth = Add(library, "semantic.element.earth", "element", "earth");
        Element.Neg = Add(library, "semantic.element.neg", "element", "neg");
        Element.Pos = Add(library, "semantic.element.pos", "element", "pos");
        Element.Entropy = Add(library, "semantic.element.entropy", "element", "entropy");
        Element.Wind = Add(library, "semantic.element.wind", "element", "wind");
        Element.Lightning = Add(library, "semantic.element.lightning", "element", "lightning");
        Element.Generic = Add(library, "semantic.element.generic", "element", "generic");

        Form.Slash = Add(library, "semantic.form.slash", "form", "slash");
        Form.Pierce = Add(library, "semantic.form.pierce", "form", "pierce");
        Form.Ball = Add(library, "semantic.form.ball", "form", "ball");
        Form.Aoe = Add(library, "semantic.form.aoe", "form", "aoe");
        Form.Falling = Add(library, "semantic.form.falling", "form", "falling");
        Form.Sustain = Add(library, "semantic.form.sustain", "form", "sustain");
        Form.Spell = Add(library, "semantic.form.spell", "form", "spell");
        Form.Single = Add(library, "semantic.form.single", "form", "single");

        Delivery.Projectile = Add(library, "semantic.delivery.projectile", "delivery",
            "delivery_projectile", "projectile");
        Delivery.Melee = Add(library, "semantic.delivery.melee", "delivery", "delivery_melee");
        Delivery.Instant = Add(library, "semantic.delivery.instant", "delivery", "delivery_instant");
        Delivery.Field = Add(library, "semantic.delivery.field", "delivery", "delivery_field", "field");

        Motion.Direct = Add(library, "semantic.motion.direct", "motion", "direct");
        Motion.Homing = Add(library, "semantic.motion.homing", "motion", "homing");
        Motion.Falling = Add(library, "semantic.motion.falling", "motion", "motion_falling");
        Motion.Ground = Add(library, "semantic.motion.ground", "motion", "ground");
        Motion.Snap = Add(library, "semantic.motion.snap", "motion", "snap");
        Motion.Vortex = Add(library, "semantic.motion.vortex", "motion", "vortex");
        Motion.Rain = Add(library, "semantic.motion.rain", "motion", "rain");
        Motion.Return = Add(library, "semantic.motion.return", "motion", "return");
        Motion.Zigzag = Add(library, "semantic.motion.zigzag", "motion", "zigzag");
        Motion.Wave = Add(library, "semantic.motion.wave", "motion", "wave");
        Motion.Spiral = Add(library, "semantic.motion.spiral", "motion", "spiral");
        Motion.Orbit = Add(library, "semantic.motion.orbit", "motion", "orbit");
        Motion.Appear = Add(library, "semantic.motion.appear", "motion", "appear");
        Motion.MeleeSweep = Add(library, "semantic.motion.melee_sweep", "motion", "melee_sweep");

        Effect.Control = Add(library, "semantic.effect.control", "effect", "control");
        Effect.Slow = Add(library, "semantic.effect.slow", "effect", "slow",
            parents: Ids(Effect.Control));
        Effect.DamageOverTime = Add(library, "semantic.effect.damage_over_time", "effect", "dot");
        Effect.Burn = Add(library, "semantic.effect.burn", "effect", "burn",
            parents: Ids(Effect.DamageOverTime), implications: Implies(Element.Fire, 0.8f));
        Effect.Freeze = Add(library, "semantic.effect.freeze", "effect", "freeze",
            parents: Ids(Effect.Control), implications: Implies(Element.Ice, 0.8f));
        Effect.Blast = Add(library, "semantic.effect.blast", "effect", "blast",
            implications: Implies(Form.Aoe, 0.8f));
        Effect.Speed = Add(library, "semantic.effect.speed", "effect", "speed");
        Effect.Growth = Add(library, "semantic.effect.growth", "effect", "growth");
        Effect.Power = Add(library, "semantic.effect.power", "effect", "power");
        Effect.Damage = Add(library, "semantic.effect.damage", "effect", "damage");
        Effect.Displace = Add(library, "semantic.effect.displace", "effect", "displace",
            parents: Ids(Effect.Control));
        Effect.Burst = Add(library, "semantic.effect.burst", "effect", "burst", "BurstCount");
        Effect.Size = Add(library, "semantic.effect.size", "effect", "size");
        Effect.Debuff = Add(library, "semantic.effect.debuff", "effect", "debuff");
        Effect.AttackDown = Add(library, "semantic.effect.attack_down", "effect", "attack_down",
            parents: Ids(Effect.Debuff));
        Effect.ArmorDown = Add(library, "semantic.effect.armor_down", "effect", "armor_down",
            parents: Ids(Effect.Debuff));
        Effect.Pull = Add(library, "semantic.effect.pull", "effect", "pull",
            parents: Ids(Effect.Displace));
        Effect.Stun = Add(library, "semantic.effect.stun", "effect", "stun",
            parents: Ids(Effect.Control));
        Effect.Special = Add(library, "semantic.effect.special", "effect", "special");
        Effect.Random = Add(library, "semantic.effect.random", "effect", "random");
        Effect.Swap = Add(library, "semantic.effect.swap", "effect", "swap",
            parents: Ids(Effect.Displace));
        Effect.Combo = Add(library, "semantic.effect.combo", "effect", "combo");
        Effect.Silence = Add(library, "semantic.effect.silence", "effect", "silence",
            parents: Ids(Effect.Control));
        Effect.Execute = Add(library, "semantic.effect.execute", "effect", "execute",
            implications: Implies(Effect.Damage, 0.8f));
        Effect.Curse = Add(library, "semantic.effect.curse", "effect", "curse",
            parents: Ids(Effect.Debuff));
        Effect.Trajectory = Add(library, "semantic.effect.trajectory", "effect", "trajectory");
        Effect.MotionChange = Add(library, "semantic.effect.motion_change", "effect", "motion");
        Effect.Salvo = Add(library, "semantic.effect.salvo", "effect", "salvo", "SalvoCount");

        Role.Offensive = Add(library, "semantic.role.offensive", "role", "offensive");
        Role.Defensive = Add(library, "semantic.role.defensive", "role", "defensive");
        Role.Support = Add(library, "semantic.role.support", "role", "support");
        Role.Production = Add(library, "semantic.role.production", "role", "production");
        Theme.Metal = Add(library, "semantic.theme.metal", "theme", "metal",
            implications: Implies(Element.Iron, 0.6f));
    }

    private static SemanticAsset Add(
        SemanticLibrary library,
        string id,
        string facet,
        string displayKey,
        string alias = null,
        string[] parents = null,
        SemanticImplication[] implications = null)
    {
        var aliases = alias == null ? new[] { displayKey } : new[] { displayKey, alias };
        return library.add(new SemanticAsset
        {
            id = id,
            facet_id = facet,
            name_key = $"Cultiway.SkillTag.{displayKey}",
            aliases = aliases,
            parent_ids = parents ?? Array.Empty<string>(),
            implications = implications ?? Array.Empty<SemanticImplication>()
        });
    }

    private static string[] Ids(params SemanticAsset[] semantics)
    {
        var ids = new string[semantics.Length];
        for (var i = 0; i < semantics.Length; i++) ids[i] = semantics[i].id;
        return ids;
    }

    private static SemanticImplication[] Implies(SemanticAsset semantic, float strength)
    {
        return new[] { new SemanticImplication(semantic.id, strength) };
    }
}
