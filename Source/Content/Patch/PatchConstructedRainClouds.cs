using Cultiway.Content.Skills;
using HarmonyLib;

namespace Cultiway.Content.Patch;

/// <summary>原版天气回收即解除模组保存身份，防止对象池复用时串到另一朵云。</summary>
internal static class PatchConstructedRainClouds
{
    /// <summary>只为模组拥有的天气提供实际世界时间，原版自然云保持原调用。</summary>
    /// <param name="__instance">当前云。</param><param name="pElapsed">原版本轮推进秒数。</param>
    [HarmonyPrefix, HarmonyPatch(typeof(Cloud), nameof(Cloud.update))]
    private static bool BeforeUpdate(Cloud __instance, ref float pElapsed) =>
        ConstructedRainClouds.TryAdvance(__instance, ref pElapsed);

    /// <summary>回收发生前忘记原云，正常原版回收行为继续。</summary>
    /// <param name="__instance">实际回收的效果。</param>
    [HarmonyPrefix, HarmonyPatch(typeof(BaseEffect), nameof(BaseEffect.deactivate))]
    private static void BeforeDeactivate(BaseEffect __instance) => ConstructedRainClouds.Forget(__instance);
}