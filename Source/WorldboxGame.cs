using Cultiway.AbstractGame;
using UnityEngine;

namespace Cultiway;

public class WorldboxGame : AGame
{
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
}