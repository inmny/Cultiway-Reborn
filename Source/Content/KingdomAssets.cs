using Cultiway.Abstract;
using NeoModLoader;

namespace Cultiway.Content;

public class KingdomAssets : ExtendLibrary<KingdomAsset, KingdomAssets>
{
    [GetOnly("undead")]
    public static KingdomAsset Undead { get; private set; }
    public static KingdomAsset Ming { get; private set; }
    public static KingdomAsset NoMadsMing { get; private set; }
    protected override void OnInit()
    {
        RegisterAssets();
        
        Ming.addTag(nameof(Ming));
        Ming.addTag(nameof(Undead));
        Ming.addFriendlyTag(nameof(Ming));
        Ming.addFriendlyTag(nameof(Undead));
        
        NoMadsMing.addTag(nameof(Ming));
        NoMadsMing.addTag(nameof(Undead));
        NoMadsMing.addFriendlyTag(nameof(Ming));
        NoMadsMing.addFriendlyTag(nameof(Undead));
        
        Undead.addTag(nameof(Undead));
        Undead.addFriendlyTag(nameof(Undead));
    }

    protected override KingdomAsset Add(KingdomAsset asset)
    {
        World.world.kingdoms.newHiddenKingdom(asset);
        return base.Add(asset);
    }
}