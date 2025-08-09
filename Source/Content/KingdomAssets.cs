using System;
using Cultiway.Abstract;
using Cultiway.Debug;
using NeoModLoader;

namespace Cultiway.Content;

public partial class KingdomAssets : ExtendLibrary<KingdomAsset, KingdomAssets>
{
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
    protected override void OnInit()
    {
        RegisterAssets();

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