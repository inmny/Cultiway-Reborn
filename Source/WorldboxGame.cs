using System;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.AbstractGame;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway;

public partial class WorldboxGame : AGame<WorldTile, TerraformOptions, BaseSimObject>
{
    internal WorldboxGame()
    {
        I = this;
        var library_ts = GetType().GetNestedTypes().Where(t => t.GetInterfaces().Contains(typeof(ICanInit))).ToList();
        library_ts = DependencyAttribute.SortManagerTypes(library_ts);
        foreach (Type t in library_ts)
        {
            (Activator.CreateInstance(t) as ICanInit)?.Init();
            ModClass.LogInfo($"({nameof(WorldboxGame)}) initializes {t.Name}");
        }
    }

    public static WorldboxGame I { get; private set; }

    public override float GetLogicDeltaTime()
    {
        return World.world.getCurElapsed() / Mathf.Max(0.01f, Config.timeScale);
    }

    public override float GetGameTime()
    {
        return (float)World.world.getCurSessionTime();
    }

    public override bool IsPaused()
    {
        return World.world.isPaused();
    }

    [Hotfixable]
    public override void DamageWorld(WorldTile tile, int radius, TerraformOptions terraform, BaseSimObject source)
    {
        MapAction.damageWorld(tile, radius, terraform, source);
    }

    public override WorldTile GetTile(int x, int y)
    {
        return World.world.GetTile(x, y);
    }

    public void Pause()
    {
        Config.paused = true;
    }

    public float GetRenderDeltaTime()
    {
        return World.world.getCurElapsed();
    }

    public bool IsLoaded()
    {
        return Config.LOAD_TIME_GENERATE > 0;
    }
}