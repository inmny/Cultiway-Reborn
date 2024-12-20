using System;
using System.Text;
using Cultiway.Content.CultisysComponents;
using Cultiway.Core;
using Cultiway.Core.Components;
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
    private static Action<CityExtend, StringBuilder> info_display_func;

    public static void RegisterInfoDisplay(Action<CityExtend, StringBuilder> func)
    {
        info_display_func += func;
    }

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
        }

        StringBuilder sb = new();
        sb.AppendLine(__instance.city.data.id);
        CityExtend city_extend = __instance.city.GetExtend();

        var items = city_extend.GetSpecialItems();
        sb.AppendLine("Special Items:");
        var idx = 0;
        foreach (SpecialItem item in items)
        {
            Entity item_entity = item.self;
            sb.Append($"[{idx++}] {item_entity}: ");
            if (item_entity.TryGetComponent(out ItemShape shape))
            {
                sb.Append(shape.shape_id);
                sb.Append('\t');
            }

            if (item_entity.TryGetComponent(out Jindan jindan))
            {
                sb.Append(jindan.jindan_type);
                sb.Append('\t');
            }

            if (item_entity.TryGetComponent(out XianBase xian_base))
            {
                sb.Append(xian_base);
                sb.Append('\t');
            }

            if (item_entity.TryGetComponent(out ElementRoot element_root))
            {
                sb.Append(element_root.Type.id);
                sb.Append('\n');
            }
        }

        info_display_func?.Invoke(city_extend, sb);
        info_text.text = sb.ToString();
    }
}