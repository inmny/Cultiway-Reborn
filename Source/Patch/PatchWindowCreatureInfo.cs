using System;
using System.Text;
using Cultiway.Core;
using Cultiway.Utils.Extension;
using HarmonyLib;
using NeoModLoader.api.attributes;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.Patch;

internal static class PatchWindowCreatureInfo
{
    private static bool _initialized = false;

    private static Action<ActorExtend, StringBuilder> info_display_func;

    public static void RegisterInfoDisplay(Action<ActorExtend, StringBuilder> func)
    {
        info_display_func += func;
    }

    [Hotfixable]
    [HarmonyPrefix, HarmonyPatch(typeof(WindowCreatureInfo), nameof(WindowCreatureInfo.OnEnable))]
    private static void OnEnable_prefix(WindowCreatureInfo __instance)
    {
        if (!(__instance.actor?.isAlive() ?? false)) return;

        Text info_text = null;
        if (!_initialized)
        {
            _initialized = true;
            var obj = new GameObject("TempInfo", typeof(Text));
            obj.transform.SetParent(__instance.transform.Find("Background"));
            obj.transform.localPosition = new(250, 0);
            obj.transform.localScale = Vector3.one;
            info_text = obj.GetComponent<Text>();
            info_text.font = LocalizedTextManager.currentFont;
            info_text.resizeTextForBestFit = true;
            info_text.resizeTextMinSize = 1;
            info_text.resizeTextMaxSize = 8;
        }
        else
        {
            info_text = __instance.transform.Find("Background/TempInfo").GetComponent<Text>();
        }

        var sb = new StringBuilder();
        sb.AppendLine(__instance.actor.data.id);
        var actor_extend = __instance.actor.GetExtend();
        if (actor_extend.HasElementRoot())
        {
            sb.AppendLine($"灵根: {actor_extend.GetElementRoot().ToString()}");
        }
        else
        {
            sb.AppendLine("无灵根");
        }

        info_display_func?.Invoke(actor_extend, sb);

        info_text.text = sb.ToString();
    }
}