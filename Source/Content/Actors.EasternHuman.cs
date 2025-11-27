using System.IO;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Content.Extensions;
using Cultiway.Core;
using Cultiway.Utils.Extension;
using strings;

namespace Cultiway.Content;

public partial class Actors
{
    [CloneSource(SA.human)]
    public static ActorAsset EasternHuman { get; private set; }
    [CloneSource(ActorAssetLibrary.TEMPLATE_BOAT_TRADING), AssetId("boat_trading_Cultiway.EasternHuman")]
    public static ActorAsset EasternHumanTradingBoat {get; private set;}
    [CloneSource(ActorAssetLibrary.TEMPLATE_BOAT_TRANSPORT), AssetId("boat_transport_Cultiway.EasternHuman")]
    public static ActorAsset EasternHumanTransportBoat {get; private set;}
    private void SetupEasternHuman() 
    { 
        EasternHuman.AddCultureTrait(CultureTraits.CultureSkin)
                    //.AddCultureTrait(S_CultureTrait.city_layout_stone_garden)
                    //.AddCultureTrait(S_CultureTrait.city_layout_the_grand_arrangement)
                    .AddCultureTrait(S_CultureTrait.city_layout_pebbles);
        EasternHuman.build_order_template_id = BuildingOrders.Classic.id;
        EasternHuman.architecture_id = Architectures.EasternHuman.id;
        EasternHuman.kingdom_id_wild = KingdomAssets.NoMadsEasternHuman.id;
        EasternHuman.kingdom_id_civilization = KingdomAssets.EasternHuman.id;
        EasternHuman.name_locale = EasternHuman.id;
        EasternHuman.power_id = null;

        EasternHuman.icon = "../../cultiway/icons/races/iconEasternHuman";
        EasternHuman.color_hex = "#5AAFE5";
        EasternHuman.skin_citizen_male = Directory
            .GetDirectories($"{ModClass.I.GetDeclaration().FolderPath}/GameResources/actors/species/civs/{EasternHuman.id}")
            .Select(x => new DirectoryInfo(x).Name)
            .Where(x => x.ToLower().StartsWith("male"))
            .ToArray();
        EasternHuman.skin_citizen_female = Directory
            .GetDirectories($"{ModClass.I.GetDeclaration().FolderPath}/GameResources/actors/species/civs/{EasternHuman.id}")
            .Select(x => new DirectoryInfo(x).Name)
            .Where(x => x.ToLower().StartsWith("female"))
            .ToArray();
        EasternHuman.skin_warrior = Directory
            .GetDirectories($"{ModClass.I.GetDeclaration().FolderPath}/GameResources/actors/species/civs/{EasternHuman.id}")
            .Select(x => new DirectoryInfo(x).Name)
            .Where(x => x.ToLower().StartsWith("warrior"))
            .ToArray();
        EasternHuman.GetExtend<ActorAssetExtend>().skin_king = Directory
            .GetDirectories($"{ModClass.I.GetDeclaration().FolderPath}/GameResources/actors/species/civs/{EasternHuman.id}")
            .Select(x => new DirectoryInfo(x).Name)
            .Where(x => x.ToLower().StartsWith("king_"))
            .ToArray();
        EasternHuman.GetExtend<ActorAssetExtend>().skin_leader = Directory
            .GetDirectories($"{ModClass.I.GetDeclaration().FolderPath}/GameResources/actors/species/civs/{EasternHuman.id}")
            .Select(x => new DirectoryInfo(x).Name)
            .Where(x => x.ToLower().StartsWith("leader_"))
            .ToArray();
        EasternHuman.texture_id = EasternHuman.id;
        EasternHuman.texture_asset = new ActorTextureSubAsset($"actors/species/civs/{EasternHuman.id}/", true)
        {
            render_heads_for_children = false
        };
        EasternHuman.texture_asset.GetAnyExtend<ActorTextureSubAsset, ActorTextureSubAssetExtend>().disable_heads = true;
    }
}
