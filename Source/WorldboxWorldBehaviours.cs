using System.Collections.Generic;
using System.Collections.ObjectModel;
using Cultiway.Abstract;
using strings;

namespace Cultiway;

public partial class WorldboxGame
{
    public class WorldBehaviours : ExtendLibrary<WorldBehaviourAsset, WorldBehaviours>
    {
        [GetOnly(S_WorldBehaviour.erosion)] public static WorldBehaviourAsset Erosion { get; private set; }

        protected override bool AutoRegisterAssets() => true;
        protected override void OnInit()
        {
            Erosion.interval = 9999;
        }
    }
}