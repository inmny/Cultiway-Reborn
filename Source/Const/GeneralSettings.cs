namespace Cultiway.Const;

public static class GeneralSettings
{
    public static bool SpawnNaturally { get; private set; } = true;
    
    public static void SwitchElementRootSpawnNaturally(bool value)
    {
        SpawnNaturally = value;
    }
}