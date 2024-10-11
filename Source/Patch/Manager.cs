using System.Linq;
using HarmonyLib;

namespace Cultiway.Patch;

internal class Manager
{
    public void Init()
    {
        var ns = GetType().Namespace;
        foreach (var t in ModClass.A.GetTypes().Where(t => t.Name.StartsWith("Patch") && t.Namespace == ns))
        {
            Harmony.CreateAndPatchAll(t, "inmny.cultiway");
        }
    }
}