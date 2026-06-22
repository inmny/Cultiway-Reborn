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

namespace Cultiway;

public partial class WorldboxGame
{
    [Dependency(typeof(MetaTypes))]
    public class Nameplates : ExtendLibrary<NameplateAsset, Nameplates> 
    {
        public static NameplateAsset Sect {get; private set;}
        public static NameplateAsset GeoRegion { get; private set; }
        protected override bool AutoRegisterAssets() => false;
        protected override void OnInit()
        {
            Sect = Add(new NameplateAsset()
            {
                id = "plate_sect",
                path_sprite = "ui/nameplates/nameplate_culture",
                padding_left = 11,
                padding_right = 13,
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
            if (ModClass.I?.TileExtendManager == null) return;
            if (!ModClass.I.TileExtendManager.Ready()) return;

            int current = 0;
            foreach (var sect in WorldboxGame.I.Sects.list)
            {
                if (current >= asset.max_nameplate_count) return;
                // TODO: 添加Sect的Nameplate
                current++;
            }
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
