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
using db;
using strings;

namespace Cultiway;

public partial class WorldboxGame
{
    [Dependency(typeof(MetaTypes))]
    public class Nameplates : ExtendLibrary<NameplateAsset, Nameplates> 
    {
        public static NameplateAsset GeoRegion { get; private set; }
        protected override bool AutoRegisterAssets() => false;
        protected override void OnInit()
        {
            GeoRegion = Add(new NameplateAsset()
            {
                id = "plate_geo_region",
                path_sprite = "cultiway/icons/iconExtendGeoRegion",
                padding_left = 11,
                padding_right = 13,
                map_mode = MetaTypeExtend.GeoRegion.Back(),
                action_main = new NameplateBase(ActionGeoRegion)
            });
        }
        private static void ActionGeoRegion(NameplateManager manager, NameplateAsset asset)
        {

        }
    }
}