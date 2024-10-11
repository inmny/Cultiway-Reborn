using System;
using System.Linq;
using HarmonyLib;

namespace Cultiway.Content.Patch;

internal class Manager
{
    public void Init()
    {
        var ns = GetType().Namespace;
        foreach (var t in ModClass.A.GetTypes().Where(t => t.Name.StartsWith("Patch") && t.Namespace == ns))
        {
            try
            {
                Harmony.CreateAndPatchAll(t, "inmny.cultiway.content");
                ModClass.LogInfo($"Patch {t.Namespace}.{t.Name}");
            }
            catch (Exception e)
            {
                ModClass.LogWarning($"Failed to patch {t.Namespace}.{t.Name}");
                ModClass.LogError(e.ToString());
            }
        }
    }
}