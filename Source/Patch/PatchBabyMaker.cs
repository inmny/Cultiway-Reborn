using Cultiway.Content;
using HarmonyLib;

namespace Cultiway.Patch;

/// <summary>
/// 在 BabyMaker.makeBaby 之后记录父母关系，构建骑士血脉所需的父母表。
/// 用 Postfix 而非 transpiler：makeBaby 的参数 pParent1/pParent2 即父母，返回值即婴儿，安全且无需改 IL。
/// </summary>
internal class PatchBabyMaker
{
    [HarmonyPostfix, HarmonyPatch(typeof(BabyMaker), nameof(BabyMaker.makeBaby))]
    public static void makeBaby_postfix(Actor __result, Actor pParent1, Actor pParent2)
    {
        if (__result == null) return;
        KnightBloodline.RecordBirth(__result.data.id, pParent1, pParent2);
    }
}
