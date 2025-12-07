using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NeoModLoader.General.Game.extensions;

namespace Cultiway.Core.Libraries
{
    public class PortalLibrary : AssetLibrary<PortalAsset>
    {
        public static PortalAsset Dock {get; private set;}
        public override void init()
        {
            base.init();
            Dock = add(new()
            {
                id = "Cultiway.Dock"
            });
            AssetManager.buildings.ForEach<BuildingAsset, BuildingLibrary>(asset => 
            {
                if (asset.docks)
                {
                    Dock.Buildings.Add(asset);
                }
            });
        }
    }
}