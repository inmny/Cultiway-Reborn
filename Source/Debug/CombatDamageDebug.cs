using System.Text;
using Cultiway.Const;
using Cultiway.Core;
using Cultiway.Core.Logging;
using Cultiway.Utils.Extension;
using strings;

namespace Cultiway.Debug;

public static class CombatDamageDebug
{
    private static readonly string[] ElementDebugNames =
    [
        "金",
        "木",
        "水",
        "火",
        "土",
        "阴",
        "阳",
        "熵"
    ];

    public static bool Enabled { get; private set; }
    public static bool FavoriteUnitsOnly { get; private set; } = true;

    public static void EnableForFavoriteUnits()
    {
        SetEnabled(true, true);
    }

    public static void SetEnabled(bool enabled, bool favoriteUnitsOnly = true)
    {
        Enabled = enabled;
        FavoriteUnitsOnly = favoriteUnitsOnly;
        var scope = !Enabled
            ? "不会输出伤害结算"
            : (FavoriteUnitsOnly ? "仅输出最爱的单位相关伤害结算" : "输出全部伤害结算");
        ModClass.LogInfo($"[CombatDamageDebug] {(Enabled ? "已启用" : "已禁用")}：{scope}。");
    }

    public static void Disable()
    {
        SetEnabled(false);
    }

    public static bool ShouldLog(ActorExtend target, BaseSimObject attacker)
    {
        if (!Enabled || !CultiLog.Combat.DamageResolvedEnabled || target?.Base == null) return false;
        if (!FavoriteUnitsOnly) return true;
        if (target.Base.isFavorite()) return true;
        return attacker != null && !attacker.isRekt() && attacker.isActor() && attacker.a.isFavorite();
    }

    public static CombatDamageDebugRecord StartRecord(ActorExtend target, BaseSimObject attacker, float inputDamage,
        ref ElementComposition damageComposition, AttackType attackType, bool ignoreDamageReduction)
    {
        var record = new CombatDamageDebugRecord
        {
            Target = Describe(target.Base),
            Attacker = Describe(attacker),
            TargetId = target.Base.data.id,
            AttackerId = ActorId(attacker),
            TargetFavorite = target.Base.isFavorite(),
            AttackerFavorite = attacker != null && !attacker.isRekt() && attacker.isActor() && attacker.a.isFavorite(),
            AttackType = attackType.ToString(),
            IgnoreDamageReduction = ignoreDamageReduction,
            InputDamage = inputDamage,
            DamageAfterPreActions = inputDamage,
            DamageBeforePowerSuppression = inputDamage,
            DamageAfterPowerSuppression = inputDamage,
            DamageBeforeResistance = inputDamage,
            DamageAfterResistance = inputDamage,
            DamageBeforeMinimum = inputDamage,
            DamageAfterMinimum = inputDamage,
            DamageBeforeVanillaSpecial = inputDamage,
            VanillaSpecialFinalDamage = inputDamage,
            FinalDamage = inputDamage
        };

        CaptureCompositionAndDefense(record, target, ref damageComposition);
        return record;
    }

    public static void RefreshComposition(CombatDamageDebugRecord record, ActorExtend target,
        ref ElementComposition damageComposition)
    {
        if (record == null) return;
        CaptureCompositionAndDefense(record, target, ref damageComposition);
    }

    public static void Log(CombatDamageDebugRecord record)
    {
        if (record == null) return;

        var sb = new StringBuilder();
        sb.AppendLine("[CombatDamageDebug]");
        sb.AppendLine(
            $"目标={record.Target} favorite={record.TargetFavorite} 攻击者={record.Attacker} favorite={record.AttackerFavorite} 类型={record.AttackType}");
        sb.AppendLine(
            $"伤害 input={Fmt(record.InputDamage)} preAction后={Fmt(record.DamageAfterPreActions)} 境界压制前={Fmt(record.DamageBeforePowerSuppression)} 境界压制后={Fmt(record.DamageAfterPowerSuppression)} 抗性后={Fmt(record.DamageAfterResistance)} 最低伤害后={Fmt(record.DamageAfterMinimum)} 原版特殊结算后={Fmt(record.FinalDamage)}");
        sb.AppendLine(
            $"境界 target={Fmt(record.TargetPowerLevel)} attacker={Fmt(record.AttackerPowerLevel)} gap(target-attacker)={Fmt(record.PowerLevelGap)} powerBase={DamageCalcHyperParameters.PowerBase}");
        sb.AppendLine(
            $"减伤 ignore={record.IgnoreDamageReduction} armorDecay={DamageCalcHyperParameters.ArmorEffectDecay} masterDecay={DamageCalcHyperParameters.MasterEffectDecay} 通用护甲={Fmt(record.GeneralArmorStat)} 通用减免={Pct(record.GeneralArmorReduction)} 五行通过={Pct(record.FiveElementPassRatio)} 阴阳通过={Pct(record.PolarityPassRatio)} 阴阳附加熵通过={Pct(record.PolarityEntropyPassRatio)} 熵通过={Pct(record.EntropyPassRatio)} 总通过={Pct(record.TotalPassRatio)}");
        sb.AppendLine(
            $"最低伤害 eligible={record.MinimumDamageEligible} 抗性后触发={record.MinimumDamageAppliedBeforeIneffective} 原版特殊后触发={record.MinimumDamageAppliedAfterVanillaSpecial}");
        sb.AppendLine(
            $"无效命中 chance={Pct(record.IneffectiveHitChance)} resolved={record.IneffectiveHit}");
        sb.AppendLine("元素组分/防御:");
        for (var i = 0; i < 8; i++)
        {
            sb.AppendLine(
                $"  {ElementDebugNames[i]} weight={Fmt(record.ElementWeights[i])} armor={Fmt(record.ElementArmorStats[i])} master={Fmt(record.ElementMasterStats[i])} combinedReduction={Pct(record.ElementReductionRatios[i])} pass={Pct(1f - record.ElementReductionRatios[i])}");
        }

        CultiLog.Combat.DamageResolved(sb.ToString(), record.TargetId, record.AttackerId);
    }

    private static void CaptureCompositionAndDefense(CombatDamageDebugRecord record, ActorExtend target,
        ref ElementComposition damageComposition)
    {
        for (var i = 0; i < 8; i++)
        {
            record.ElementWeights[i] = damageComposition[i];
            record.ElementArmorStats[i] = target.Base.stats[WorldboxGame.BaseStats.ArmorStats[i]];
            record.ElementMasterStats[i] = target.Base.stats[WorldboxGame.BaseStats.MasterStats[i]];
            record.ElementReductionRatios[i] = target.s_armor[i];
        }

        record.GeneralArmorStat = target.Base.stats[S.armor];
        record.GeneralArmorReduction = target.s_armor[ElementIndex.Entropy + 1];
    }

    private static string Describe(BaseSimObject obj)
    {
        if (obj == null) return "null";
        if (obj.isRekt()) return "rekt";
        if (obj.isActor()) return $"{obj.a.getName()}#{obj.a.data?.id}";
        return obj.GetType().Name;
    }

    private static long ActorId(BaseSimObject obj)
    {
        if (obj == null || obj.isRekt() || !obj.isActor()) return -1;
        return obj.a.data?.id ?? -1;
    }

    private static string Fmt(float value)
    {
        return value.ToString("0.###");
    }

    private static string Pct(float value)
    {
        return $"{value * 100f:0.##}%";
    }
}

public class CombatDamageDebugRecord
{
    public string Target;
    public string Attacker;
    public long TargetId;
    public long AttackerId;
    public bool TargetFavorite;
    public bool AttackerFavorite;
    public string AttackType;
    public bool IgnoreDamageReduction;

    public float InputDamage;
    public float DamageAfterPreActions;
    public float DamageBeforePowerSuppression;
    public float DamageAfterPowerSuppression;
    public float DamageBeforeResistance;
    public float DamageAfterResistance;
    public float DamageBeforeMinimum;
    public float DamageAfterMinimum;
    public float DamageBeforeVanillaSpecial;
    public float VanillaSpecialFinalDamage;
    public float FinalDamage;

    public float TargetPowerLevel;
    public float AttackerPowerLevel;
    public float PowerLevelGap;

    public float GeneralArmorStat;
    public float GeneralArmorReduction;
    public float FiveElementPassRatio = 1f;
    public float PolarityPassRatio = 1f;
    public float PolarityEntropyPassRatio = 1f;
    public float EntropyPassRatio = 1f;
    public float TotalPassRatio = 1f;

    public bool MinimumDamageEligible;
    public bool MinimumDamageAppliedBeforeIneffective;
    public bool MinimumDamageAppliedAfterVanillaSpecial;
    public float IneffectiveHitChance;
    public bool IneffectiveHit;

    public readonly float[] ElementWeights = new float[8];
    public readonly float[] ElementArmorStats = new float[8];
    public readonly float[] ElementMasterStats = new float[8];
    public readonly float[] ElementReductionRatios = new float[8];
}
