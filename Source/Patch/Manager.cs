using System;
using System.Linq;
using System.Reflection;
using Cultiway.Debug;
using HarmonyLib;

namespace Cultiway.Patch;

internal class Manager
{
    public void Init()
    {
        var ns = GetType().Namespace;
        foreach (var t in ModClass.A.GetTypes().Where(t => t.Name.StartsWith("Patch") && t.Namespace == ns))
        {
            Try.Start(() =>
            {
                try
                {
                    Harmony.CreateAndPatchAll(t, "inmny.cultiway");
                    ModClass.LogInfo($"Patch {t.Name}");
                    var special_patch_method = t.GetMethod("SpecialPatch", BindingFlags.Static | BindingFlags.Public);
                    if (special_patch_method != null)
                    {
                        special_patch_method.Invoke(null, null);
                        ModClass.LogInfo($"Patch {t.Name} specially");
                    }
                }
                catch (Exception e)
                {
                    ModClass.LogError($"Failed to patch {t.Name}");
                    throw;
                }
            });
        }
    }
}