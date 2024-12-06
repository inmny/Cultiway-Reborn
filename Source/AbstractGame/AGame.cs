namespace Cultiway.AbstractGame;

public abstract class AGame
{
    public abstract float GetLogicDeltaTime();
    public abstract float GetGameTime();
    public abstract bool  IsPaused();
}