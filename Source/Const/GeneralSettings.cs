using Cultiway.Content;

namespace Cultiway.Const;

public static class GeneralSettings
{
    public static float SpawnNaturally { get; private set; } = 0.1f;
    /// <summary>
    /// 地理演化
    /// </summary>
    public static bool EnableGeoSystems { get; private set; } = true;
    /// <summary>
    /// 功法
    /// </summary>
    public static bool EnableCultibookSystems { get; private set; } = true;
    /// <summary>
    /// 符箓
    /// </summary>
    public static bool EnableTalismanSystems { get; private set; } = true;
    
    /// <summary>
    /// 丹药
    /// </summary>
    public static bool EnableElixirSystems { get; private set; } = true;
    /// <summary>
    /// 炼器
    /// </summary>
    public static bool EnableArtifactSystems { get; private set; } = true;
    /// <summary>
    /// 师徒
    /// </summary>
    public static bool EnableAMSystems { get; private set; } = true;
    /// <summary>
    /// 技能
    /// </summary>
    public static bool EnableSkillSystems { get; private set; } = true;
    /// <summary>
    /// 宗门。关闭后不再允许建立新宗门。
    /// </summary>
    public static bool EnableSectSystems { get; private set; } = true;
    /// <summary>
    /// 灵气扩散。
    /// </summary>
    public static bool EnableWakanSpread { get; private set; } = true;
    /// <summary>
    /// 自然生命回复。
    /// </summary>
    public static bool EnableNaturalHealthRestore { get; private set; } = true;
    /// <summary>
    /// 自然灵气吸收。
    /// </summary>
    public static bool EnableNaturalWakanRestore { get; private set; } = true;
    /// <summary>
    /// 魔法师自然恢复 mana 和精神力。
    /// </summary>
    public static bool EnableNaturalMagicRestore { get; private set; } = true;
    public static void SetElementRootSpawnNaturally(float value)
    {
        SpawnNaturally = value;
    }

    public static void SwitchGeoSystems(bool value)
    {
        EnableGeoSystems = value;
    }

    public static void SwitchCultibookSystems(bool value)
    {
        EnableCultibookSystems = value;
    }
    public static void SwitchTalismanSystems(bool value)
    {
        EnableTalismanSystems = value;
    }

    public static void SwitchElixirSystems(bool value)
    {
        EnableElixirSystems = value;
    }

    public static void SwitchArtifactSystems(bool value)
    {
        EnableArtifactSystems = value;
    }

    public static void SwitchAMSystems(bool value)
    {
        EnableAMSystems = value;
    }
    public static void SwitchSkillSystems(bool value)
    {
        EnableSkillSystems = value;
    }

    public static void SwitchSectSystems(bool value)
    {
        EnableSectSystems = value;
    }

    public static void SwitchWakanSpread(bool value)
    {
        EnableWakanSpread = value;
    }

    public static void SwitchNaturalHealthRestore(bool value)
    {
        EnableNaturalHealthRestore = value;
    }

    public static void SwitchNaturalWakanRestore(bool value)
    {
        EnableNaturalWakanRestore = value;
    }

    public static void SwitchNaturalMagicRestore(bool value)
    {
        EnableNaturalMagicRestore = value;
    }

    public static void SwitchTrainExperimentalTimedDispatch(bool value)
    {
        TrainConfig.ExperimentalTimedDispatchEnabled = value;
    }
}
