using System;
using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.Libraries;
using Cultiway.Content.Semantics;
using Cultiway.Core.Libraries;
using Cultiway.Core.Semantics;
using strings;

namespace Cultiway.Content;

[Dependency(typeof(BaseStatses), typeof(WorldboxGame.BaseStats), typeof(ActorTraits), typeof(Operations),
    typeof(CultivationSemantics))]
public class ElixirEffectAtoms : ExtendLibrary<ElixirEffectAtomAsset, ElixirEffectAtoms>
{
    private const float RegularDataGainChance = 0.08f;
    private const float GrowthDataGainChance = 0.16f;

    public static ElixirEffectAtomAsset Body { get; private set; }
    public static ElixirEffectAtomAsset HealthRegen { get; private set; }
    public static ElixirEffectAtomAsset WakanCapacity { get; private set; }
    public static ElixirEffectAtomAsset WakanRegen { get; private set; }
    public static ElixirEffectAtomAsset WakanRestore { get; private set; }
    public static ElixirEffectAtomAsset Mind { get; private set; }
    public static ElixirEffectAtomAsset Life { get; private set; }
    public static ElixirEffectAtomAsset Speed { get; private set; }
    public static ElixirEffectAtomAsset AttackSpeed { get; private set; }
    public static ElixirEffectAtomAsset Damage { get; private set; }
    public static ElixirEffectAtomAsset Crit { get; private set; }
    public static ElixirEffectAtomAsset Accuracy { get; private set; }
    public static ElixirEffectAtomAsset DamageRange { get; private set; }
    public static ElixirEffectAtomAsset Armor { get; private set; }
    public static ElixirEffectAtomAsset Soul { get; private set; }
    public static ElixirEffectAtomAsset Qiyun { get; private set; }
    public static ElixirEffectAtomAsset ManaPool { get; private set; }
    public static ElixirEffectAtomAsset StaminaPool { get; private set; }
    public static ElixirEffectAtomAsset CombatSkill { get; private set; }
    public static ElixirEffectAtomAsset SpellSkill { get; private set; }
    public static ElixirEffectAtomAsset Jindan { get; private set; }
    public static ElixirEffectAtomAsset RootOpening { get; private set; }

    public static ElixirEffectAtomAsset IronMastery { get; private set; }
    public static ElixirEffectAtomAsset IronGuard { get; private set; }
    public static ElixirEffectAtomAsset WoodMastery { get; private set; }
    public static ElixirEffectAtomAsset WoodGuard { get; private set; }
    public static ElixirEffectAtomAsset WaterMastery { get; private set; }
    public static ElixirEffectAtomAsset WaterGuard { get; private set; }
    public static ElixirEffectAtomAsset FireMastery { get; private set; }
    public static ElixirEffectAtomAsset FireGuard { get; private set; }
    public static ElixirEffectAtomAsset EarthMastery { get; private set; }
    public static ElixirEffectAtomAsset EarthGuard { get; private set; }
    public static ElixirEffectAtomAsset YinMastery { get; private set; }
    public static ElixirEffectAtomAsset YinGuard { get; private set; }
    public static ElixirEffectAtomAsset YangMastery { get; private set; }
    public static ElixirEffectAtomAsset YangGuard { get; private set; }
    public static ElixirEffectAtomAsset EntropyMastery { get; private set; }
    public static ElixirEffectAtomAsset EntropyGuard { get; private set; }

    protected override bool AutoRegisterAssets() => true;
    protected override string Prefix() => "Cultiway.ElixirEffectAtom";

    protected override void OnInit()
    {
        Set(Body, "body", ["淬体", "固元", "益血"], "血肉精气", "大幅增强体魄与血量",
            Stats((S.multiplier_health, 1.4f), (S.health, 28f)), Stats((S.health, 5f)),
            [A(CultivationSemantics.Resource.Vitality, 4), A(CultivationSemantics.Form.Body, 2),
                A(CultivationSemantics.Material.Hardness, 1), A(CultivationSemantics.Material.Stability, 1),
                A(CultivationSemantics.Theme.Dragon, 1)], GrowthDataGainChance);

        Set(HealthRegen, "health_regen", ["回春", "复元", "生肌"], "再生药性", "大幅提升生命恢复",
            Stats(Flat(WorldboxGame.BaseStats.HealthRegen, 22f), Mod(WorldboxGame.BaseStats.HealthRegen, 3.5f)),
            Stats(Flat(WorldboxGame.BaseStats.HealthRegen, 0.35f)),
            [A(CultivationSemantics.Effect.Recovery, 4), A(CultivationSemantics.Resource.Vitality, 2),
                A(CultivationSemantics.Effect.Purification, 2), A(CultivationSemantics.Craft.Alchemy, 1),
                A(SkillSemantics.Element.Wood, 1), A(SkillSemantics.Element.Water, 1)], GrowthDataGainChance);

        Set(WakanCapacity, "wakan_capacity", ["纳元", "聚灵", "广脉"], "灵海容量", "扩张灵力上限",
            Stats((BaseStatses.MaxWakan.id, 72f)), Stats((BaseStatses.MaxWakan.id, 8f)),
            [A(CultivationSemantics.Resource.Reserve, 4), A(CultivationSemantics.Material.Capacity, 4),
                A(CultivationSemantics.Resource.Spirituality, 2), A(CultivationSemantics.Effect.Resonance, 1),
                A(CultivationSemantics.Material.Stability, 1), A(SkillSemantics.Element.Water, 1),
                A(SkillSemantics.Element.Pos, 1), A(SkillSemantics.Element.Entropy, 1),
                A(SkillSemantics.Element.Generic, 1)], GrowthDataGainChance);

        Set(WakanRegen, "wakan_regen", ["回元", "引灵", "归息"], "灵息回转", "提升灵力恢复",
            Stats((BaseStatses.WakanRegen.id, 4.2f)), Stats((BaseStatses.WakanRegen.id, 0.35f)),
            [A(CultivationSemantics.Effect.Transformation, 4), A(CultivationSemantics.Resource.Reserve, 2),
                A(CultivationSemantics.Craft.Alchemy, 2), A(CultivationSemantics.Effect.Recovery, 1),
                A(CultivationSemantics.Resource.Spirituality, 1), A(SkillSemantics.Element.Water, 1),
                A(SkillSemantics.Element.Wood, 1), A(SkillSemantics.Element.Pos, 1)], GrowthDataGainChance);

        Set(WakanRestore, "wakan_restore", ["补气", "复灵", "归元"], "回灵药性", "立即恢复灵力",
            Stats(), Stats(),
            [A(CultivationSemantics.Resource.Reserve, 4), A(CultivationSemantics.Effect.Recovery, 4),
                A(CultivationSemantics.Craft.Alchemy, 2), A(SkillSemantics.Element.Water, 1),
                A(CultivationSemantics.Effect.Transformation, 1),
                A(CultivationSemantics.Resource.Spirituality, 1)], 0f,
            effectMode: ElixirAtomEffectMode.Restore);

        Set(Mind, "mind", ["明心", "清神", "定识"], "清明神识", "大幅提升悟性",
            Stats((S.intelligence, 46f)), Stats((S.intelligence, 5f)),
            [A(CultivationSemantics.Resource.Spirituality, 4), A(CultivationSemantics.Effect.Perception, 2),
                A(CultivationSemantics.Effect.Purification, 2), A(CultivationSemantics.Effect.Revealing, 1),
                A(CultivationSemantics.Material.Stability, 1), A(SkillSemantics.Element.Pos, 1),
                A(SkillSemantics.Element.Entropy, 1)], GrowthDataGainChance,
            dataGainKind: ElixirDataGainKind.Trait, traits: [ActorTraits.OpenSource.id]);

        Set(Life, "life", ["延寿", "驻年", "生息"], "寿元生机", "大幅延展寿元",
            Stats((S.multiplier_lifespan, 2.4f)), Stats((S.lifespan, 4f)),
            [A(CultivationSemantics.Resource.Vitality, 4), A(CultivationSemantics.Material.Stability, 2),
                A(CultivationSemantics.Effect.Recovery, 1), A(SkillSemantics.Element.Wood, 1),
                A(CultivationSemantics.Effect.Purification, 1)], GrowthDataGainChance);

        Set(Speed, "speed", ["风行", "轻灵", "迅影"], "轻身药性", "大幅提升移动速度",
            Stats((S.multiplier_speed, 1.5f), (S.speed, 12f)), Stats((S.speed, 4f)),
            [A(CultivationSemantics.Effect.Mobility, 4), A(SkillSemantics.Element.Wind, 4),
                A(CultivationSemantics.Material.Lightweight, 2),
                A(CultivationSemantics.Material.Flexibility, 1), A(SkillSemantics.Element.Neg, 1)],
            RegularDataGainChance);

        Set(AttackSpeed, "attack_speed", ["疾攻", "连锋", "急影"], "疾攻药性", "大幅提升攻击速度",
            Stats((S.multiplier_attack_speed, 1.2f), (S.attack_speed, 8f)), Stats((S.attack_speed, 2f)),
            [A(SkillSemantics.Element.Lightning, 4), A(CultivationSemantics.Effect.Mobility, 2),
                A(CultivationSemantics.Material.Flexibility, 2), A(CultivationSemantics.Material.Lightweight, 1),
                A(CultivationSemantics.Material.Volatility, 1), A(CultivationSemantics.Effect.Binding, 1),
                A(CultivationSemantics.Effect.Concealment, 1), A(SkillSemantics.Element.Fire, 1),
                A(SkillSemantics.Element.Iron, 1), A(SkillSemantics.Element.Neg, 1)], RegularDataGainChance);

        Set(Damage, "damage", ["破锋", "摧锋", "裂刃"], "锋锐杀机", "大幅提升伤害",
            Stats((S.multiplier_damage, 1.4f), (S.damage, 16f)), Stats((S.damage, 3f)),
            [A(CultivationSemantics.Effect.ArmorBreak, 4), A(SkillSemantics.Element.Poison, 4),
                A(CultivationSemantics.Form.Blade, 2), A(CultivationSemantics.Effect.Impact, 2),
                A(SkillSemantics.Element.Lightning, 2), A(CultivationSemantics.Material.Volatility, 1),
                A(SkillSemantics.Element.Fire, 1), A(SkillSemantics.Element.Iron, 1),
                A(CultivationSemantics.Theme.Dragon, 1)], RegularDataGainChance);

        Set(Crit, "crit", ["会心", "断机", "破绽"], "会心灵机", "大幅提升暴击",
            Stats((S.multiplier_crit, 1.1f), (S.critical_chance, 0.45f)), Stats((S.critical_chance, 0.035f)),
            [A(CultivationSemantics.Effect.Concealment, 4), A(CultivationSemantics.Effect.Perception, 2),
                A(CultivationSemantics.Material.Brittle, 2), A(CultivationSemantics.Material.Volatility, 2),
                A(CultivationSemantics.Effect.Revealing, 1), A(SkillSemantics.Element.Neg, 1),
                A(SkillSemantics.Element.Entropy, 1), A(SkillSemantics.Element.Lightning, 1),
                A(SkillSemantics.Element.Poison, 1)], RegularDataGainChance);

        Set(Accuracy, "accuracy", ["洞微", "命中", "照准"], "洞微感知", "大幅提升命中",
            Stats((S.accuracy, 70f)), Stats((S.accuracy, 4f)),
            [A(CultivationSemantics.Effect.Revealing, 4), A(CultivationSemantics.Effect.Perception, 2),
                A(CultivationSemantics.Effect.Resonance, 1), A(SkillSemantics.Element.Iron, 1),
                A(SkillSemantics.Element.Pos, 1)], RegularDataGainChance);

        Set(DamageRange, "damage_range", ["穿云", "裂甲", "洞金"], "贯穿锐意", "扩展伤害穿透范围",
            Stats((S.damage_range, 0.35f)), Stats((S.damage_range, 0.025f)),
            [A(CultivationSemantics.Material.Brittle, 4), A(CultivationSemantics.Effect.ArmorBreak, 2),
                A(CultivationSemantics.Form.Blade, 1), A(CultivationSemantics.Effect.Perception, 1),
                A(SkillSemantics.Element.Iron, 1)], RegularDataGainChance);

        Set(Armor, "armor", ["玄甲", "坚鳞", "护身"], "护体坚意", "大幅提升护甲",
            Stats((S.armor, 28f)), Stats((S.armor, 3f)),
            [A(CultivationSemantics.Effect.Ward, 4), A(CultivationSemantics.Material.Hardness, 2),
                A(CultivationSemantics.Material.Stability, 2), A(CultivationSemantics.Material.Immoveable, 2),
                A(CultivationSemantics.Material.Flexibility, 1), A(CultivationSemantics.Effect.Binding, 1),
                A(SkillSemantics.Element.Earth, 1), A(SkillSemantics.Element.Iron, 1),
                A(SkillSemantics.Element.Ice, 1)], RegularDataGainChance);

        Set(Soul, "soul", ["养魂", "凝魄", "神魂"], "魂魄清光", "壮大神魂",
            Stats(Flat(WorldboxGame.BaseStats.MaxSoul, 160f), Mod(WorldboxGame.BaseStats.MaxSoul, 2.4f)),
            Stats(Flat(WorldboxGame.BaseStats.MaxSoul, 10f)),
            [A(CultivationSemantics.Theme.Soul, 4), A(CultivationSemantics.Theme.Spirit, 4),
                A(CultivationSemantics.Resource.Spirituality, 2), A(CultivationSemantics.Effect.Resonance, 2),
                A(CultivationSemantics.Effect.Perception, 1), A(SkillSemantics.Element.Pos, 1),
                A(SkillSemantics.Element.Entropy, 1)], GrowthDataGainChance);

        Set(Qiyun, "qiyun", ["聚运", "天眷", "紫气"], "气运紫华", "汇聚气运",
            Stats(Flat(WorldboxGame.BaseStats.MaxQiyun, 150f), Mod(WorldboxGame.BaseStats.MaxQiyun, 2.5f)),
            Stats(Flat(WorldboxGame.BaseStats.MaxQiyun, 8f)),
            [A(CultivationSemantics.Path.FortuneCultivation, 4), A(SkillSemantics.Element.Pos, 2),
                A(CultivationSemantics.Effect.Purification, 2), A(CultivationSemantics.Effect.Resonance, 1),
                A(CultivationSemantics.Resource.Spirituality, 1), A(CultivationSemantics.Theme.Dragon, 1)],
            GrowthDataGainChance, minimumStage: 1);

        Set(ManaPool, "mana_pool", ["法力", "灵池", "玄泉"], "法力池泉", "大幅扩展法力",
            Stats((S.mana, 130f), (S.multiplier_mana, 2f)), Stats((S.mana, 8f)),
            [A(CultivationSemantics.Theme.Spirit, 4), A(CultivationSemantics.Resource.Spirituality, 2),
                A(CultivationSemantics.Effect.Transformation, 2), A(CultivationSemantics.Resource.Reserve, 1),
                A(SkillSemantics.Element.Water, 1), A(SkillSemantics.Element.Pos, 1),
                A(SkillSemantics.Element.Entropy, 1), A(SkillSemantics.Element.Generic, 1)],
            GrowthDataGainChance);

        Set(StaminaPool, "stamina_pool", ["气力", "劲脉", "耐战"], "气力根基", "大幅提升气力",
            Stats((S.stamina, 120f), (S.multiplier_stamina, 1.8f)), Stats((S.stamina, 7f)),
            [A(CultivationSemantics.Material.Stability, 4), A(CultivationSemantics.Resource.Vitality, 2),
                A(CultivationSemantics.Material.Hardness, 2), A(CultivationSemantics.Form.Body, 1),
                A(SkillSemantics.Element.Earth, 1), A(SkillSemantics.Element.Wood, 1)], GrowthDataGainChance);

        Set(CombatSkill, "combat_skill", ["战魄", "武胆", "斗罡"], "战技精魄", "大幅提升战技",
            Stats((S.skill_combat, 1.6f)), Stats((S.skill_combat, 0.07f)),
            [A(CultivationSemantics.Path.BattleCultivation, 4), A(CultivationSemantics.Path.Sword, 4),
                A(CultivationSemantics.Form.Blade, 2), A(CultivationSemantics.Effect.ArmorBreak, 2),
                A(CultivationSemantics.Effect.Impact, 2), A(CultivationSemantics.Material.Hardness, 1),
                A(SkillSemantics.Element.Fire, 1), A(SkillSemantics.Element.Iron, 1),
                A(SkillSemantics.Element.Earth, 1), A(CultivationSemantics.Theme.Dragon, 1)],
            RegularDataGainChance);

        Set(SpellSkill, "spell_skill", ["术心", "法印", "灵枢"], "术法灵枢", "大幅提升术法",
            Stats((S.skill_spell, 1.6f)), Stats((S.skill_spell, 0.07f)),
            [A(CultivationSemantics.Path.Meditation, 4), A(CultivationSemantics.Theme.Elemental, 2),
                A(CultivationSemantics.Resource.Spirituality, 2), A(SkillSemantics.Element.Poison, 2),
                A(CultivationSemantics.Effect.Transformation, 1), A(CultivationSemantics.Effect.Resonance, 1),
                A(CultivationSemantics.Effect.Perception, 1), A(CultivationSemantics.Craft.Alchemy, 1),
                A(SkillSemantics.Element.Generic, 1)], RegularDataGainChance);

        Set(Jindan, "jindan", ["丹核", "金丹", "抱元"], "金丹余韵", "温养金丹根基",
            Stats(), Stats(),
            [A(CultivationSemantics.Realm.Jindan, 6), A(CultivationSemantics.Effect.Resonance, 2),
                A(CultivationSemantics.Resource.Reserve, 1), A(CultivationSemantics.Role.Cultivation, 1)], 1f,
            minimumStage: 2, requiredSemantics: [CultivationSemantics.Realm.Jindan],
            effectMode: ElixirAtomEffectMode.DataGain, dataGainKind: ElixirDataGainKind.OneTime,
            operations: [Operations.EnhanceJindan]);

        Set(RootOpening, "root_opening", ["开灵", "启根", "通脉"], "灵根引子", "牵引灵根",
            Stats(), Stats(),
            [A(CultivationSemantics.Trait.ElementRoot, 6), A(CultivationSemantics.Theme.Elemental, 2),
                A(CultivationSemantics.Resource.Spirituality, 1)], 1f,
            minimumStage: 2, requiredSemantics: [CultivationSemantics.Trait.ElementRoot],
            effectMode: ElixirAtomEffectMode.DataGain, dataGainKind: ElixirDataGainKind.OneTime,
            operations: [Operations.OpenElementRoot]);

        SetElementAtoms();
    }

    private static void SetElementAtoms()
    {
        SetElementPair(IronMastery, IronGuard, SkillSemantics.Element.Iron,
            ["庚金", "金锋", "锐金"], "金行掌握", "大幅提升金行掌握",
            Stats(Flat(WorldboxGame.BaseStats.IronMaster, 12f), Mod(WorldboxGame.BaseStats.IronMaster, 2.4f)),
            Stats(Flat(WorldboxGame.BaseStats.IronMaster, 1f)),
            ["金甲", "铁壁", "玄金"], "金行抗性", "大幅提升金行抗性",
            Stats(Flat(WorldboxGame.BaseStats.IronArmor, 12f), Mod(WorldboxGame.BaseStats.IronArmor, 1.8f)),
            Stats(Flat(WorldboxGame.BaseStats.IronArmor, 1f)), "iron");
        SetElementPair(WoodMastery, WoodGuard, SkillSemantics.Element.Wood,
            ["青木", "长青", "灵木"], "木行掌握", "大幅提升木行掌握",
            Stats(Flat(WorldboxGame.BaseStats.WoodMaster, 11f), Mod(WorldboxGame.BaseStats.WoodMaster, 2f)),
            Stats(Flat(WorldboxGame.BaseStats.WoodMaster, 0.9f)),
            ["木甲", "青屏", "生壁"], "木行抗性", "大幅提升木行抗性",
            Stats(Flat(WorldboxGame.BaseStats.WoodArmor, 12f), Mod(WorldboxGame.BaseStats.WoodArmor, 1.8f)),
            Stats(Flat(WorldboxGame.BaseStats.WoodArmor, 1f)), "wood");
        SetElementPair(WaterMastery, WaterGuard, SkillSemantics.Element.Water,
            ["玄水", "寒泉", "澄流"], "水行掌握", "大幅提升水行掌握",
            Stats(Flat(WorldboxGame.BaseStats.WaterMaster, 11f), Mod(WorldboxGame.BaseStats.WaterMaster, 2f)),
            Stats(Flat(WorldboxGame.BaseStats.WaterMaster, 0.9f)),
            ["水幕", "寒甲", "玄流"], "水行抗性", "大幅提升水行抗性",
            Stats(Flat(WorldboxGame.BaseStats.WaterArmor, 12f), Mod(WorldboxGame.BaseStats.WaterArmor, 1.8f)),
            Stats(Flat(WorldboxGame.BaseStats.WaterArmor, 1f)), "water");
        SetElementPair(FireMastery, FireGuard, SkillSemantics.Element.Fire,
            ["离火", "炎华", "赤炼"], "火行掌握", "大幅提升火行掌握",
            Stats(Flat(WorldboxGame.BaseStats.FireMaster, 13f), Mod(WorldboxGame.BaseStats.FireMaster, 2.6f)),
            Stats(Flat(WorldboxGame.BaseStats.FireMaster, 1.1f)),
            ["火衣", "炎甲", "赤障"], "火行抗性", "大幅提升火行抗性",
            Stats(Flat(WorldboxGame.BaseStats.FireArmor, 12f), Mod(WorldboxGame.BaseStats.FireArmor, 1.8f)),
            Stats(Flat(WorldboxGame.BaseStats.FireArmor, 1f)), "fire");
        SetElementPair(EarthMastery, EarthGuard, SkillSemantics.Element.Earth,
            ["坤土", "厚岳", "山魄"], "土行掌握", "大幅提升土行掌握",
            Stats(Flat(WorldboxGame.BaseStats.EarthMaster, 10f), Mod(WorldboxGame.BaseStats.EarthMaster, 1.8f)),
            Stats(Flat(WorldboxGame.BaseStats.EarthMaster, 0.8f)),
            ["土甲", "山壁", "镇岳"], "土行抗性", "大幅提升土行抗性",
            Stats(Flat(WorldboxGame.BaseStats.EarthArmor, 14f), Mod(WorldboxGame.BaseStats.EarthArmor, 2.2f)),
            Stats(Flat(WorldboxGame.BaseStats.EarthArmor, 1.1f)), "earth");
        SetElementPair(YinMastery, YinGuard, SkillSemantics.Element.Neg,
            ["玄阴", "幽冥", "阴华"], "阴性掌握", "大幅提升阴性掌握",
            Stats(Flat(WorldboxGame.BaseStats.NegMaster, 12f), Mod(WorldboxGame.BaseStats.NegMaster, 2.4f)),
            Stats(Flat(WorldboxGame.BaseStats.NegMaster, 1f)),
            ["阴甲", "幽障", "玄屏"], "阴性抗性", "大幅提升阴性抗性",
            Stats(Flat(WorldboxGame.BaseStats.NegArmor, 12f), Mod(WorldboxGame.BaseStats.NegArmor, 1.8f)),
            Stats(Flat(WorldboxGame.BaseStats.NegArmor, 1f)), "yin");
        SetElementPair(YangMastery, YangGuard, SkillSemantics.Element.Pos,
            ["阳华", "曜光", "明阳"], "阳性掌握", "大幅提升阳性掌握",
            Stats(Flat(WorldboxGame.BaseStats.PosMaster, 12f), Mod(WorldboxGame.BaseStats.PosMaster, 2.4f)),
            Stats(Flat(WorldboxGame.BaseStats.PosMaster, 1f)),
            ["阳甲", "明障", "曜屏"], "阳性抗性", "大幅提升阳性抗性",
            Stats(Flat(WorldboxGame.BaseStats.PosArmor, 12f), Mod(WorldboxGame.BaseStats.PosArmor, 1.8f)),
            Stats(Flat(WorldboxGame.BaseStats.PosArmor, 1f)), "yang");
        SetElementPair(EntropyMastery, EntropyGuard, SkillSemantics.Element.Entropy,
            ["混沌", "浊玄", "归墟"], "混沌掌握", "大幅提升混沌掌握",
            Stats(Flat(WorldboxGame.BaseStats.EntropyMaster, 14f), Mod(WorldboxGame.BaseStats.EntropyMaster, 2.8f)),
            Stats(Flat(WorldboxGame.BaseStats.EntropyMaster, 1.2f)),
            ["混甲", "浊障", "归墟"], "混沌抗性", "大幅提升混沌抗性",
            Stats(Flat(WorldboxGame.BaseStats.EntropyArmor, 14f), Mod(WorldboxGame.BaseStats.EntropyArmor, 2.2f)),
            Stats(Flat(WorldboxGame.BaseStats.EntropyArmor, 1.2f)), "entropy");
    }

    private static void SetElementPair(
        ElixirEffectAtomAsset mastery,
        ElixirEffectAtomAsset guard,
        SemanticAsset element,
        string[] masteryStems,
        string masteryFragment,
        string masterySentence,
        Dictionary<string, float> masteryStatus,
        Dictionary<string, float> masteryData,
        string[] guardStems,
        string guardFragment,
        string guardSentence,
        Dictionary<string, float> guardStatus,
        Dictionary<string, float> guardData,
        string key)
    {
        Set(mastery, key + "_mastery", masteryStems, masteryFragment, masterySentence, masteryStatus, masteryData,
            [A(element, 6), A(CultivationSemantics.Material.Volatility, 2),
                A(CultivationSemantics.Effect.ArmorBreak, 1),
                A(CultivationSemantics.Effect.Transformation, 1),
                A(CultivationSemantics.Effect.Resonance, 1)], RegularDataGainChance,
            requiredSemantics: [element]);
        Set(guard, key + "_guard", guardStems, guardFragment, guardSentence, guardStatus, guardData,
            [A(element, 6), A(CultivationSemantics.Effect.Ward, 2),
                A(CultivationSemantics.Material.Hardness, 1),
                A(CultivationSemantics.Material.Stability, 1),
                A(CultivationSemantics.Effect.Purification, 1)], RegularDataGainChance,
            requiredSemantics: [element]);
    }

    private static void Set(
        ElixirEffectAtomAsset atom,
        string effectKey,
        string[] nameStems,
        string fragment,
        string sentence,
        Dictionary<string, float> statusStats,
        Dictionary<string, float> dataAttributes,
        ElixirSemanticAffinity[] affinities,
        float dataGainChance,
        int minimumStage = 0,
        SemanticAsset[] requiredSemantics = null,
        ElixirAtomEffectMode effectMode = ElixirAtomEffectMode.Adaptive,
        ElixirDataGainKind dataGainKind = ElixirDataGainKind.Attribute,
        string[] traits = null,
        OperationAsset[] operations = null,
        float baseScore = 0f)
    {
        atom.effect_key = effectKey;
        atom.name_stems = nameStems;
        atom.description_fragment = fragment;
        atom.effect_sentence = sentence;
        atom.status_stats = statusStats;
        atom.data_attributes = dataAttributes;
        atom.semantic_affinities = affinities ?? [];
        atom.data_gain_chance = dataGainChance;
        atom.minimum_quality_stage = minimumStage;
        atom.required_semantics = requiredSemantics ?? [];
        atom.effect_mode = effectMode;
        atom.data_gain_kind = dataGainKind;
        atom.data_traits = traits ?? [];
        atom.data_operations = operations ?? [];
        atom.base_score = baseScore;
    }

    private static ElixirSemanticAffinity A(SemanticAsset semantic, float weight)
    {
        return new ElixirSemanticAffinity(semantic, weight);
    }

    private static Dictionary<string, float> Stats(params (string Key, float Value)[] values)
    {
        Dictionary<string, float> result = new();
        foreach (var value in values)
        {
            if (string.IsNullOrEmpty(value.Key) || value.Value == 0f) continue;
            result[value.Key] = value.Value;
        }
        return result;
    }

    private static (string Key, float Value) Flat(BaseStatAsset stat, float value)
    {
        return (stat?.id, value);
    }

    private static (string Key, float Value) Mod(BaseStatAsset stat, float value)
    {
        if (stat == null) return (null, value);
        return WorldboxGame.BaseStats.StatsToModStats.TryGetValue(stat.id, out var id)
            ? (id, value)
            : ($"Mod{stat.id}", value);
    }
}
