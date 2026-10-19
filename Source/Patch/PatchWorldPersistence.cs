using Cultiway.Utils.Extension;
using HarmonyLib;

namespace Cultiway.Patch;

/// <summary>在原版手动、自动保存与导入世界的共同入口接入模组资料。</summary>
internal static class PatchWorldPersistence
{
    /// <summary>清理本次世界的报告，保留应用级的数据部分登记。</summary>
    public static void SpecialPatch()
        => PatchMapBox.RegisterActionOnClearWorld(() => ModClass.I.WorldSaves.ClearWorldState());

    /// <summary>人物资料准备完毕后写入模组自己的数据键。</summary>
    [HarmonyPostfix, HarmonyPatch(typeof(Actor), nameof(Actor.prepareForSave))]
    private static void SaveActor(Actor __instance)
    {
        if (__instance.TryGetExtend(out var actor)) ModClass.I.WorldSaves.CaptureActor(actor);
    }

    /// <summary>原版已恢复身份和特质，但尚未重算属性及限制生命时，先恢复成长。</summary>
    [HarmonyPostfix, HarmonyPatch(typeof(Actor), nameof(Actor.loadFromSave))]
    private static void LoadActor(Actor __instance) => ModClass.I.WorldSaves.RestoreActor(__instance);

    /// <summary>采集世界资料，沿用原版世界文件及保存方式。</summary>
    [HarmonyPostfix, HarmonyPatch(typeof(SavedMap), nameof(SavedMap.create))]
    private static void SaveWorld(SavedMap __instance) => ModClass.I.WorldSaves.CaptureWorld(__instance);

    /// <summary>把世界关系恢复安排在原版人物和建筑全部加载之后。</summary>
    [HarmonyPostfix, HarmonyPatch(typeof(SaveManager), nameof(SaveManager.loadData))]
    private static void LoadWorld(SavedMap pData, string pPath)
        => SmoothLoader.add(() => ModClass.I.WorldSaves.FinishLoad(pData, pPath), "恢复修仙世界资料");

    /// <summary>原档无法保留时，只禁止覆盖原位置。</summary>
    [HarmonyPrefix, HarmonyPatch(typeof(SaveManager), nameof(SaveManager.saveWorldToDirectory))]
    private static void ProtectSource(string pFolder) => ModClass.I.WorldSaves.EnsureCanSave(pFolder);
}
