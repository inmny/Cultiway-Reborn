using System.Collections.Generic;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Content.Attributes;
using NeoModLoader.General.Game.extensions;
using strings;
using UnityEngine;

namespace Cultiway.Content;

public partial class Buildings
{
    [SetupButton, CloneSource(SB.flame_tower)]
    public static BuildingAsset VampireTower { get; private set; }
    private void SetupFantasyBuildings()
    {
        VampireTower.tower = false;
        VampireTower.spawn_units_asset = Actors.Bloodsucker.id;
        VampireTower.kingdom = KingdomAssets.Vampire.id;
    }
}