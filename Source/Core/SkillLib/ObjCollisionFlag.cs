namespace Cultiway.Core.SkillLib;

public static class ObjCollisionFlag
{
    public const uint Actor    = 1 << 0;
    public const uint Building = 1 << 1;
    public const uint Enemy    = 1 << 2;
    public const uint Friend   = 1 << 3;
}