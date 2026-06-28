using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Core;
using Cultiway.Core.Libraries;
using Cultiway.UI.Components;
using Cultiway.UI.Prefab;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using NeoModLoader.General;
using strings;

namespace Cultiway;

public partial class WorldboxGame
{
    public class Tooltips : ExtendLibrary<TooltipAsset, Tooltips>
    {
        [GetOnly("tip")] public static TooltipAsset Tip { get; private set; }
        [GetOnly(S_Tooltip.actor)] public static TooltipAsset Actor { get; private set; }
        [GetOnly(S_Tooltip.actor_king)] public static TooltipAsset ActorKing { get; private set; }
        [GetOnly(S_Tooltip.actor_leader)] public static TooltipAsset ActorLeader { get; private set; }
        [GetOnly(S_Tooltip.book)] public static TooltipAsset Book { get; private set; }
        public static TooltipAsset Sect { get; private set; }
        public static TooltipAsset GeoRegion { get; private set; }
        [CloneSource("tooltip_meta_list_kingdoms")]
        public static TooltipAsset ListGeoRegion { get; private set; }
        [CloneSource("tooltip_meta_list_kingdoms")]
        public static TooltipAsset ListSect { get; private set; }
        public static TooltipAsset RawTip { get; private set; }

        public static TooltipAsset SpecialItem { get; private set; }
        public static TooltipAsset GetMetaListTooltipAsset(MetaTypeExtend meta_type)
        {
            return meta_type switch
            {
                MetaTypeExtend.GeoRegion => ListGeoRegion,
                MetaTypeExtend.Sect => ListSect,
                _ => null,
            };
        }
        protected override bool AutoRegisterAssets() => true;
        protected override void OnInit()
        {
            SpecialItem.prefab_id = "tooltips/tooltip_cultiway_special_item";
            SpecialItem.callback = ShowSpecialItem;
            SpecialItemTooltip.PatchTo<Tooltip>(SpecialItem.prefab_id);
            Sect.prefab_id = "tooltips/tooltip_cultiway_sect";
            Sect.callback = ShowSect;
            SectTooltip.PatchTo<Tooltip>(Sect.prefab_id);

            GeoRegion.prefab_id = "tooltips/tooltip_cultiway_geo_region";
            GeoRegion.callback = ShowGeoRegion;
            GeoRegionTooltip.PatchTo<Tooltip>(GeoRegion.prefab_id);

            ListGeoRegion.callback = (TooltipShowAction)Delegate.Combine((TooltipShowAction)AssetManager.tooltips.showNormal, (TooltipShowAction)ShowGeoRegionMetaListInfo);
            ListSect.callback = (TooltipShowAction)Delegate.Combine((TooltipShowAction)AssetManager.tooltips.showNormal, (TooltipShowAction)ShowSectMetaListInfo);

            Book.callback += ShowCustomBookReadAction;
            
            RawTip.callback = ShowRawTip;
        }
        private void ShowGeoRegionMetaListInfo(Tooltip tooltip, string type, TooltipData data)
        {
            var categoryIds = new HashSet<string>();
            foreach (var geoRegion in I.GeoRegions.list)
            {
                if (geoRegion == null || geoRegion.isRekt()) continue;
                if (string.IsNullOrEmpty(geoRegion.data?.CategoryId)) continue;
                categoryIds.Add(geoRegion.data.CategoryId);
            }

            AssetManager.tooltips.setIconValue(tooltip, "i_total", I.GeoRegions.Count);
            AssetManager.tooltips.setIconValue(tooltip, "i_destroyed", categoryIds.Count);
            AssetManager.tooltips.setIconSprite(tooltip, "i_total", MetaTypes.GeoRegion.icon_list);
            AssetManager.tooltips.setIconSprite(tooltip, "i_destroyed", "iconUnity");
        }

        private void ShowSectMetaListInfo(Tooltip tooltip, string type, TooltipData data)
        {
            int memberCount = 0;
            foreach (Sect sect in I.Sects.list)
            {
                if (sect == null || sect.isRekt()) continue;
                memberCount += sect.countUnits();
            }

            AssetManager.tooltips.setIconValue(tooltip, "i_total", I.Sects.Count);
            AssetManager.tooltips.setIconValue(tooltip, "i_destroyed", memberCount);
            AssetManager.tooltips.setIconSprite(tooltip, "i_total", MetaTypes.Sect.icon_list);
            AssetManager.tooltips.setIconSprite(tooltip, "i_destroyed", "iconPopulation");
        }

        private void ShowGeoRegion(Tooltip tooltip, string type, TooltipData data)
        {
            if (!long.TryParse(data.tip_name, out long regionId))
            {
                tooltip.setTitle("ERROR");
                return;
            }

            GeoRegion geoRegion = I.GeoRegions.get(regionId);
            if (geoRegion == null)
            {
                tooltip.setTitle("ERROR");
                return;
            }

            GeoRegionAsset category = geoRegion.GetCategory();
            GeoRegionManager manager = I.GeoRegions;
            List<City> cities = manager.GetCitiesInRegion(geoRegion, int.MaxValue);
            List<Kingdom> kingdoms = manager.GetKingdomsInRegion(geoRegion, int.MaxValue);
            List<GeoRegion> overlappingRegions = manager.GetOverlappingRegions(geoRegion, int.MaxValue);
            List<GeoRegion> adjacentRegions = manager.GetAdjacentRegions(geoRegion, geoRegion.data.Layer, int.MaxValue);
            int population = cities.Sum(city => city.getPopulationPeople());

            tooltip.setTitle(geoRegion.name, "GeoRegion", geoRegion.getColor().color_text);
            tooltip.GetComponent<GeoRegionTooltip>()?.Setup(geoRegion);
            tooltip.setSpeciesIcon(category.GetSpriteIcon());
            tooltip.transform.FindRecursive("Stats")?.gameObject.SetActive(true);
            AssetManager.tooltips.setIconValue(tooltip, "i_age", geoRegion.getAge());
            AssetManager.tooltips.setIconValue(tooltip, "i_population", population);
            AssetManager.tooltips.setIconSprite(tooltip, "i_army", "iconZones");
            AssetManager.tooltips.setIconValue(tooltip, "i_army", geoRegion.data.TileCount);

            tooltip.addLineText("Cultiway.GeoRegion.Category", category.GetDisplayName());
            tooltip.addLineText("Cultiway.GeoRegion.Layer", GeoRegionSelectedTagsContainer.FormatLayer(geoRegion.data.Layer));
            tooltip.addLineText("Cultiway.GeoRegion.Center", $"{geoRegion.data.CenterX}, {geoRegion.data.CenterY}");

            tooltip.addLineBreak();
            tooltip.addLineIntText("Cultiway.GeoRegion.Kingdoms", kingdoms.Count);
            tooltip.addLineIntText("Cultiway.GeoRegion.Cities", cities.Count);
            AddGeoRegionMainKingdom(tooltip, kingdoms);
            AddGeoRegionMainCity(tooltip, cities);

            tooltip.addLineBreak();
            tooltip.addLineIntText("Cultiway.GeoRegion.Overlapping", overlappingRegions.Count);
            tooltip.addLineIntText("Cultiway.GeoRegion.Adjacent", adjacentRegions.Count);
        }

        private static void AddGeoRegionMainKingdom(Tooltip tooltip, IReadOnlyList<Kingdom> kingdoms)
        {
            if (kingdoms.Count == 0) return;

            Kingdom kingdom = kingdoms[0];
            tooltip.addLineText("Cultiway.GeoRegion.MainKingdom", kingdom.name, kingdom.getColor().color_text);
        }

        private static void AddGeoRegionMainCity(Tooltip tooltip, IReadOnlyList<City> cities)
        {
            if (cities.Count == 0) return;

            City city = cities[0];
            string color = city.kingdom?.getColor()?.color_text;
            tooltip.addLineText("Cultiway.GeoRegion.MainCity", city.name, color);
        }

        private void ShowSect(Tooltip tooltip, string type, TooltipData data)
        {
            if (!long.TryParse(data.tip_name, out long sectId))
            {
                tooltip.setTitle("ERROR");
                return;
            }

            var sect = I.Sects.get(sectId);
            if (sect == null)
            {
                tooltip.setTitle("ERROR");
                return;
            }
            tooltip.setTitle(sect.name, "sect", sect.getColor().color_text);
            tooltip.GetComponent<SectTooltip>()?.Setup(sect);
            tooltip.transform.FindRecursive("Stats")?.gameObject.SetActive(true);
            tooltip.addLineIntText("adults", sect.countAdults());
            tooltip.addLineIntText("children", sect.countChildren());
            tooltip.addLineIntText("Cultiway.Sect.Level", sect.data.Level);
            tooltip.addLineIntText("Cultiway.Sect.Reputation", sect.data.Reputation);
        }

        private void ShowCustomBookReadAction(Tooltip tooltip, string type, TooltipData data)
        {
            var book = data.book;
            var bte = book.getAsset().GetExtend<BookTypeAssetExtend>();
            if (bte.instance_read_action != null)
            {
                tooltip.addLineText($"Cultiway.Book.ReadAction.{bte.instance_read_action.Method.Name}", "");
            }
        }

        private void ShowRawTip(Tooltip tooltip, string type, TooltipData data)
        {
            tooltip.name.text = LM.Has(data.tip_name) ? LM.Get(data.tip_name) : data.tip_name;
            
            if (!string.IsNullOrEmpty(data.tip_description))
            {
                tooltip.setDescription(LM.Has(data.tip_description) ? LM.Get(data.tip_description) : data.tip_description);
            }

            if (!string.IsNullOrEmpty(data.tip_description_2))
            {
                tooltip.setBottomDescription(LM.Has(data.tip_description_2) ? LM.Get(data.tip_description_2) : data.tip_description_2);
            }
        }

        private static void ShowSpecialItem(Tooltip tooltip, string type, TooltipData data = default)
        {
            if (string.IsNullOrEmpty(data.tip_name)) return;
            Entity entity = ModClass.I.W.GetEntityById(int.Parse(data.tip_name));
            if (entity.IsNull) return;
            tooltip.GetComponent<SpecialItemTooltip>()?.Setup(type, entity);
        }
    }
}
