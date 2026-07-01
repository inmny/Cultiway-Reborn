using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Core;
using Cultiway.Tables;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using Cultiway.Core.Components;
using Cultiway.Core.GeoLib.Components;
using Friflo.Engine.ECS;
using UnityEngine;
using db;
using strings;
using Cultiway.Core.Libraries;
using Cultiway.UI.Components;

namespace Cultiway;

public partial class WorldboxGame
{
    [Dependency(typeof(MetaTypes))]
    public class Nameplates : ExtendLibrary<NameplateAsset, Nameplates> 
    {
        private const int SectZoneModeResidence = 0;

        public static NameplateAsset Sect {get; private set;}
        public static NameplateAsset GeoRegion { get; private set; }
        protected override bool AutoRegisterAssets() => false;
        protected override void OnInit()
        {
            Sect = Add(new NameplateAsset()
            {
                id = "plate_sect",
                path_sprite = "ui/nameplates/nameplate_army",
                padding_left = 26,
                padding_right = 18,
                padding_top = -2,
                banner_only_mode_scale = 1.5f,
                map_mode = MetaTypeExtend.Sect.Back(),
                action_main = new NameplateBase(ActionSect)
            });
            GeoRegion = Add(new NameplateAsset()
            {
                id = "plate_geo_region",
                path_sprite = "ui/nameplates/nameplate_culture",
                padding_left = 11,
                padding_right = 13,
                map_mode = MetaTypeExtend.GeoRegion.Back(),
                action_main = new NameplateBase(ActionGeoRegion)
            });
        }
        private static void ActionSect(NameplateManager manager, NameplateAsset asset)
        {
            int current = 0;
            using (ListPool<Sect> sortedSects = new(WorldboxGame.I.Sects.list))
            {
                sortedSects.Sort(CompareSectsForNameplate);
                foreach (Sect sect in sortedSects)
                {
                    if (current >= asset.max_nameplate_count) return;
                    if (sect == null || sect.isRekt()) continue;
                    if (!TryGetSectNameplatePosition(sect, out Vector3 position)) continue;

                    NameplateText nameplate = manager.prepareNext(asset, sect);
                    ApplySectNameplate(nameplate, sect, position);
                    current++;
                }
            }
        }

        private static int CompareSectsForNameplate(Sect left, Sect right)
        {
            int favoriteComparison = right.isFavorite().CompareTo(left.isFavorite());
            if (favoriteComparison != 0) return favoriteComparison;

            int selectedComparison = right.isSelected().CompareTo(left.isSelected());
            if (selectedComparison != 0) return selectedComparison;

            return right.countUnits().CompareTo(left.countUnits());
        }

        private static bool TryGetSectNameplatePosition(Sect sect, out Vector3 position)
        {
            position = Vector3.zero;
            if (sect == null || !sect.isAlive()) return false;

            if (WorldboxGame.MetaTypes.Sect.getZoneOptionState() == SectZoneModeResidence)
            {
                return TryGetSectResidenceNameplatePosition(sect, out position);
            }

            return TryGetSectMembersNameplatePosition(sect, out position);
        }

        private static bool TryGetSectResidenceNameplatePosition(Sect sect, out Vector3 position)
        {
            position = Vector3.zero;

            WorldTile tile = sect.GetResidenceTile();
            if (tile == null) return false;

            position = tile.posV3;
            position.y += 2f;
            return true;
        }

        private static bool TryGetSectMembersNameplatePosition(Sect sect, out Vector3 position)
        {
            position = Vector3.zero;
            List<Actor> members = sect.GetLivingMembers();
            if (members.Count == 0) return false;

            Vector2 sum = Vector2.zero;
            int count = 0;
            for (int i = 0; i < members.Count; i++)
            {
                Actor member = members[i];
                if (member == null || member.isRekt()) continue;

                sum += member.current_position;
                count++;
            }

            if (count == 0) return false;

            position = new Vector3(sum.x / count, sum.y / count, 0f);
            position.y += -2f;
            return true;
        }

        private static void ApplySectNameplate(NameplateText nameplate, Sect sect, Vector3 position)
        {
            nameplate.setupMeta(sect.data, sect.getColor());

            int unitCount = sect.countUnits();
            string text = nameplate.is_mini ? string.Empty : $"{sect.name} - {unitCount}";
            nameplate.setText(text, position, 10);
            nameplate.setPriority(unitCount);
            nameplate.showSpecies(sect.getActorAsset().getSpriteIcon());
            SectNameplateBanner banner = nameplate.GetComponent<SectNameplateBanner>() ??
                                         nameplate.gameObject.AddComponent<SectNameplateBanner>();
            banner.Show(sect);
        }

        private static void ActionGeoRegion(NameplateManager manager, NameplateAsset asset)
        {
            if (ModClass.I?.TileExtendManager == null) return;
            if (!ModClass.I.TileExtendManager.Ready()) return;

            var currMapMode = ModClass.I.CustomMapModeManager?.CurrMapMode;
            int current = 0;
            foreach (var geoRegion in I.GeoRegions.list)
            {
                if (current >= asset.max_nameplate_count) return;
                if (geoRegion == null || geoRegion.isRekt()) continue;

                // 与 CustomMapModeLibrary 的渲染层选择保持一致
                if (!ShouldShowGeoRegionInCurrentMapMode(geoRegion, currMapMode)) continue;

                int tileCount = geoRegion.data?.TileCount ?? 0;
                if (tileCount <= 0) continue;
                if (!TryGetRegionPosition(geoRegion, out var position)) continue;
                if (!World.world.move_camera.isWithinCameraViewNotPowerBar(position)) continue;

                var nameplate = manager.prepareNext(asset, geoRegion);
                ApplyGeoRegionNameplate(nameplate, geoRegion, geoRegion.getColor().getColorMain32(), position, tileCount);
                current++;
            }
        }

        /// <summary>
        /// 根据当前自定义地图模式过滤 GeoRegion 铭牌显示层。
        /// 规则应与 CustomMapModeLibrary 中各 map mode 的 kernel_func 一致。
        /// </summary>
        private static bool ShouldShowGeoRegionInCurrentMapMode(GeoRegion geoRegion, CustomMapModeAsset currMapMode)
        {
            var layer = geoRegion.data.Layer;

            if (currMapMode == CustomMapModeLibrary.GeoRegionLandform)
            {
                return layer == GeoRegionLayer.Landform;
            }

            if (currMapMode == CustomMapModeLibrary.GeoRegionLandmass)
            {
                return layer == GeoRegionLayer.Landmass;
            }

            if (currMapMode == CustomMapModeLibrary.GeoRegionMorphology)
            {
                return layer == GeoRegionLayer.Strait ||
                       layer == GeoRegionLayer.Peninsula ||
                       layer == GeoRegionLayer.Archipelago;
            }

            // 默认/Primary map mode 只显示 Primary 层
            return layer == GeoRegionLayer.Primary;
        }

        private static bool TryGetRegionPosition(GeoRegion geoRegion, out Vector3 position)
        {
            position = Vector3.zero;
            if (geoRegion?.data == null) return false;

            int x = geoRegion.data.CenterX;
            int y = geoRegion.data.CenterY;
            if (x < 0 || y < 0 || x >= MapBox.width || y >= MapBox.height) return false;

            WorldTile tile = World.world.GetTile(x, y);
            if (tile == null) return false;

            position = tile.posV3;
            return true;
        }

        private static void ApplyGeoRegionNameplate(NameplateText nameplate, GeoRegion geoRegion, Color32 color, Vector3 position, int tileCount)
        {
            var colorHex = Toolbox.colorToHex(color, false);
            var colorAsset = ColorAsset.tryMakeNewColorAsset(colorHex);
            nameplate.setupMeta(geoRegion.data, colorAsset);
            var text = nameplate.is_mini ? string.Empty : geoRegion.name;
            nameplate.setText(text, position, 10);
            nameplate.setPriority(tileCount);

            nameplate.showSpecies(geoRegion.GetCategory().GetSpriteIcon());
        }
    }
}
