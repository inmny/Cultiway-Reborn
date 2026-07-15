using System;
using Cultiway.Abstract;
using Cultiway.Debug;
using NeoModLoader;
using strings;

namespace Cultiway.Content;

public partial class KingdomAssets : ExtendLibrary<KingdomAsset, KingdomAssets>
{
    protected override bool AutoRegisterAssets() => true;
    /// <summary>
    /// 亡灵
    /// </summary>
    [GetOnly("undead")]
    public static KingdomAsset Undead { get; private set; }
    /// <summary>
    /// 冥族
    /// </summary>
    public static KingdomAsset Ming { get; private set; }
    [CloneSource("$TEMPLATE_NOMAD$")]
    public static KingdomAsset NoMadsMing { get; private set; }
    /// <summary>
    /// 东方人族
    /// </summary>
    [CloneSource("$TEMPLATE_NOMAD$")]
    public static KingdomAsset EasternHuman { get; private set; }
    [CloneSource("$TEMPLATE_NOMAD$")]
    public static KingdomAsset NoMadsEasternHuman { get; private set; }
    /// <summary>
    /// 鬼族
    /// </summary>
    [CloneSource("$TEMPLATE_NOMAD$")]
    public static KingdomAsset Gui { get; private set; }
    [CloneSource("$TEMPLATE_NOMAD$")]
    public static KingdomAsset NoMadsGui { get; private set; }
    public static KingdomAsset SectBuildings { get; private set; }
    protected override void OnInit()
    {
        Ming.clearKingdomColor();
        Ming.civ = true;
        Ming.mobs = false;
        Ming.addTag(nameof(Ming));
        Ming.addTag(nameof(Undead));
        Ming.addFriendlyTag(nameof(Ming));
        Ming.addFriendlyTag(nameof(Undead));
        
        NoMadsMing.default_kingdom_color = new("#636B77");
        NoMadsMing.addTag(nameof(Ming));
        NoMadsMing.addTag(nameof(Undead));
        NoMadsMing.addFriendlyTag(nameof(Ming));
        NoMadsMing.addFriendlyTag(nameof(Undead));


        EasternHuman.clearKingdomColor();
        EasternHuman.civ = true;
        EasternHuman.mobs = false;
        EasternHuman.group_main = true;
        EasternHuman.setIcon("cultiway/icons/races/iconEasternHuman");
        EasternHuman.addTag(nameof(EasternHuman));
        EasternHuman.addTag(SK.sliceable);
        EasternHuman.addFriendlyTag(nameof(EasternHuman));


        NoMadsEasternHuman.default_kingdom_color = new("#5AAFE5");
        NoMadsEasternHuman.group_main = true;
        NoMadsEasternHuman.setIcon("cultiway/icons/races/iconEasternHuman");
        NoMadsEasternHuman.addTag(nameof(EasternHuman));
        NoMadsEasternHuman.addTag(SK.sliceable);
        NoMadsEasternHuman.addFriendlyTag(nameof(EasternHuman));

        Gui.clearKingdomColor();
        Gui.civ = true;
        Gui.mobs = false;
        Gui.group_main = true;
        Gui.setIcon("cultiway/icons/races/iconGui");
        Gui.addTag(nameof(Gui));
        Gui.addTag(SK.sliceable);
        Gui.addFriendlyTag(nameof(Gui));

        NoMadsGui.default_kingdom_color = new("#9088C4");
        NoMadsGui.group_main = true;
        NoMadsGui.setIcon("cultiway/icons/races/iconGui");
        NoMadsGui.addTag(nameof(Gui));
        NoMadsGui.addTag(SK.sliceable);
        NoMadsGui.addFriendlyTag(nameof(Gui));

        SectBuildings.default_kingdom_color = new("#D6B44C");
        SectBuildings.civ = false;
        SectBuildings.mobs = false;
        SectBuildings.nomads = false;
        SectBuildings.nature = true;
        SectBuildings.concept = true;
        SectBuildings.count_as_danger = false;
        SectBuildings.setIcon("cultiway/icons/iconSect");
        SectBuildings.addTag(nameof(SectBuildings));
        SectBuildings.addFriendlyTag(nameof(SectBuildings));

        Undead.addTag(nameof(Undead));
        Undead.addFriendlyTag(nameof(Undead));

        SetupFantasyCreatureKingdoms();
    }
    /// <summary>
    /// 为拥有所有<paramref name="pIfHasTags"/>标签的阵营对<paramref name="pTag"/>标签友好
    /// </summary>
    internal void AllFriendWith(string pTag, params string[] pIfHasTags)
    {
        // 等新版本NML出来后再换成ForEach
        foreach (var camp in cached_library.list)
        {
            if (camp.list_tags.IsSupersetOf(pIfHasTags))
            {
                camp.addFriendlyTag(pTag);
            }
        }
    }

    /// <summary>
    /// 为拥有所有<paramref name="pIfHasTags"/>标签的阵营对<paramref name="pTag"/>标签敌对
    /// </summary>
    internal void AllEnemyWith(string pTag, params string[] pIfHasTags)
    {
        // 等新版本NML出来后再换成ForEach
        foreach (var camp in cached_library.list)
        {
            if (camp.list_tags.IsSupersetOf(pIfHasTags))
            {
                camp.addEnemyTag(pTag);
            }
        }
    }

    protected override void PostInit(KingdomAsset asset)
    {
        Try.Start(() =>
        {
            if (!asset.civ)
                World.world.kingdoms_wild.newWildKingdom(asset);
            ModClass.LogInfo($"Added kingdom {asset.id} to wild kingdoms");
        });
    }
}
