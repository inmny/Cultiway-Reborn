using System;
using System.Text;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.UI;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using HarmonyLib;
using NeoModLoader.api.attributes;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.Patch;

internal static class PatchCityWindow
{
    private static bool                              _initialized;
    private static Text                              info_text;

    [HarmonyPrefix, Hotfixable]
    [HarmonyPatch(typeof(CityWindow), nameof(CityWindow.OnEnable))]
    private static void OnEnable_prefix(CityWindow __instance)
    {
        if (__instance.city == null) return;
        if (!_initialized)
        {
            _initialized = true;
            var obj = new GameObject("TempInfo", typeof(Text), typeof(ContentSizeFitter));
            obj.transform.SetParent(__instance.transform.Find("Background"));
            obj.transform.localPosition = new Vector3(250, 0);
            obj.transform.localScale = Vector3.one;
            obj.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            info_text = obj.GetComponent<Text>();
            info_text.font = LocalizedTextManager.currentFont;
            info_text.fontSize = 6;

            if (__instance.GetComponent<AdditionCityWindow>() == null)
                __instance.gameObject.AddComponent<AdditionCityWindow>();
        }

        StringBuilder sb = new();
        sb.AppendLine(__instance.city.data.id);
        info_text.text = sb.ToString();
    }
}