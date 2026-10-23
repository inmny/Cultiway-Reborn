using System;
using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Content.YaoBeasts;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>
/// Content 提供的施法资源通道。Core 只消费注册后的抽象资源接口。
/// </summary>
public sealed class SkillCastResources : ExtendLibrary<SkillCastResourceAsset, SkillCastResources>
{
    public static SkillCastResourceAsset Wakan { get; private set; }
    public static SkillCastResourceAsset Mana { get; private set; }

    /// <summary>首轮构造使用的原版人物法力，保持正式旧资料不要求魔法体系的规则。</summary>
    public static SkillCastResourceAsset NativeMana { get; private set; }

    public static SkillCastResourceAsset Vigor { get; private set; }

    /// <summary>原版人物已有的体力，不要求额外修炼体系。</summary>
    public static SkillCastResourceAsset Stamina { get; private set; }

    /// <summary>妖兽的妖力施法资源通道。</summary>
    public static SkillCastResourceAsset YaoPower { get; private set; }

    protected override bool AutoRegisterAssets() => true;
    protected override string Prefix() => "Cultiway.SkillCastResource";

    protected override void OnInit()
    {
        Wakan.Configure(HasWakan, ReadWakan, WriteWakan, QuoteWakan)
            .ConfigureSpending((caster, amount) =>
                WakanResourceService.Spend(caster, ref caster.GetCultisys<Xian>(), (float)amount))
            .ConfigureRecovery(caster => caster.Base.stats[BaseStatses.MaxWakan.id])
            .ConfigureEditor("Cultiway.SkillCastResource.Wakan.Description", "cultiway/icons/iconWakan", 0)
            .ConfigureItemLevelDisplay(FormatWakanItemLevel);
        Mana.Configure(HasMana, ReadMana, WriteMana, QuoteMana)
            .ConfigureSpending(SpendMana)
            .ConfigureRecovery(caster => caster.Base.getMaxMana(), true)
            .ConfigureEditor("Cultiway.SkillCastResource.Mana.Description", "ui/icons/iconMana", 10)
            .ConfigureItemLevelDisplay(FormatManaItemLevel);
        Vigor.Configure(HasVigor, ReadVigor, WriteVigor, QuoteVigor)
            .ConfigureSpending((caster, amount) => caster.GetCultisys<Knight>().vigor -= (float)amount)
            .ConfigureRecovery(caster => caster.Base.stats[BaseStatses.MaxVigor.id])
            .ConfigureEditor("Cultiway.SkillCastResource.Vigor.Description", "cultiway/icons/iconKnight", 20);
        NativeMana.Configure(caster => true, ReadMana, WriteMana, QuoteMana)
            .ConfigureSpending(SpendMana)
            .ConfigureRecovery(caster => caster.Base.getMaxMana(), true);
        Stamina.Configure(caster => true, caster => caster.Base.data.stamina,
                (caster, amount) => caster.Base.data.stamina = (int)Math.Max(0d, amount),
                (caster, demand) => Mathf.Ceil(Mathf.Max(0f, demand)))
            .ConfigureSpending((caster, amount) => caster.Base.data.stamina -= (int)amount)
            .ConfigureRecovery(caster => caster.Base.getMaxStamina(), true);
        YaoPower.Configure(HasYaoPower, ReadYaoPower, WriteYaoPower, QuoteYaoPower)
            .ConfigureSpending((caster, amount) =>
                YaoResourceService.Spend(caster, ref caster.GetCultisys<Yao>(), (float)amount))
            .ConfigureRecovery(caster => caster.Base.stats[BaseStatses.MaxYaoPower.id])
            .ConfigureEditor("Cultiway.SkillCastResource.YaoPower.Description", "cultiway/icons/iconCultivation", 30);
    }

    private static bool HasWakan(ActorExtend caster)
    {
        return caster.HasCultisys<Xian>();
    }

    private static double ReadWakan(ActorExtend caster)
    {
        return caster.GetCultisys<Xian>().wakan;
    }

    private static void WriteWakan(ActorExtend caster, double amount)
    {
        ref Xian xian = ref caster.GetCultisys<Xian>();
        WakanResourceService.Set(caster, ref xian, (float)amount);
    }

    private static float QuoteWakan(ActorExtend caster, float demand)
    {
        return demand;
    }

    private static bool HasMana(ActorExtend caster)
    {
        return caster.HasCultisys<Magic>();
    }

    private static double ReadMana(ActorExtend caster)
    {
        return caster.Base.getMana();
    }

    private static void WriteMana(ActorExtend caster, double amount)
    {
        caster.Base.setMana((int)Math.Max(0d, amount));
    }

    private static float QuoteMana(ActorExtend caster, float demand)
    {
        return Mathf.Ceil(Mathf.Max(0f, demand));
    }

    /// <summary>原版整数法力精确扣费，不通过单精度或顺带收回已有超容量存量。</summary>
    /// <param name="caster">真实支付者。</param><param name="amount">已确认的整数费用。</param>
    private static void SpendMana(ActorExtend caster, double amount)
    {
        caster.Base.setMana(caster.Base.getMana() - (int)amount, false);
    }

    private static bool HasVigor(ActorExtend caster)
    {
        return caster.HasCultisys<Knight>();
    }

    private static double ReadVigor(ActorExtend caster)
    {
        return caster.GetCultisys<Knight>().vigor;
    }

    private static void WriteVigor(ActorExtend caster, double amount)
    {
        ref Knight knight = ref caster.GetCultisys<Knight>();
        knight.vigor = Mathf.Clamp((float)amount, 0f, caster.Base.stats[BaseStatses.MaxVigor.id]);
    }

    private static float QuoteVigor(ActorExtend caster, float demand)
    {
        return Mathf.Max(0f, demand);
    }

    private static bool HasYaoPower(ActorExtend caster)
    {
        return caster.HasCultisys<Yao>();
    }

    private static double ReadYaoPower(ActorExtend caster)
    {
        return caster.GetCultisys<Yao>().yao_power;
    }

    private static void WriteYaoPower(ActorExtend caster, double amount)
    {
        ref Yao yao = ref caster.GetCultisys<Yao>();
        YaoResourceService.Set(caster, ref yao, (float)amount);
    }

    private static float QuoteYaoPower(ActorExtend caster, float demand)
    {
        return Mathf.Max(0f, demand);
    }

    private static string FormatWakanItemLevel(ItemLevel itemLevel)
    {
        return itemLevel.GetName();
    }

    private static string FormatManaItemLevel(ItemLevel itemLevel)
    {
        var value = (int)itemLevel;
        var ringName = $"Cultiway.ERStyle.Magic.Stage.{value / 3}".Localize();
        var rankName = $"Cultiway.ERStyle.Magic.Level.{value % 3}".Localize();
        return string.Format("Cultiway.SkillCastResource.Mana.ItemLevel".Localize(), ringName, rankName);
    }
}