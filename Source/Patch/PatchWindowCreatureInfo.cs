using System;
using System.Text;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.UI;
using Cultiway.Utils.Extension;
using HarmonyLib;
using NeoModLoader.api.attributes;
using NeoModLoader.General.UI.Prefabs;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

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
    [HarmonyPrefix, HarmonyPatch(typeof(UnitWindow), nameof(UnitWindow.OnEnable))]
    private static void OnEnable_prefix(UnitWindow __instance)
    {
        if (!(__instance.actor?.isAlive() ?? false)) return;
        SimpleButton button = Object.Instantiate(SimpleButton.Prefab, __instance.transform.Find("Background"));
        button.transform.localPosition = new Vector3(-250, 0);
        button.transform.localScale = Vector3.one;
        button.Setup(WindowNewCreatureInfo.Show, SpriteTextureLoader.getSprite("cultiway/icons/iconCultivation"));

        Text info_text = null;
        if (!_initialized)
        {
            _initialized = true;
            var obj = new GameObject("TempInfo", typeof(Text), typeof(ContentSizeFitter));
            obj.transform.SetParent(__instance.transform.Find("Background"));
            obj.transform.localPosition = new(250, 0);
            obj.transform.localScale = Vector3.one;
            obj.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            info_text = obj.GetComponent<Text>();
            info_text.font = LocalizedTextManager.current_font;
            info_text.resizeTextForBestFit = true;
            info_text.resizeTextMinSize = 1;
            info_text.resizeTextMaxSize = 8;
        }
        else
        {
            info_text = __instance.transform.Find("Background/TempInfo").GetComponent<Text>();
        }

        var sb = new StringBuilder();
        sb.AppendLine(__instance.actor.data.id.ToString());
        var actor_extend = __instance.actor.GetExtend();
        if (actor_extend.HasElementRoot())
        {
            sb.AppendLine($"灵根: {actor_extend.GetElementRoot().ToString()}");
            sb.AppendLine($"期望修炼倍率: {actor_extend.GetElementRoot().GetStrength()}");
        }
        else
        {
            sb.AppendLine("无灵根");
        }

        if (actor_extend.TryGetComponent(out Qiyun qiyun))
        {
            sb.AppendLine($"气运: {qiyun.Value:F1}/{qiyun.MaxValue:F1}");
        }

        info_display_func?.Invoke(actor_extend, sb);

        var idx = 0;
        foreach (var skill_container_entity in actor_extend.all_skills)
            sb.AppendLine($"技能[{idx++}]: " + skill_container_entity);

        info_text.text = sb.ToString();
    }
}