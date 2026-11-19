using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Content.Territories;
using Cultiway.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.Content.UI.Territories;

/// <summary>地区图层打开时显示名称，复用原版地图文字及对象池。</summary>
public sealed class TerritoryNameLayer : MonoBehaviour
{
    /// <summary>当前名称图层。</summary>
    private static TerritoryNameLayer instance;
    /// <summary>放在工具栏下方的地图文字容器。</summary>
    private RectTransform root;
    /// <summary>复用原版地图名称文字。</summary>
    private MonoObjPool<Text> labels;
    /// <summary>当前地区对应的文字和地图位置。</summary>
    private readonly List<(Text text, Vector3 position)> entries = new();
    /// <summary>已经显示的数据版本。</summary>
    private int revision = -1;
    /// <summary>默认名称随当前语言更新。</summary>
    private string defaultName;

    /// <summary>随内容界面初始化一次。</summary>
    internal static void Initialize() => instance = CanvasMain.instance.canvas_ui.gameObject.AddComponent<TerritoryNameLayer>();

    /// <summary>创建地图名称容器，延后到有地区时获取原版文字模板。</summary>
    private void Awake()
    {
        root = new GameObject("CustomRegionNames", typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(transform, false);
        root.SetAsFirstSibling();
        UiLayout.Stretch(root);
    }

    /// <summary>只在当前地区图层显示；形状和名称变化时重算位置，平时仅跟随镜头。</summary>
    private void LateUpdate()
    {
        bool visible = Config.game_loaded && !Config.worldLoading
                       && ModClass.I.CustomMapModeManager.CurrMapMode == MapModes.Territories;
        root.gameObject.SetActive(visible);
        if (!visible) return;
        string format = LocalizedTextManager.getText("Cultiway.Territories.DefaultName");
        if (revision != TerritoryMapOverlay.Revision || defaultName != format)
        {
            Rebuild();
            revision = TerritoryMapOverlay.Revision;
            defaultName = format;
        }
        Canvas canvas = CanvasMain.instance.canvas_ui;
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        foreach (var entry in entries)
        {
            bool inside = World.world.move_camera.isWithinCameraViewNotPowerBar(entry.position);
            entry.text.gameObject.SetActive(inside);
            if (!inside) continue;
            Vector3 screen = World.world.camera.WorldToScreenPoint(entry.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, uiCamera, out Vector2 local);
            entry.text.rectTransform.anchoredPosition = local;
        }
    }

    /// <summary>为每个地区复用一段原版文字，并选取靠近几何中心的实际地区格子作为位置。</summary>
    private void Rebuild()
    {
        labels ??= new MonoObjPool<Text>(World.world.nameplate_manager.prefab._text_name, root,
            create_action: PrepareLabel);
        labels.ResetToStart();
        entries.Clear();
        foreach (CustomTerritory region in TerritoryService.Regions)
        {
            Vector3 center = Vector3.zero;
            foreach (int id in region.Zones) center += World.world.zone_calculator.getZoneByID(id).centerTile.posV3;
            center /= region.Zones.Count;
            Vector3 anchor = center;
            float best = float.MaxValue;
            foreach (int id in region.Zones)
            {
                Vector3 point = World.world.zone_calculator.getZoneByID(id).centerTile.posV3;
                float distance = (point - center).sqrMagnitude;
                if (distance >= best) continue;
                anchor = point;
                best = distance;
            }
            Text label = labels.GetNext();
            label.text = TerritoryService.GetName(region);
            label.supportRichText = false;
            label.raycastTarget = false;
            label.alignment = TextAnchor.MiddleCenter;
            label.font = UiTheme.Current.Font;
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            label.rectTransform.sizeDelta = new Vector2(240, 24);
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 6;
            label.resizeTextMaxSize = 8;
            entries.Add((label, anchor + new Vector3(0, 4)));
        }
        labels.ClearUnsed();
    }

    /// <summary>原版文字下还带有收藏星标；地区只保留文字，不复制单位附属图标。</summary>
    /// <param name="label">刚从原版模板复制的地区名称。</param>
    private static void PrepareLabel(Text label)
    {
        foreach (Transform child in label.transform) child.gameObject.SetActive(false);
    }

    /// <summary>换世界时立即清除旧名称和位置。</summary>
    internal static void ClearWorldState()
    {
        if (instance == null) return;
        instance.labels?.Clear();
        instance.entries.Clear();
        instance.revision = -1;
        instance.root.gameObject.SetActive(false);
    }
}
