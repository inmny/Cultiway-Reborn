using System;
using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.Libraries;
using strings;

namespace Cultiway.Content;

[Dependency(typeof(BaseStatses), typeof(WorldboxGame.BaseStats), typeof(ItemShapes), typeof(ActorTraits), typeof(Operations))]
public class ElixirEffectAtoms : ExtendLibrary<ElixirEffectAtomAsset, ElixirEffectAtoms>
{
    private const string MultiplierCrit = "multiplier_crit";
    private const string MultiplierLifespan = "multiplier_lifespan";
    private const string MultiplierMana = "multiplier_mana";
    private const string MultiplierStamina = "multiplier_stamina";
    private const string MultiplierDiplomacy = "multiplier_diplomacy";
    private const string Mana = "mana";
    private const string Stamina = "stamina";
    private const string AccuracyStat = "accuracy";
    private const string CriticalChance = "critical_chance";
    private const string SkillCombat = "skill_combat";
    private const string SkillSpell = "skill_spell";

    public static ElixirEffectAtomAsset Body { get; private set; }
    public static ElixirEffectAtomAsset HealthRegen { get; private set; }
    public static ElixirEffectAtomAsset WakanCapacity { get; private set; }
    public static ElixirEffectAtomAsset WakanRegen { get; private set; }
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
            Stats((S.multiplier_health, 1.4f), (S.health, 28f)),
            Stats((S.health, 5f)),
            r => 0.4f + Shape(r, "blood", "bone", "meat") + Element(r, ElementIndex.Earth));

        Set(HealthRegen, "health_regen", ["回春", "复元", "生肌"], "再生药性", "大幅提升生命恢复",
            Stats(Flat(WorldboxGame.BaseStats.HealthRegen, 22f), Mod(WorldboxGame.BaseStats.HealthRegen, 3.5f)),
            Stats(Flat(WorldboxGame.BaseStats.HealthRegen, 0.35f)),
            r => Shape(r, "blood", "liquid", "herb", "root") + Element(r, ElementIndex.Wood, ElementIndex.Water));

        Set(WakanCapacity, "wakan_capacity", ["纳元", "聚灵", "广脉"], "灵海容量", "扩张灵力上限",
            Stats((BaseStatses.MaxWakan.id, 72f)),
            Stats((BaseStatses.MaxWakan.id, 8f)),
            r => Shape(r, "crystal", "stone", "lotus", "ball") + Element(r, ElementIndex.Water, ElementIndex.Pos, ElementIndex.Entropy));

        Set(WakanRegen, "wakan_regen", ["回元", "引灵", "归息"], "灵息回转", "提升灵力恢复",
            Stats((BaseStatses.WakanRegen.id, 4.2f)),
            Stats((BaseStatses.WakanRegen.id, 0.35f)),
            r => Shape(r, "liquid", "lotus", "herb") + Element(r, ElementIndex.Water, ElementIndex.Wood, ElementIndex.Pos));

        Set(Mind, "mind", ["明心", "清神", "定识"], "清明神识", "大幅提升悟性",
            Stats((S.intelligence, 46f)),
            Stats((S.intelligence, 5f)),
            r => Shape(r, "eye", "fruit", "lotus") + Element(r, ElementIndex.Pos, ElementIndex.Entropy),
            [ActorTraits.OpenSource.id]);

        Set(Life, "life", ["延寿", "驻年", "生息"], "寿元生机", "大幅延展寿元",
            Stats((MultiplierLifespan, 2.4f)),
            Stats((S.lifespan, 4f)),
            r => Shape(r, "root", "mushroom", "fruit", "wood") + Element(r, ElementIndex.Wood));

        Set(Speed, "speed", ["风行", "轻灵", "迅影"], "轻身药性", "大幅提升移动速度",
            Stats((S.multiplier_speed, 1.5f), (S.speed, 12f)),
            Stats((S.speed, 4f)),
            r => Shape(r, "wing", "feather", "fur") + Element(r, ElementIndex.Neg));

        Set(AttackSpeed, "attack_speed", ["疾攻", "连锋", "急影"], "疾攻药性", "大幅提升攻击速度",
            Stats((S.multiplier_attack_speed, 1.2f), (S.attack_speed, 8f)),
            Stats((S.attack_speed, 2f)),
            r => Shape(r, "claw", "feather", "wing", "tooth") + Element(r, ElementIndex.Iron, ElementIndex.Fire, ElementIndex.Neg));

        Set(Damage, "damage", ["破锋", "摧锋", "裂刃"], "锋锐杀机", "大幅提升伤害",
            Stats((S.multiplier_damage, 1.4f), (S.damage, 16f)),
            Stats((S.damage, 3f)),
            r => Shape(r, "claw", "tooth", "horn") + Element(r, ElementIndex.Iron, ElementIndex.Fire));

        Set(Crit, "crit", ["会心", "断机", "破绽"], "会心灵机", "大幅提升暴击",
            Stats((MultiplierCrit, 1.1f), (CriticalChance, 0.45f)),
            Stats((CriticalChance, 0.035f)),
            r => Shape(r, "eye", "horn", "tooth", "crystal") + Element(r, ElementIndex.Iron, ElementIndex.Neg, ElementIndex.Entropy));

        Set(Accuracy, "accuracy", ["洞微", "命中", "照准"], "洞微感知", "大幅提升命中",
            Stats((AccuracyStat, 70f)),
            Stats((AccuracyStat, 4f)),
            r => Shape(r, "eye", "feather", "horn") + Element(r, ElementIndex.Iron, ElementIndex.Pos, ElementIndex.Neg));

        Set(DamageRange, "damage_range", ["穿云", "裂甲", "洞金"], "贯穿锐意", "扩展伤害穿透范围",
            Stats((S.damage_range, 0.35f)),
            Stats((S.damage_range, 0.025f)),
            r => Shape(r, "horn", "tooth", "stone", "crystal") + Element(r, ElementIndex.Iron));

        Set(Armor, "armor", ["玄甲", "坚鳞", "护身"], "护体坚意", "大幅提升护甲",
            Stats((S.armor, 28f)),
            Stats((S.armor, 3f)),
            r => Shape(r, "shell", "stone", "bone", "hoof") + Element(r, ElementIndex.Iron, ElementIndex.Earth));

        Set(Soul, "soul", ["养魂", "凝魄", "神魂"], "魂魄清光", "壮大神魂",
            Stats(Flat(WorldboxGame.BaseStats.MaxSoul, 160f), Mod(WorldboxGame.BaseStats.MaxSoul, 2.4f)),
            Stats(Flat(WorldboxGame.BaseStats.MaxSoul, 10f)),
            r => Shape(r, "eye", "crystal", "ball", "lotus") + Element(r, ElementIndex.Pos, ElementIndex.Entropy));

        Set(Qiyun, "qiyun", ["聚运", "天眷", "紫气"], "气运紫华", "汇聚气运",
            Stats(Flat(WorldboxGame.BaseStats.MaxQiyun, 150f), Mod(WorldboxGame.BaseStats.MaxQiyun, 2.5f)),
            Stats(Flat(WorldboxGame.BaseStats.MaxQiyun, 8f)),
            r => Shape(r, "fruit", "lotus", "crystal", "ball") + Quality(r, 1) + Element(r, ElementIndex.Pos));

        Set(ManaPool, "mana_pool", ["法力", "灵池", "玄泉"], "法力池泉", "大幅扩展法力",
            Stats((Mana, 130f), (MultiplierMana, 2f)),
            Stats((Mana, 8f)),
            r => Shape(r, "liquid", "crystal", "lotus") + Element(r, ElementIndex.Water, ElementIndex.Pos, ElementIndex.Entropy));

        Set(StaminaPool, "stamina_pool", ["气力", "劲脉", "耐战"], "气力根基", "大幅提升气力",
            Stats((Stamina, 120f), (MultiplierStamina, 1.8f)),
            Stats((Stamina, 7f)),
            r => Shape(r, "bone", "hoof", "root", "shell") + Element(r, ElementIndex.Earth, ElementIndex.Wood));

        Set(CombatSkill, "combat_skill", ["战魄", "武胆", "斗罡"], "战技精魄", "大幅提升战技",
            Stats((SkillCombat, 1.6f)),
            Stats((SkillCombat, 0.07f)),
            r => Shape(r, "claw", "tooth", "horn", "bone", "hoof") + Element(r, ElementIndex.Iron, ElementIndex.Fire, ElementIndex.Earth));

        Set(SpellSkill, "spell_skill", ["术心", "法印", "灵枢"], "术法灵枢", "大幅提升术法",
            Stats((SkillSpell, 1.6f)),
            Stats((SkillSpell, 0.07f)),
            r => Shape(r, "eye", "crystal", "lotus", "ball") + Element(r, ElementIndex.Water, ElementIndex.Pos, ElementIndex.Entropy));

        Set(Jindan, "jindan", ["丹核", "金丹", "抱元"], "金丹余韵", "温养金丹根基",
            Stats((BaseStatses.MaxWakan.id, 88f)),
            Stats((BaseStatses.MaxWakan.id, 7f)),
            r => Shape(r, "ball") + HasJindan(r) + Quality(r, 1),
            operations: [Operations.EnhanceJindan.id]);

        Set(RootOpening, "root_opening", ["开灵", "启根", "通脉"], "灵根引子", "牵引灵根",
            Stats((BaseStatses.WakanRegen.id, 1.8f)),
            Stats((BaseStatses.MaxWakan.id, 4f)),
            r => Shape(r, "root", "lotus", "crystal") + Quality(r, 1) + Element(r, ElementIndex.Wood, ElementIndex.Pos),
            [ActorTraits.Cultivator.id],
            [Operations.OpenElementRoot.id]);

        Set(IronMastery, "iron_mastery", ["庚金", "金锋", "锐金"], "金行掌握", "大幅提升金行掌握",
            Stats(Flat(WorldboxGame.BaseStats.IronMaster, 12f), Mod(WorldboxGame.BaseStats.IronMaster, 2.4f)),
            Stats(Flat(WorldboxGame.BaseStats.IronMaster, 1f)),
            r => Element(r, ElementIndex.Iron) + Shape(r, "stone", "crystal", "horn", "tooth"));

        Set(IronGuard, "iron_guard", ["金甲", "铁壁", "玄金"], "金行抗性", "大幅提升金行抗性",
            Stats(Flat(WorldboxGame.BaseStats.IronArmor, 12f), Mod(WorldboxGame.BaseStats.IronArmor, 1.8f)),
            Stats(Flat(WorldboxGame.BaseStats.IronArmor, 1f)),
            r => Element(r, ElementIndex.Iron) + Shape(r, "shell", "stone", "bone"));

        Set(WoodMastery, "wood_mastery", ["青木", "长青", "灵木"], "木行掌握", "大幅提升木行掌握",
            Stats(Flat(WorldboxGame.BaseStats.WoodMaster, 11f), Mod(WorldboxGame.BaseStats.WoodMaster, 2f)),
            Stats(Flat(WorldboxGame.BaseStats.WoodMaster, 0.9f)),
            r => Element(r, ElementIndex.Wood) + Shape(r, "root", "wood", "mushroom", "fruit", "herb"));

        Set(WoodGuard, "wood_guard", ["木甲", "青屏", "生壁"], "木行抗性", "大幅提升木行抗性",
            Stats(Flat(WorldboxGame.BaseStats.WoodArmor, 12f), Mod(WorldboxGame.BaseStats.WoodArmor, 1.8f)),
            Stats(Flat(WorldboxGame.BaseStats.WoodArmor, 1f)),
            r => Element(r, ElementIndex.Wood) + Shape(r, "root", "wood", "shell"));

        Set(WaterMastery, "water_mastery", ["玄水", "寒泉", "澄流"], "水行掌握", "大幅提升水行掌握",
            Stats(Flat(WorldboxGame.BaseStats.WaterMaster, 11f), Mod(WorldboxGame.BaseStats.WaterMaster, 2f)),
            Stats(Flat(WorldboxGame.BaseStats.WaterMaster, 0.9f)),
            r => Element(r, ElementIndex.Water) + Shape(r, "liquid", "lotus", "crystal"));

        Set(WaterGuard, "water_guard", ["水幕", "寒甲", "玄流"], "水行抗性", "大幅提升水行抗性",
            Stats(Flat(WorldboxGame.BaseStats.WaterArmor, 12f), Mod(WorldboxGame.BaseStats.WaterArmor, 1.8f)),
            Stats(Flat(WorldboxGame.BaseStats.WaterArmor, 1f)),
            r => Element(r, ElementIndex.Water) + Shape(r, "liquid", "lotus", "shell"));

        Set(FireMastery, "fire_mastery", ["离火", "炎华", "赤炼"], "火行掌握", "大幅提升火行掌握",
            Stats(Flat(WorldboxGame.BaseStats.FireMaster, 13f), Mod(WorldboxGame.BaseStats.FireMaster, 2.6f)),
            Stats(Flat(WorldboxGame.BaseStats.FireMaster, 1.1f)),
            r => Element(r, ElementIndex.Fire) + Shape(r, "blood", "crystal", "tooth", "horn"));

        Set(FireGuard, "fire_guard", ["火衣", "炎甲", "赤障"], "火行抗性", "大幅提升火行抗性",
            Stats(Flat(WorldboxGame.BaseStats.FireArmor, 12f), Mod(WorldboxGame.BaseStats.FireArmor, 1.8f)),
            Stats(Flat(WorldboxGame.BaseStats.FireArmor, 1f)),
            r => Element(r, ElementIndex.Fire) + Shape(r, "shell", "stone", "blood"));

        Set(EarthMastery, "earth_mastery", ["坤土", "厚岳", "山魄"], "土行掌握", "大幅提升土行掌握",
            Stats(Flat(WorldboxGame.BaseStats.EarthMaster, 10f), Mod(WorldboxGame.BaseStats.EarthMaster, 1.8f)),
            Stats(Flat(WorldboxGame.BaseStats.EarthMaster, 0.8f)),
            r => Element(r, ElementIndex.Earth) + Shape(r, "stone", "bone", "root", "shell"));

        Set(EarthGuard, "earth_guard", ["土甲", "山壁", "镇岳"], "土行抗性", "大幅提升土行抗性",
            Stats(Flat(WorldboxGame.BaseStats.EarthArmor, 14f), Mod(WorldboxGame.BaseStats.EarthArmor, 2.2f)),
            Stats(Flat(WorldboxGame.BaseStats.EarthArmor, 1.1f)),
            r => Element(r, ElementIndex.Earth) + Shape(r, "stone", "bone", "shell"));

        Set(YinMastery, "yin_mastery", ["玄阴", "幽冥", "阴华"], "阴性掌握", "大幅提升阴性掌握",
            Stats(Flat(WorldboxGame.BaseStats.NegMaster, 12f), Mod(WorldboxGame.BaseStats.NegMaster, 2.4f)),
            Stats(Flat(WorldboxGame.BaseStats.NegMaster, 1f)),
            r => Element(r, ElementIndex.Neg) + Shape(r, "fur", "wing", "silk", "eye"));

        Set(YinGuard, "yin_guard", ["阴甲", "幽障", "玄屏"], "阴性抗性", "大幅提升阴性抗性",
            Stats(Flat(WorldboxGame.BaseStats.NegArmor, 12f), Mod(WorldboxGame.BaseStats.NegArmor, 1.8f)),
            Stats(Flat(WorldboxGame.BaseStats.NegArmor, 1f)),
            r => Element(r, ElementIndex.Neg) + Shape(r, "fur", "shell", "silk"));

        Set(YangMastery, "yang_mastery", ["阳华", "曜光", "明阳"], "阳性掌握", "大幅提升阳性掌握",
            Stats(Flat(WorldboxGame.BaseStats.PosMaster, 12f), Mod(WorldboxGame.BaseStats.PosMaster, 2.4f)),
            Stats(Flat(WorldboxGame.BaseStats.PosMaster, 1f)),
            r => Element(r, ElementIndex.Pos) + Shape(r, "lotus", "eye", "crystal", "fruit"));

        Set(YangGuard, "yang_guard", ["阳甲", "明障", "曜屏"], "阳性抗性", "大幅提升阳性抗性",
            Stats(Flat(WorldboxGame.BaseStats.PosArmor, 12f), Mod(WorldboxGame.BaseStats.PosArmor, 1.8f)),
            Stats(Flat(WorldboxGame.BaseStats.PosArmor, 1f)),
            r => Element(r, ElementIndex.Pos) + Shape(r, "lotus", "shell", "crystal"));

        Set(EntropyMastery, "entropy_mastery", ["混沌", "浊玄", "归墟"], "混沌掌握", "大幅提升混沌掌握",
            Stats(Flat(WorldboxGame.BaseStats.EntropyMaster, 14f), Mod(WorldboxGame.BaseStats.EntropyMaster, 2.8f)),
            Stats(Flat(WorldboxGame.BaseStats.EntropyMaster, 1.2f)),
            r => Element(r, ElementIndex.Entropy) + Shape(r, "crystal", "eye", "ball", "stone"));

        Set(EntropyGuard, "entropy_guard", ["混甲", "浊障", "归墟"], "混沌抗性", "大幅提升混沌抗性",
            Stats(Flat(WorldboxGame.BaseStats.EntropyArmor, 14f), Mod(WorldboxGame.BaseStats.EntropyArmor, 2.2f)),
            Stats(Flat(WorldboxGame.BaseStats.EntropyArmor, 1.2f)),
            r => Element(r, ElementIndex.Entropy) + Shape(r, "crystal", "shell", "stone"));
    }

    private static void Set(
        ElixirEffectAtomAsset atom,
        string tag,
        string[] nameStems,
        string fragment,
        string sentence,
        Dictionary<string, float> statusStats,
        Dictionary<string, float> dataAttributes,
        Func<ElixirRecipeContext, float> score,
        string[] traits = null,
        string[] operations = null)
    {
        atom.tag = tag;
        atom.name_stems = nameStems;
        atom.description_fragment = fragment;
        atom.effect_sentence = sentence;
        atom.status_stats = statusStats;
        atom.data_attributes = dataAttributes;
        atom.ScoreRecipe = score;
        atom.data_traits = traits ?? [];
        atom.data_operations = operations ?? [];
        atom.keywords = BuildKeywords(nameStems, fragment, sentence);
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
        if (WorldboxGame.BaseStats.StatsToModStats.TryGetValue(stat.id, out var id))
        {
            return (id, value);
        }
        return ($"Mod{stat.id}", value);
    }

    private static string[] BuildKeywords(string[] stems, string fragment, string sentence)
    {
        List<string> result = new();
        if (stems != null) result.AddRange(stems);
        if (!string.IsNullOrEmpty(fragment)) result.Add(fragment);
        if (!string.IsNullOrEmpty(sentence)) result.Add(sentence);
        return result.ToArray();
    }

    private static float Shape(ElixirRecipeContext recipe, params string[] folders)
    {
        if (string.IsNullOrEmpty(recipe.main_shape_id) || folders == null) return 0f;
        for (var i = 0; i < folders.Length; i++)
        {
            if (recipe.main_shape_id == ItemShapes.ShapeId(folders[i])) return 4f;
        }
        return 0f;
    }

    private static float Element(ElixirRecipeContext recipe, params int[] elements)
    {
        if (elements == null) return 0f;
        var score = 0f;
        for (var i = 0; i < elements.Length; i++)
        {
            if (recipe.primary_element_index == elements[i]) score += 3f;
            if (recipe.secondary_element_index == elements[i]) score += 1.2f;
        }
        return score;
    }

    private static float HasJindan(ElixirRecipeContext recipe)
    {
        return string.IsNullOrEmpty(recipe.main_jindan_id) ? 0f : 3f;
    }

    private static float Quality(ElixirRecipeContext recipe, int minStage)
    {
        return recipe.quality_stage >= minStage ? 1.5f + recipe.quality_stage * 0.4f : 0f;
    }
}
