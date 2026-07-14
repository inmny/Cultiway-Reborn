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
    public static ActorAsset Gui { get; private set; }
    [CloneSource(ActorAssetLibrary.TEMPLATE_BOAT_TRADING), AssetId("boat_trading_Cultiway.Gui")]
    public static ActorAsset GuiTradingBoat {get; private set;}
    [CloneSource(ActorAssetLibrary.TEMPLATE_BOAT_TRANSPORT), AssetId("boat_transport_Cultiway.Gui")]
    public static ActorAsset GuiTransportBoat {get; private set;}
    private void SetupGui()
    {
        Gui.AddCultureTrait(CultureTraits.CultureSkin)
                    .RemoveCultureTrait(S_CultureTrait.city_layout_the_grand_arrangement)
                    .AddCultureTrait(S_CultureTrait.city_layout_pebbles);
        Gui.build_order_template_id = BuildingOrders.Classic.id;
        Gui.architecture_id = Architectures.Gui.id;
        Gui.kingdom_id_wild = KingdomAssets.NoMadsGui.id;
        Gui.kingdom_id_civilization = KingdomAssets.Gui.id;
        Gui.name_locale = Gui.id;
        Gui.name_template_sets = WorldboxGame.NameSets.EasternHumanTemplateSets;
        Gui.power_id = null;
        Gui.phenotypes_dict = null;
        Gui.phenotypes_list = null;
        Gui.AddPhenotype(S_Phenotype.skin_light);
        Gui.AddPhenotype(S_Phenotype.skin_yellow);

        Gui.icon = "../../cultiway/icons/races/iconGui";
        Gui.color_hex = "#9088C4";
        Gui.skin_citizen_male = Directory
            .GetDirectories($"{ModClass.I.GetDeclaration().FolderPath}/GameResources/actors/species/civs/{Gui.id}")
            .Select(x => new DirectoryInfo(x).Name)
            .Where(x => x.ToLower().StartsWith("male"))
            .ToArray();
        Gui.skin_citizen_female = Directory
            .GetDirectories($"{ModClass.I.GetDeclaration().FolderPath}/GameResources/actors/species/civs/{Gui.id}")
            .Select(x => new DirectoryInfo(x).Name)
            .Where(x => x.ToLower().StartsWith("female"))
            .ToArray();
        Gui.skin_warrior = Directory
            .GetDirectories($"{ModClass.I.GetDeclaration().FolderPath}/GameResources/actors/species/civs/{Gui.id}")
            .Select(x => new DirectoryInfo(x).Name)
            .Where(x => x.ToLower().StartsWith("warrior"))
            .ToArray();
        // 鬼族仅有一套 king/leader 皮肤（无 king_/leader_ 变体目录），保留 skin_king/skin_leader 为 null，
        // 由原版默认路径 base+"king"/base+"leader" 直接命中对应目录（PatchAboutCultureSkin 在 null 时回退默认路径）。
        Gui.texture_id = Gui.id;
        Gui.texture_asset = new ActorTextureSubAsset($"actors/species/civs/{Gui.id}/", true)
        {
            render_heads_for_children = false
        };
        Gui.texture_asset.GetAnyExtend<ActorTextureSubAsset, ActorTextureSubAssetExtend>().disable_heads = true;
    }
}
