using System.Linq;
using HarmonyLib;

namespace Cultiway.Patch;

/// <summary>保留原版Localize使用的规范化文本键，语言切换后不丢失模组文字。</summary>
internal static class PatchLocalizedTextManager
{
    /// <summary>NML先恢复模组当前语言，再补齐原版字符串扩展所查找的键。</summary>
    [HarmonyPostfix, HarmonyAfter("wbom.nml")]
    [HarmonyPatch(typeof(LocalizedTextManager), nameof(LocalizedTextManager.loadLocalizedText))]
    private static void AfterLoad() => RestoreAliases();

    /// <summary>启动、热重载和语言切换共用同一恢复，不只在首次载入时修改当前字典。</summary>
    internal static void RestoreAliases()
    {
        var texts = LocalizedTextManager.instance._localized_text;
        foreach (string key in texts.Keys.ToArray()) texts[key.Underscore()] = texts[key];
    }
}
