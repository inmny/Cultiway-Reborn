using System;
using System.Text;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.UI;
using Cultiway.UI.Components;
using Cultiway.UI.Prefab;
using Cultiway.Utils.Extension;
using HarmonyLib;
using NeoModLoader.api.attributes;
using NeoModLoader.General.UI.Prefabs;
using UnityEngine;
using UnityEngine.Events;
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


            var content_master_apprentice_obj = Object.Instantiate(__instance.transform.GetComponentInChildren<UnitGenealogyElement>(true), __instance.transform.Find("Background/Scroll View/Viewport/Content")).gameObject;
            content_master_apprentice_obj.name = "content_master_apprentice";
            Object.DestroyImmediate(content_master_apprentice_obj.GetComponent<UnitGenealogyElement>());
            var content_master_apprentice = content_master_apprentice_obj.AddComponent<UnitMasterApprenticeElement>();
            var vertical_layout_group = content_master_apprentice_obj.GetComponent<VerticalLayoutGroup>();
            vertical_layout_group.childControlHeight = true;
            vertical_layout_group.childControlWidth = false;
            vertical_layout_group.childForceExpandHeight = true;
            vertical_layout_group.childForceExpandWidth = false;
            vertical_layout_group.spacing = 6;
            vertical_layout_group.childAlignment = TextAnchor.UpperCenter;

            // 找到UnitGenealogyElement所在的Transform
            Transform tabsContainer = __instance.transform.Find("Background/Tabs");
            Transform genealogyTabTransform = null;
            int indexToInsert = -1;
            for (int i = 0; i < tabsContainer.childCount; i++)
            {
                var t = tabsContainer.GetChild(i);
                if (t.name.ToLower().Contains("genealogy"))
                {
                    genealogyTabTransform = t;
                    indexToInsert = i;
                    break;
                }
            }
            // 如果没找到，则新entry还是放到最后
            if (indexToInsert < 0)
                indexToInsert = tabsContainer.childCount;

            // 新建master/apprentice entry
            var master_apprentice_entry = Object.Instantiate(__instance.transform.Find("Background/Tabs/Genealogy").GetComponent<WindowMetaTab>(), tabsContainer);
            master_apprentice_entry.name = "MasterApprenticeTab";
            master_apprentice_entry.tab_action = new WindowMetaTabEvent();
            master_apprentice_entry.tab_action.AddListener(new UnityEngine.Events.UnityAction<WindowMetaTab>(tab =>
            {
                __instance.showTab(tab);
            }));
            master_apprentice_entry._tip_button.textOnClick = "tab_master_apprentice";
            master_apprentice_entry._tip_button.textOnClickDescription = "tab_master_apprentice_description";
            master_apprentice_entry._worldtip_text = master_apprentice_entry.getWorldTipText();

            // 移动到有genealogy的位置前面
            master_apprentice_entry.transform.SetSiblingIndex(indexToInsert);

            // 在插入点之前，查找并移除一个名字包含space的按钮
            for (int i = 0; i < indexToInsert; i++)
            {
                var t = tabsContainer.GetChild(i);
                if (t.name.ToLower().Contains("space"))
                {
                    Object.DestroyImmediate(t.gameObject);
                    break;
                }
            }

            master_apprentice_entry.container = __instance.tabs;
            master_apprentice_entry.tab_elements.RemoveAll(t => t.name.ToLower().StartsWith("content_"));
            __instance.tabs._tabs.Add(master_apprentice_entry);
            __instance.tabs.addTabContent(master_apprentice_entry, content_master_apprentice_obj.transform);
            __instance.tabs.refillTabsWithContent();
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