namespace Cultiway.AbstractGame;

public abstract class AGame<TTile, TTerraform, TSimObject>
{
    public abstract float GetLogicDeltaTime();
    public abstract float GetGameTime();
    public abstract bool  IsPaused();
    public abstract void  DamageWorld(TTile tile, int radius, TTerraform terraform, TSimObject source);
    public abstract TTile GetTile(int       x,    int y);
}