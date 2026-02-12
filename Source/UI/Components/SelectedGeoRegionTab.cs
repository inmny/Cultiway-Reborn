using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cultiway.Const;
using Cultiway.Core;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.UI.Components
{
    public class SelectedGeoRegionTab : SelectedMeta<GeoRegion, GeoRegionData>
    {
        public override MetaType meta_type => MetaTypeExtend.GeoRegion.Back();
        public static PowersTab PowersTab {get; private set;}
        public override string getPowerTabAssetID()
        {
            return WorldboxGame.PowerTabs.SelectedGeoRegion.id;
        }
        internal static void Init()
        {
            var tab = Manager.CreateSelectedMetaTab<SelectedGeoRegionTab, GeoRegion, GeoRegionData>(WorldboxGame.PowerTabs.SelectedGeoRegion.id);



            PowersTab = tab.GetComponent<PowersTab>();
        }
    }
}