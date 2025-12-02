using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cultiway.Abstract;

namespace Cultiway.Content
{
    public class HistoryGroups : ExtendLibrary<HistoryGroupAsset, HistoryGroups>
    {
        protected override bool AutoRegisterAssets()
        {
            return true;
        }
        public static HistoryGroupAsset Cultivations { get; private set; }
        public static HistoryGroupAsset Sects { get; private set; }

        protected override void OnInit()
        {
            Cultivations.icon_path = "cultiway/icons/iconCultivation";
            Sects.icon_path = "cultiway/icons/iconMasterApprentice";
        }
    }
}