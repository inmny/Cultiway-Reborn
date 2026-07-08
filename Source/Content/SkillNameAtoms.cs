using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Core.AIGCLib;
using Cultiway.Core.SkillLibV3;
using ElementTag = Cultiway.Core.SkillLibV3.SkillTags.Element;
using FormTag = Cultiway.Core.SkillLibV3.SkillTags.Form;
using ModifierTag = Cultiway.Core.SkillLibV3.SkillTags.Modifier;
using MotionTag = Cultiway.Core.SkillLibV3.SkillTags.Motion;
using SeriesTag = Cultiway.Core.SkillLibV3.SkillTags.Series;

namespace Cultiway.Content;

internal class SkillNameAtoms : ExtendLibrary<SkillNameAtomAsset, SkillNameAtoms>
{
    public static SkillNameAtomAsset ElementIron { get; private set; }
    public static SkillNameAtomAsset ElementWood { get; private set; }
    public static SkillNameAtomAsset ElementWater { get; private set; }
    public static SkillNameAtomAsset ElementFire { get; private set; }
    public static SkillNameAtomAsset ElementEarth { get; private set; }
    public static SkillNameAtomAsset ElementNeg { get; private set; }
    public static SkillNameAtomAsset ElementPos { get; private set; }
    public static SkillNameAtomAsset ElementEntropy { get; private set; }
    public static SkillNameAtomAsset ElementWind { get; private set; }
    public static SkillNameAtomAsset ElementLightning { get; private set; }
    public static SkillNameAtomAsset ElementGeneric { get; private set; }

    public static SkillNameAtomAsset FormSlash { get; private set; }
    public static SkillNameAtomAsset FormPierce { get; private set; }
    public static SkillNameAtomAsset FormBall { get; private set; }
    public static SkillNameAtomAsset FormAoe { get; private set; }
    public static SkillNameAtomAsset FormFalling { get; private set; }
    public static SkillNameAtomAsset FormSustain { get; private set; }
    public static SkillNameAtomAsset FormSpell { get; private set; }

    public static SkillNameAtomAsset MotionFalling { get; private set; }
    public static SkillNameAtomAsset MotionGround { get; private set; }
    public static SkillNameAtomAsset MotionSnap { get; private set; }
    public static SkillNameAtomAsset MotionVortex { get; private set; }
    public static SkillNameAtomAsset MotionRain { get; private set; }
    public static SkillNameAtomAsset MotionReturn { get; private set; }
    public static SkillNameAtomAsset MotionZigzag { get; private set; }
    public static SkillNameAtomAsset MotionWave { get; private set; }

    public static SkillNameAtomAsset ModifierDeathSentence { get; private set; }
    public static SkillNameAtomAsset ModifierEternalCurse { get; private set; }
    public static SkillNameAtomAsset ModifierReincarnationTrial { get; private set; }
    public static SkillNameAtomAsset ModifierSilence { get; private set; }
    public static SkillNameAtomAsset ModifierBurnout { get; private set; }
    public static SkillNameAtomAsset ModifierChaos { get; private set; }
    public static SkillNameAtomAsset ModifierCombo { get; private set; }
    public static SkillNameAtomAsset ModifierMercy { get; private set; }
    public static SkillNameAtomAsset ModifierSwap { get; private set; }
    public static SkillNameAtomAsset ModifierRandomAffix { get; private set; }
    public static SkillNameAtomAsset ModifierGravity { get; private set; }
    public static SkillNameAtomAsset ModifierArmorBreak { get; private set; }
    public static SkillNameAtomAsset ModifierHuge { get; private set; }
    public static SkillNameAtomAsset ModifierDaze { get; private set; }
    public static SkillNameAtomAsset ModifierWeaken { get; private set; }
    public static SkillNameAtomAsset ModifierExplosion { get; private set; }
    public static SkillNameAtomAsset ModifierFreeze { get; private set; }
    public static SkillNameAtomAsset ModifierPoison { get; private set; }
    public static SkillNameAtomAsset ModifierBurn { get; private set; }
    public static SkillNameAtomAsset ModifierVolley { get; private set; }
    public static SkillNameAtomAsset ModifierKnockback { get; private set; }
    public static SkillNameAtomAsset ModifierHaste { get; private set; }
    public static SkillNameAtomAsset ModifierEmpower { get; private set; }
    public static SkillNameAtomAsset ModifierSlow { get; private set; }
    public static SkillNameAtomAsset ModifierProficiency { get; private set; }
    public static SkillNameAtomAsset ModifierSalvoCount { get; private set; }
    public static SkillNameAtomAsset ModifierBurstCount { get; private set; }

    protected override bool AutoRegisterAssets() => true;
    protected override string Prefix() => "Cultiway.SkillNameAtom";

    protected override void OnInit()
    {
        SetElement(ElementIron, ElementTag.Iron, ["金", "庚金", "锋"], [ElementTag.Iron, SeriesTag.Metal], ElementIndex.Iron);
        SetElement(ElementWood, ElementTag.Wood, ["木", "青木", "苍"], [ElementTag.Wood], ElementIndex.Wood);
        SetElement(ElementWater, ElementTag.Water, ["水", "寒", "玄水"], [ElementTag.Water], ElementIndex.Water);
        SetElement(ElementFire, ElementTag.Fire, ["火", "炎", "赤"], [ElementTag.Fire], ElementIndex.Fire);
        SetElement(ElementEarth, ElementTag.Earth, ["土", "岩", "岳"], [ElementTag.Earth], ElementIndex.Earth);
        SetElement(ElementNeg, ElementTag.Neg, ["阴", "幽", "玄"], [ElementTag.Neg], ElementIndex.Neg);
        SetElement(ElementPos, ElementTag.Pos, ["阳", "曜", "明"], [ElementTag.Pos], ElementIndex.Pos);
        SetElement(ElementEntropy, ElementTag.Entropy, ["混", "浊", "墟"], [ElementTag.Entropy], ElementIndex.Entropy);
        SetElement(ElementWind, ElementTag.Wind, ["风", "岚", "罡"], [ElementTag.Wind]);
        SetElement(ElementLightning, ElementTag.Lightning, ["雷", "霆", "电"], [ElementTag.Lightning]);
        SetElement(ElementGeneric, ElementTag.Generic, ["灵", "玄", "元"], []);

        SetForm(FormSlash, FormTag.Slash, ["刃", "斩", "锋"], [FormTag.Slash],
            ["{element}{form}", "{motion}{form}", "{element}{motion}", "{base}"], ["斩", "刃", "锋"]);
        SetForm(FormPierce, FormTag.Pierce, ["刺", "矢", "锥"], [FormTag.Pierce],
            ["{element}{form}", "{motion}{form}", "{element}{motion}", "{base}"], ["刺", "矢", "锥"]);
        SetForm(FormBall, FormTag.Ball, ["丸", "弹", "珠"], [FormTag.Ball],
            ["{element}{form}", "{element}{motion}", "{base}"], ["丸", "弹", "珠"]);
        SetForm(FormAoe, FormTag.Aoe, ["涡", "域", "阵"], [FormTag.Aoe],
            ["{element}{form}", "{motion}{form}", "{element}{motion}", "{base}"], ["阵", "域", "界"]);
        SetForm(FormFalling, FormTag.Falling, ["陨", "落"], [FormTag.Falling],
            ["{element}{form}", "{motion}{form}", "{element}{motion}", "{base}"], ["陨", "落"]);
        SetForm(FormSustain, FormTag.Sustain, ["流", "轮", "幕"], [FormTag.Sustain],
            ["{element}{form}", "{motion}{form}", "{element}{motion}", "{base}"], ["幕", "轮", "流"]);
        SetForm(FormSpell, FormTag.Spell, ["术", "法", "咒"], [],
            ["{element}{form}", "{element}{ending}", "{motion}{form}", "{base}"], ["术", "法", "诀", "咒"]);

        SetMotion(MotionFalling, MotionTag.Falling, ["落", "坠", "陨"],
            ["{element}{motion}", "{motion}{form}"], [MotionTag.Falling], ["FallingStrike"]);
        SetMotion(MotionGround, MotionTag.Ground, ["地", "岩"],
            ["{motion}{form}", "{element}{motion}"], [], ["GroundCrawl"]);
        SetMotion(MotionSnap, MotionTag.Snap, ["闪", "掣"],
            ["{element}{motion}", "{motion}{form}"], [], ["LightningSnap"]);
        SetMotion(MotionVortex, MotionTag.Vortex, ["旋", "涡"],
            ["{element}{form}", "{motion}{form}", "{element}{motion}"], [], ["SlowVortex", "SpiralHoming"]);
        SetMotion(MotionRain, MotionTag.Rain, ["雨", "霖"],
            ["{element}{motion}", "{motion}{form}"], [], ["RainFall"]);
        SetMotion(MotionReturn, MotionTag.Return, ["回", "返"],
            ["{motion}{form}", "{element}{motion}"], [], ["Boomerang"]);
        SetMotion(MotionZigzag, MotionTag.Zigzag, ["折", "掠"],
            ["{motion}{form}", "{element}{motion}"], [], ["Zigzag"]);
        SetMotion(MotionWave, MotionTag.Wave, ["波", "澜"],
            ["{element}{motion}", "{motion}{form}"], [], ["SineWave"]);

        SetModifier(ModifierDeathSentence, ModifierTag.DeathSentence, ["终焉", "诛", "灭"], 395,
            ["{modifier}{core}", "{modifier}{element}{form}", "{modifier}{base}", "{modifier}{ending}"],
            ["{modifier}{secondary}{core}", "{secondary}{modifier}{core}"], ["诏", "印", "劫"],
            SkillModifierRarity.Legendary);
        SetModifier(ModifierEternalCurse, ModifierTag.EternalCurse, ["永咒", "幽咒", "缚魂"], 392,
            ["{modifier}{form}", "{modifier}{element}{ending}", "{modifier}{base}", "{modifier}{core}"],
            ["{modifier}{secondary}{form}", "{secondary}{modifier}{core}"], ["咒", "禁", "契"],
            SkillModifierRarity.Legendary);
        SetModifier(ModifierReincarnationTrial, ModifierTag.ReincarnationTrial, ["轮回", "劫", "渡厄"], 388,
            ["{modifier}{core}", "{modifier}{element}{ending}", "{modifier}{base}", "{modifier}{ending}"],
            ["{modifier}{secondary}{core}", "{secondary}{modifier}{ending}"], ["劫", "印", "章"],
            SkillModifierRarity.Legendary);
        SetModifier(ModifierSilence, ModifierTag.Silence, ["封", "绝音", "禁言"], 384,
            ["{modifier}{core}", "{modifier}{form}", "{modifier}{element}{ending}", "{modifier}{base}"],
            ["{modifier}{secondary}{core}", "{secondary}{modifier}{form}"], ["禁", "咒", "印"],
            SkillModifierRarity.Legendary);

        SetModifier(ModifierBurnout, ModifierTag.Burnout, ["烬", "残焰", "劫灰"], 278,
            ["{modifier}{core}", "{modifier}{element}{form}", "{modifier}{base}", "{modifier}{ending}"],
            ["{modifier}{secondary}{core}", "{secondary}{modifier}{form}", "{modifier}{secondary}{ending}"],
            ["诀", "印", "术"], SkillModifierRarity.Epic, allowSecondary: true);
        SetModifier(ModifierChaos, ModifierTag.Chaos, ["乱", "混", "逆"], 274,
            ["{modifier}{core}", "{modifier}{element}{form}", "{modifier}{base}", "{modifier}{ending}"],
            ["{modifier}{secondary}{core}", "{secondary}{modifier}{core}", "{modifier}{secondary}{ending}"],
            ["诀", "法", "印"], SkillModifierRarity.Epic, allowSecondary: true);
        SetModifier(ModifierCombo, ModifierTag.Combo, ["连", "叠", "并"], 270,
            ["{modifier}{core}", "{modifier}{form}", "{modifier}{base}"],
            ["{modifier}{secondary}{core}", "{modifier}{secondary}{form}", "{secondary}{modifier}{core}"],
            ["诀", "式", "术"], SkillModifierRarity.Epic, allowSecondary: true);
        SetModifier(ModifierMercy, ModifierTag.Mercy, ["慈", "生", "渡"], 268,
            ["{modifier}{core}", "{modifier}{element}{ending}", "{modifier}{base}"],
            ["{modifier}{secondary}{core}", "{secondary}{modifier}{ending}"], ["印", "法", "诀"],
            SkillModifierRarity.Epic);
        SetModifier(ModifierSwap, ModifierTag.Swap, ["移", "易", "换"], 266,
            ["{modifier}{core}", "{modifier}{motion}{form}", "{modifier}{base}"],
            ["{modifier}{secondary}{core}", "{secondary}{modifier}{form}"], ["诀", "法", "印"],
            SkillModifierRarity.Epic);
        SetModifier(ModifierRandomAffix, ModifierTag.RandomAffix, ["变", "幻", "化"], 262,
            ["{modifier}{core}", "{modifier}{element}{ending}", "{modifier}{base}"],
            ["{modifier}{secondary}{core}", "{secondary}{modifier}{ending}"], ["法", "诀", "咒"],
            SkillModifierRarity.Epic);

        SetModifier(ModifierGravity, ModifierTag.Gravity, ["坠", "摄", "沉"], 158,
            ["{modifier}{core}", "{modifier}{motion}{form}", "{modifier}{base}"],
            ["{modifier}{secondary}{core}", "{secondary}{modifier}{form}"], ["阵", "印", "术"],
            SkillModifierRarity.Rare);
        SetModifier(ModifierArmorBreak, ModifierTag.ArmorBreak, ["破", "裂甲", "摧"], 155,
            ["{modifier}{core}", "{modifier}{form}", "{modifier}{element}{ending}"],
            ["{modifier}{secondary}{core}", "{secondary}{modifier}{form}"], ["诀", "式", "术"],
            SkillModifierRarity.Rare);
        SetModifier(ModifierHuge, ModifierTag.Huge, ["巨", "岳", "峙"], 152,
            ["{modifier}{form}", "{modifier}{core}", "{modifier}{element}{ending}"],
            ["{modifier}{secondary}{core}", "{secondary}{modifier}{form}"], ["阵", "印", "术"],
            SkillModifierRarity.Rare);
        SetModifier(ModifierDaze, ModifierTag.Daze, ["眩", "迷", "昏"], 150,
            ["{modifier}{core}", "{modifier}{form}", "{modifier}{base}"],
            ["{modifier}{secondary}{core}", "{secondary}{modifier}{form}"], ["咒", "印", "术"],
            SkillModifierRarity.Rare);
        SetModifier(ModifierWeaken, ModifierTag.Weaken, ["衰", "蚀", "损"], 145,
            ["{modifier}{core}", "{modifier}{form}", "{modifier}{base}"],
            ["{modifier}{secondary}{core}", "{secondary}{modifier}{form}"], ["咒", "印", "术"],
            SkillModifierRarity.Rare);

        SetModifier(ModifierExplosion, ModifierTag.Explosion, ["爆", "轰", "裂"], 48,
            ["{element}{modifier}", "{modifier}{form}", "{core}{modifier}", "{modifier}{ending}"],
            ["{modifier}{secondary}{core}", "{secondary}{modifier}{form}"], ["术", "诀"],
            SkillModifierRarity.Common);
        SetModifier(ModifierFreeze, ModifierTag.Freeze, ["霜", "凝", "寒"], 46,
            ["{modifier}{form}", "{modifier}{core}", "{element}{modifier}"],
            ["{modifier}{secondary}{core}", "{secondary}{modifier}{form}"], ["术", "咒"],
            SkillModifierRarity.Common);
        SetModifier(ModifierPoison, ModifierTag.Poison, ["毒", "蚀", "瘴"], 44,
            ["{modifier}{core}", "{modifier}{form}", "{element}{modifier}"],
            ["{modifier}{secondary}{core}", "{secondary}{modifier}{form}"], ["术", "咒"],
            SkillModifierRarity.Common);
        SetModifier(ModifierBurn, ModifierTag.Burn, ["灼", "焚", "炽"], 42,
            ["{modifier}{core}", "{modifier}{form}", "{element}{modifier}"],
            ["{modifier}{secondary}{core}", "{secondary}{modifier}{form}"], ["术", "诀"],
            SkillModifierRarity.Common);
        SetModifier(ModifierVolley, ModifierTag.Volley, ["雨", "散", "群"], 40,
            ["{element}{modifier}{form}", "{modifier}{core}", "{modifier}{form}"],
            ["{modifier}{secondary}{core}", "{secondary}{modifier}{form}"], ["阵", "式"],
            SkillModifierRarity.Common);
        SetModifier(ModifierKnockback, ModifierTag.Knockback, ["震", "荡", "摧"], 38,
            ["{modifier}{core}", "{modifier}{form}", "{element}{modifier}"],
            ["{modifier}{secondary}{core}", "{secondary}{modifier}{form}"], ["术", "印"],
            SkillModifierRarity.Common);
        SetModifier(ModifierHaste, ModifierTag.Haste, ["疾", "迅", "驰"], 36,
            ["{modifier}{core}", "{modifier}{motion}{form}", "{element}{modifier}"],
            ["{modifier}{secondary}{core}", "{secondary}{modifier}{form}"], ["诀", "式"],
            SkillModifierRarity.Common);
        SetModifier(ModifierEmpower, ModifierTag.Empower, ["威", "盛", "壮"], 34,
            ["{modifier}{core}", "{modifier}{form}", "{element}{modifier}"],
            ["{modifier}{secondary}{core}", "{secondary}{modifier}{form}"], ["印", "术"],
            SkillModifierRarity.Common);
        SetModifier(ModifierSlow, ModifierTag.Slow, ["滞", "缚", "迟"], 32,
            ["{modifier}{core}", "{modifier}{form}", "{element}{modifier}"],
            ["{modifier}{secondary}{core}", "{secondary}{modifier}{form}"], ["咒", "印"],
            SkillModifierRarity.Common);
        SetModifier(ModifierProficiency, ModifierTag.Proficiency, ["御", "驭", "熟"], 24,
            ["{modifier}{core}", "{modifier}{form}", "{element}{modifier}"],
            ["{modifier}{secondary}{core}", "{secondary}{modifier}{form}"], ["诀", "式"],
            SkillModifierRarity.Common);
        SetModifier(ModifierSalvoCount, ModifierTag.SalvoCount, ["连", "复", "重"], 22,
            ["{modifier}{core}", "{modifier}{form}", "{element}{modifier}"],
            ["{modifier}{secondary}{core}", "{secondary}{modifier}{form}"], ["式", "诀"],
            SkillModifierRarity.Common);
        SetModifier(ModifierBurstCount, ModifierTag.BurstCount, ["散", "迸", "裂"], 20,
            ["{modifier}{core}", "{modifier}{form}", "{element}{modifier}"],
            ["{modifier}{secondary}{core}", "{secondary}{modifier}{form}"], ["式", "术"],
            SkillModifierRarity.Common);
    }

    private static void SetElement(SkillNameAtomAsset atom, string tag, string[] stems, string[] seriesTags,
        int elementIndex = -1)
    {
        atom.tag = tag;
        atom.category = SkillNameAtomCategory.Element;
        atom.name_stems = stems;
        atom.series_tags = seriesTags;
        atom.element_index = elementIndex;
        atom.priority = 100;
        atom.ScoreContext = context => context.ElementTag == tag ? 10f : 0f;
    }

    private static void SetForm(SkillNameAtomAsset atom, string tag, string[] stems, string[] seriesTags,
        string[] corePatterns, string[] endingStems)
    {
        atom.tag = tag;
        atom.category = SkillNameAtomCategory.Form;
        atom.name_stems = stems;
        atom.series_tags = seriesTags;
        atom.core_patterns = corePatterns;
        atom.ending_stems = endingStems;
        atom.priority = 100;
        atom.ScoreContext = context => context.FormTag == tag ? 10f : 0f;
    }

    private static void SetMotion(SkillNameAtomAsset atom, string tag, string[] stems, string[] corePatterns,
        string[] seriesTags, string[] trajectorySuffixes)
    {
        atom.tag = tag;
        atom.category = SkillNameAtomCategory.Motion;
        atom.name_stems = stems;
        atom.series_tags = seriesTags;
        atom.trajectory_suffixes = trajectorySuffixes;
        atom.core_pattern = corePatterns[0];
        atom.core_patterns = corePatterns;
        atom.priority = 100;
        atom.ScoreContext = context => context.MotionTag == tag ? 10f : 0f;
    }

    private static void SetModifier(SkillNameAtomAsset atom, string kind, string[] stems, int priority,
        string[] patterns, string[] secondaryPatterns, string[] endingStems, SkillModifierRarity rarity,
        bool allowSecondary = false)
    {
        atom.tag = kind;
        atom.category = SkillNameAtomCategory.Modifier;
        atom.name_stems = stems;
        atom.pattern = patterns[0];
        atom.modifier_patterns = patterns;
        atom.secondary_patterns = secondaryPatterns;
        atom.ending_stems = endingStems;
        atom.priority = priority;
        atom.allow_secondary = allowSecondary;
        atom.min_rarity = rarity;
        atom.ScoreModifier = modifier =>
        {
            if (modifier.Kind != kind) return 0f;
            return priority + (int)modifier.Rarity * 10 + modifier.ValueTier;
        };
    }

}
