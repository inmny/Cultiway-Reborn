using System.IO;
using Cultiway.Content.YaoBeasts;
using Cultiway.Utils.Extension;
using HarmonyLib;
using Newtonsoft.Json;

namespace Cultiway.Content.Patch;

/// <summary>沿原版单位的保存读取流程接通妖兽成长记录。</summary>
public static class PatchYaoPersistence
{
    /// <summary>原版角色资料准备完毕后写入模组自己的数据键。</summary>
    [HarmonyPostfix, HarmonyPatch(typeof(Actor), nameof(Actor.prepareForSave))]
    private static void Save(Actor __instance)
    {
        if (__instance.TryGetExtend(out var actor)) YaoPersistence.Save(actor);
    }

    /// <summary>原版单位和亚种恢复后重建妖兽；损坏记录只在这个读取边界报告。</summary>
    [HarmonyPostfix, HarmonyPatch(typeof(ActorManager), nameof(ActorManager.loadObject))]
    private static void Load(Actor __result)
    {
        if (__result == null) return;
        __result.data.get(YaoPersistence.DataKey, out string json, "");
        if (string.IsNullOrEmpty(json)) return;
        try
        {
            YaoPersistence.Restore(__result.GetExtend(), json);
        }
        catch (JsonException error)
        {
            ModClass.LogError($"单位 {__result.data.id} 的妖兽存档无法解析：{error.Message}");
        }
        catch (InvalidDataException error)
        {
            ModClass.LogError($"单位 {__result.data.id} 的妖兽存档无法恢复：{error.Message}");
        }
    }
}
