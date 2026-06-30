using System.Collections.Generic;

namespace Cultiway.Core.Logging;

public static class CultiLogEventRegistry
{
    private static readonly Dictionary<int, CultiLogEventDef> EventsById = new();

    internal static void Register(CultiLogEventDef def)
    {
        EventsById[def.Id] = def;
    }

    public static CultiLogEventDef Get(int id)
    {
        EventsById.TryGetValue(id, out var def);
        return def;
    }
}
