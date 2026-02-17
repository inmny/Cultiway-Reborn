using Cultiway.Content;

namespace Cultiway.Const;

public static class GeneralSettings
{
    public static float SpawnNaturally { get; private set; } = 0.1f;
    public static bool EnableGeoSystems { get; private set; } = true;
    public static void SetElementRootSpawnNaturally(float value)
    {
        SpawnNaturally = value;
    }

    public static void SwitchGeoSystems(bool value)
    {
        EnableGeoSystems = value;
    }

    public static void SwitchTrainExperimentalTimedDispatch(bool value)
    {
        TrainConfig.ExperimentalTimedDispatchEnabled = value;
    }
}
