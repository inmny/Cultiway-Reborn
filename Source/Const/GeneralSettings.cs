namespace Cultiway.Const;

public static class GeneralSettings
{
    public static bool SpawnNaturally { get; private set; } = true;
    public static bool EnableGeoSystems { get; private set; } = true;
    public static void SwitchElementRootSpawnNaturally(bool value)
    {
        SpawnNaturally = value;
    }

    public static void SwitchGeoSystems(bool value)
    {
        EnableGeoSystems = value;
    }
}