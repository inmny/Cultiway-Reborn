using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>
/// Content 提供的施法资源通道。Core 只消费注册后的抽象资源接口。
/// </summary>
public sealed class SkillCastResources : ExtendLibrary<SkillCastResourceAsset, SkillCastResources>
{
    public static SkillCastResourceAsset Wakan { get; private set; }
    public static SkillCastResourceAsset Mana { get; private set; }

    protected override bool AutoRegisterAssets() => true;
    protected override string Prefix() => "Cultiway.SkillCastResource";

    protected override void OnInit()
    {
        Wakan.Configure(HasWakan, ReadWakan, WriteWakan, QuoteWakan)
            .ConfigureEditor("Cultiway.SkillCastResource.Wakan.Description", "cultiway/icons/iconWakan", 0)
            .ConfigureItemLevelDisplay(FormatWakanItemLevel);
        Mana.Configure(HasMana, ReadMana, WriteMana, QuoteMana)
            .ConfigureEditor("Cultiway.SkillCastResource.Mana.Description", "ui/icons/iconMana", 10)
            .ConfigureItemLevelDisplay(FormatManaItemLevel);
    }

    private static bool HasWakan(ActorExtend caster)
    {
        return caster.HasCultisys<Xian>();
    }

    private static float ReadWakan(ActorExtend caster)
    {
        return caster.GetCultisys<Xian>().wakan;
    }

    private static void WriteWakan(ActorExtend caster, float amount)
    {
        caster.GetCultisys<Xian>().wakan = Mathf.Max(0f, amount);
    }

    private static float QuoteWakan(ActorExtend caster, Entity skill, float demand)
    {
        return demand;
    }

    private static bool HasMana(ActorExtend caster)
    {
        return caster.HasCultisys<Magic>();
    }

    private static float ReadMana(ActorExtend caster)
    {
        return caster.Base.getMana();
    }

    private static void WriteMana(ActorExtend caster, float amount)
    {
        caster.Base.setMana(Mathf.FloorToInt(Mathf.Max(0f, amount)));
    }

    private static float QuoteMana(ActorExtend caster, Entity skill, float demand)
    {
        return Mathf.Ceil(Mathf.Max(0f, demand));
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
