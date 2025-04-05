using Cultiway.Abstract;
using Cultiway.Debug;
using NeoModLoader;

namespace Cultiway.Content;

public class KingdomAssets : ExtendLibrary<KingdomAsset, KingdomAssets>
{
    [GetOnly("undead")]
    public static KingdomAsset Undead { get; private set; }
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