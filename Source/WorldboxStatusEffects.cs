using System.Collections.Generic;
using System.Collections.ObjectModel;
using Cultiway.Abstract;
using strings;

namespace Cultiway;

public partial class WorldboxGame
{
    public class StatusEffects : ExtendLibrary<StatusAsset, StatusEffects>
    {
        [GetOnly(S_Status.burning)] public static StatusAsset Burning { get; private set; }
        [GetOnly(S_Status.caffeinated)]public static StatusAsset Caffeinated { get; private set; }

        protected override void OnInit()
        {
            RegisterAssets();
        }
    }
}