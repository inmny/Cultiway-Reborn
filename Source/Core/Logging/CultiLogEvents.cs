namespace Cultiway.Core.Logging;

public static class CultiLogEvents
{
    public static class General
    {
        public static readonly CultiLogEventDef Message = new(
            1,
            "General.Message",
            CultiLogCategory.General,
            CultiLogLevel.Info,
            "{message}");

        public static readonly CultiLogEventDef Error = new(
            2,
            "General.Error",
            CultiLogCategory.Error,
            CultiLogLevel.Error,
            "{message}");
    }

    public static class Sect
    {
        public static readonly CultiLogEventDef Verify = new(
            1000,
            "Sect.Verify",
            CultiLogCategory.Sect,
            CultiLogLevel.Debug,
            "[SectVerify][{action}] {message}");
    }

    public static class Combat
    {
        public static readonly CultiLogEventDef DamageResolved = new(
            2000,
            "Combat.DamageResolved",
            CultiLogCategory.Combat,
            CultiLogLevel.Debug,
            "{message}");
    }
}
