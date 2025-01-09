using System.Collections.Generic;
using System.Collections.ObjectModel;
using Cultiway.Abstract;

namespace Cultiway;

public partial class WorldboxGame
{
    public class WorldBehaviours : ExtendLibrary<WorldBehaviourAsset, WorldBehaviours>
    {
        [GetOnly("erosion")] public static WorldBehaviourAsset Erosion { get; private set; }

        protected override void OnInit()
        {
            RegisterAssets("Cultiway.WorldBehaviours");
            Erosion.interval = 9999;
        }
    }
}