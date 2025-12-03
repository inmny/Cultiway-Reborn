using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cultiway.Abstract;
using NeoModLoader.General.Game.extensions;
using strings;
using UnityEngine;

namespace Cultiway.Content
{
    [Dependency(typeof(Buildings))]
    public class Terraforms : ExtendLibrary<TerraformOptions, Terraforms>
    {
        protected override bool AutoRegisterAssets()
        {
            return true;
        }

        [CloneSource(S_Terraform.road)]
        public static TerraformOptions TrainTrack { get; private set; }

        protected override void OnInit()
        {
            AssetManager.buildings.ForEach<BuildingAsset, BuildingLibrary>(building =>
            {
                if (building.type == Buildings.TrainStation.type) 
                if (TrainTrack.destroy_only.Contains(building.group))
                {
                    return;
                }
                TrainTrack.destroy_only.Add(building.group);
            });
        }
    }
}
