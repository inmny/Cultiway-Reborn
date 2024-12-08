using System;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.AbstractGame;
using UnityEngine;

namespace Cultiway;

public partial class WorldboxGame : AGame<WorldTile, TerraformOptions, BaseSimObject>
{
    internal WorldboxGame()
    {
        var library_ts = GetType().GetNestedTypes().Where(t => t.GetInterfaces().Contains(typeof(ICanInit))).ToList();
        foreach (Type t in library_ts) (Activator.CreateInstance(t) as ICanInit)?.Init();
    }

    public override float GetLogicDeltaTime()
    {
        return Time.deltaTime;
    }

    public override float GetGameTime()
    {
        return (float)World.world.getCurSessionTime();
    }

    public override bool IsPaused()
    {
        return World.world.isPaused();
    }

    public override void DamageWorld(WorldTile tile, int radius, TerraformOptions terraform, BaseSimObject source)
    {
        MapAction.damageWorld(tile, radius, terraform, source);
    }
}