using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Libraries;
using Cultiway.UI.Components;
using Cultiway.UI.Prefab;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using NeoModLoader.General;
using strings;
using UnityEngine;
using UnityEngine.UI;

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
        [AssetId("sect_trait")]
        public static TooltipAsset SectTrait { get; private set; }
        public static TooltipAsset GeoRegion { get; private set; }
        [CloneSource("tooltip_meta_list_kingdoms")]
        public static TooltipAsset ListGeoRegion { get; private set; }
        [CloneSource("tooltip_meta_list_kingdoms")]
        public static TooltipAsset ListSect { get; private set; }
        public static TooltipAsset RawTip { get; private set; }
        [CloneSource("tip"), AssetId("Cultiway.WanfaSkill")]
        public static TooltipAsset Skill { get; private set; }
        [CloneSource("tip"), AssetId("Cultiway.Cultisys")]
        public static TooltipAsset Cultisys { get; private set; }

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

            SectTrait.prefab_id = "tooltips/tooltip_trait";
            SectTrait.callback = ShowSectTrait;

            GeoRegion.prefab_id = "tooltips/tooltip_cultiway_geo_region";
            GeoRegion.callback = ShowGeoRegion;
            GeoRegionTooltip.PatchTo<Tooltip>(GeoRegion.prefab_id);

            ListGeoRegion.callback = (TooltipShowAction)Delegate.Combine((TooltipShowAction)AssetManager.tooltips.showNormal, (TooltipShowAction)ShowGeoRegionMetaListInfo);
            ListSect.callback = (TooltipShowAction)Delegate.Combine((TooltipShowAction)AssetManager.tooltips.showNormal, (TooltipShowAction)ShowSectMetaListInfo);

            Book.callback += ShowCustomBookReadAction;
            
            RawTip.callback = ShowRawTip;

            Skill.prefab_id = "tooltips/tooltip_cultiway_wanfa_skill";
            Skill.callback = ShowSkill;
            SkillTooltip.PatchTo<Tooltip>(Skill.prefab_id);

            Cultisys.prefab_id = "tooltips/tooltip_cultiway_cultisys";
            Cultisys.callback = ShowCultisys;
            CultisysTooltip.PatchTo<Tooltip>(Cultisys.prefab_id);
        }

        private static void ShowSkill(Tooltip tooltip, string type, TooltipData data)
        {
            tooltip.GetComponent<SkillTooltip>().SetupPending();
        }

        private static void ShowCultisys(Tooltip tooltip, string type, TooltipData data)
        {
            tooltip.GetComponent<CultisysTooltip>().SetupPending();
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

            string sectColor = sect.getColor().color_text;
            tooltip.setTitle(sect.name, "sect", sect.getColor().color_text);
            tooltip.GetComponent<SectTooltip>()?.Setup(sect);
            tooltip.setSpeciesIcon(SpriteTextureLoader.getSprite("cultiway/icons/iconSect"));
            tooltip.transform.FindRecursive("Stats")?.gameObject.SetActive(true);
            AssetManager.tooltips.setIconValue(tooltip, "i_age", sect.getAge());
            AssetManager.tooltips.setIconValue(tooltip, "i_population", sect.countUnits());
            AssetManager.tooltips.setIconSprite(tooltip, "i_army", "iconZones");
            AssetManager.tooltips.setIconValue(tooltip, "i_army", sect.GetTerritoryCount());

            string doctrineName = GetSectDoctrineName(sect);
            if (doctrineName != "-")
            {
                tooltip.setDescription($"{LMTools.GetOrKey("Cultiway.Sect.DoctrineCultibook")}: {doctrineName}");
            }

            ShowSectLeaderLine(tooltip, sect, sectColor);
            tooltip.addLineBreak();
            tooltip.addLineIntText("Cultiway.Sect.Level", sect.data.Level);
            tooltip.addLineIntText("Cultiway.Sect.Reputation", sect.data.Reputation);
            tooltip.addLineIntText("adults", sect.countAdults());
            tooltip.addLineIntText("children", sect.countChildren());
            tooltip.addLineIntText("Cultiway.Sect.Territory", sect.GetTerritoryCount());

            tooltip.addLineBreak();
            ShowSectHomeCityLine(tooltip, sect, sectColor);
            ShowSectFounderLine(tooltip, sect, sectColor);
            tooltip.addLineText("Cultiway.Sect.DoctrineCultibook", doctrineName, sectColor, false, true, 21);

            tooltip.addLineBreak();
            tooltip.addLineIntText("Cultiway.Sect.Cultibooks", sect.data.CultibookCount);
            tooltip.addLineIntText("Cultiway.Sect.ElixirRecipes", sect.data.ElixirRecipeCount);
            tooltip.addLineIntText("Cultiway.Sect.Skillbooks", sect.data.SkillbookCount);
        }

        private static void ShowSectLeaderLine(Tooltip tooltip, Sect sect, string fallbackColor)
        {
            Actor leader = sect.GetLeaderActor();
            if (!leader.isRekt())
            {
                tooltip.addLineText("Cultiway.Sect.Leader", leader.getName(), GetActorLineColor(leader, fallbackColor), false, true, 21);
                return;
            }

            tooltip.addLineText("Cultiway.Sect.Leader", GetStoredNameOrDash(sect.data.LeaderActorName), fallbackColor, false, true, 21);
        }

        private static void ShowSectFounderLine(Tooltip tooltip, Sect sect, string fallbackColor)
        {
            if (sect.data.FounderActorID > 0)
            {
                Actor founder = World.world.units.get(sect.data.FounderActorID);
                if (!founder.isRekt())
                {
                    tooltip.addLineText("Cultiway.Sect.Founder", founder.getName(), GetActorLineColor(founder, fallbackColor), false, true, 21);
                    return;
                }
            }

            tooltip.addLineText("Cultiway.Sect.Founder", GetStoredNameOrDash(sect.data.FounderActorName), fallbackColor, false, true, 21);
        }

        private static void ShowSectHomeCityLine(Tooltip tooltip, Sect sect, string fallbackColor)
        {
            City city = sect.GetHomeCity();
            if (!city.isRekt())
            {
                string color = city.kingdom?.getColor()?.color_text ?? fallbackColor;
                tooltip.addLineText("Cultiway.Sect.HomeCity", city.name, color, false, true, 21);
                return;
            }

            tooltip.addLineText("Cultiway.Sect.HomeCity", GetStoredNameOrDash(sect.GetResidenceName()), fallbackColor, false, true, 21);
        }

        private static string GetSectDoctrineName(Sect sect)
        {
            CultibookAsset doctrine = sect.GetDoctrineCultibook();
            if (doctrine != null) return doctrine.Name;

            return GetStoredNameOrDash(sect.data.DoctrineCultibookName);
        }

        private static string GetActorLineColor(Actor actor, string fallbackColor)
        {
            return actor?.kingdom?.getColor()?.color_text ?? fallbackColor;
        }

        private static string GetStoredNameOrDash(string name)
        {
            return string.IsNullOrEmpty(name) ? "-" : name;
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

        private static void ShowSectTrait(Tooltip tooltip, string type, TooltipData data)
        {
            if (data.custom_data_string == null || !data.custom_data_string.TryGetValue("sect_trait", out string traitId))
            {
                tooltip.setTitle("ERROR");
                return;
            }

            Core.Libraries.SectTrait trait = ModClass.L.SectTraitLibrary.get(traitId);
            if (trait == null)
            {
                tooltip.setTitle("ERROR");
                return;
            }

            bool showTraitInfo = !data.is_editor_augmentation_button || trait.isAvailable();
            Rarity rarity = trait.rarity;
            tooltip.name.text = showTraitInfo ? trait.getTranslatedName() : LocalizedTextManager.getText("achievement_tip_hidden");
            tooltip.name.color = rarity.getRarityColor();

            Transform root = tooltip.transform;
            Text rarityText = root.Find("Icon and Info/Background/Rarity Type/Rarity Text")?.GetComponent<Text>();
            if (rarityText != null)
            {
                rarityText.text = rarity.getAsset().getLocaleID().Localize();
                rarityText.color = rarity.getRarityColor();
            }

            Image icon = root.Find("Icon and Info/IconBG/Icon")?.GetComponent<Image>();
            if (icon != null)
            {
                icon.sprite = trait.getSprite();
                icon.color = showTraitInfo ? Toolbox.color_white : Toolbox.color_black;
            }

            root.Find("Icon and Info/IconBG/LegendaryBG")?.gameObject.SetActive(rarity == Rarity.R3_Legendary);

            Text countText = root.Find("Icon and Info/Background/IconedText")?.GetComponent<Text>();
            if (countText != null)
            {
                countText.text = showTraitInfo ? trait.getCountRows() : "";
            }

            SetupSectTraitRarityStars(root, rarity);

            string description = trait.getTranslatedDescription();
            if (!string.IsNullOrEmpty(description))
            {
                if (!trait.isAvailable() && trait.show_for_unlockables_ui)
                {
                    if (trait.unlocked_with_achievement)
                    {
                        description = LocalizedTextManager.getText("trait_locked_tooltip_text_achievement")
                            .ColorHex(ColorStyleLibrary.m.color_text_grey);
                        string achievementName = "<color=#00ffffff>" + trait.getAchievementLocaleID().Localize() + "</color>";
                        description = description.Replace("$achievement_id$", achievementName);
                    }
                    else
                    {
                        description = LocalizedTextManager.getText("sect_trait_locked_tooltip_text_exploration");
                    }
                }

                tooltip.setDescription(description);
            }

            string bottomDescription = showTraitInfo ? trait.getTranslatedDescription2() : null;
            if (!string.IsNullOrEmpty(bottomDescription))
            {
                tooltip.setBottomDescription(bottomDescription);
            }
        }

        private static void SetupSectTraitRarityStars(Transform root, Rarity rarity)
        {
            GameObject stars = root.Find("Icon and Info/Background/Rarity Type/Rarity Stars")?.gameObject;
            if (stars == null) return;

            int rarityIndex = (int)rarity;
            for (int i = 0; i < stars.transform.childCount; i++)
            {
                Image star = stars.transform.GetChild(i).GetComponent<Image>();
                if (star == null) continue;

                star.color = i <= rarityIndex ? Toolbox.makeColor("#313131") : Color.black;
            }
        }

        private static void ShowSpecialItem(Tooltip tooltip, string type, TooltipData data = default)
        {
            var specialItemTooltip = tooltip.GetComponent<SpecialItemTooltip>();
            specialItemTooltip?.HideSemanticIcons();
            if (string.IsNullOrEmpty(data.tip_name)) return;
            Entity entity = ModClass.I.W.GetEntityById(int.Parse(data.tip_name));
            if (entity.IsNull) return;
            specialItemTooltip?.Setup(type, entity);
        }
    }
}
