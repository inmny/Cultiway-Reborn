using System.Collections.Generic;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.UI.Prefab;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Cultiway.UI;

internal sealed class PossessionStatusEffectsUi : MonoBehaviour
{
    private const string RootName = "CultiwayPossessionStatusEffectsUi";
    private const float IconSize = 28f;
    private const float IconStep = 27f;
    private const float ColumnStep = 27f;
    private const int MaxRowsPerColumn = 9;

    private static PossessionStatusEffectsUi _instance;

    private readonly List<PossessionStatusEffectIcon> _icons = new();
    private PossessionUI _boundUi;
    private RectTransform _rootRect;
    private CanvasGroup _canvasGroup;
    private int _visibleCount;

    internal static void Ensure()
    {
        if (_instance != null) return;

        var root = new GameObject(RootName, typeof(RectTransform), typeof(CanvasGroup),
            typeof(PossessionStatusEffectsUi));
        var parent = CanvasMain.instance?.canvas_ui?.transform;
        if (parent != null)
        {
            root.transform.SetParent(parent, false);
        }
    }

    private void Awake()
    {
        _instance = this;
        _rootRect = GetComponent<RectTransform>();
        _canvasGroup = GetComponent<CanvasGroup>();
        ConfigureRootRect();
        SetVisible(false);
    }

    private void Update()
    {
        if (!EnsureBound() || !TryGetControlledActor(out Actor actor))
        {
            HideAllIcons();
            return;
        }

        int index = 0;
        foreach (Status status in actor.getStatuses())
        {
            if (status == null || status.is_finished) continue;

            var icon = EnsureIcon(index);
            icon.Setup(status);
            PlaceIcon(icon, index);
            index++;
        }

        ActorExtend actorExtend = actor.GetExtend();
        if (actorExtend != null)
        {
            foreach (Entity statusEntity in actorExtend.GetStatuses())
            {
                if (statusEntity.IsNull || !statusEntity.HasComponent<StatusComponent>()) continue;

                var icon = EnsureIcon(index);
                icon.Setup(statusEntity);
                PlaceIcon(icon, index);
                index++;
            }
        }

        HideIconsFrom(index);
        _visibleCount = index;
        ApplyRootSize(index);
        SetVisible(index > 0);
    }

    private bool EnsureBound()
    {
        if (PossessionUI.instance == null)
        {
            return false;
        }

        if (_boundUi == PossessionUI.instance && transform.parent != null)
        {
            return true;
        }

        Bind(PossessionUI.instance);
        return transform.parent != null;
    }

    private void Bind(PossessionUI ui)
    {
        _boundUi = ui;
        Transform parent = ui.transform.Find("Inner") ?? ui.transform.FindRecursive("Inner") ?? ui.transform;
        transform.SetParent(parent, false);
        ConfigureRootRect();
        transform.SetAsLastSibling();
    }

    private void ConfigureRootRect()
    {
        if (_rootRect == null) return;

        _rootRect.anchorMin = new Vector2(0f, 0f);
        _rootRect.anchorMax = new Vector2(0f, 0f);
        _rootRect.pivot = new Vector2(0f, 0f);
        _rootRect.anchoredPosition = new Vector2(24f, 18f);
        _rootRect.sizeDelta = new Vector2(ColumnStep, IconStep * MaxRowsPerColumn);
    }

    private PossessionStatusEffectIcon EnsureIcon(int index)
    {
        while (_icons.Count <= index)
        {
            PossessionStatusEffectIcon icon = Object.Instantiate(PossessionStatusEffectIcon.Prefab, _rootRect, false);
            icon.gameObject.name = $"cultiway_possession_status_{_icons.Count}";
            icon.Clear();
            _icons.Add(icon);
        }

        return _icons[index];
    }

    private static void PlaceIcon(PossessionStatusEffectIcon icon, int index)
    {
        RectTransform rect = icon.RectTransform;
        if (rect == null) return;

        int column = index / MaxRowsPerColumn;
        int row = index % MaxRowsPerColumn;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(column * ColumnStep + IconStep * 0.5f,
            row * IconStep + IconStep * 0.5f);
        rect.sizeDelta = new Vector2(IconSize, IconSize);
        rect.localScale = Vector3.one;
        icon.transform.SetSiblingIndex(index);
    }

    private void ApplyRootSize(int count)
    {
        if (_rootRect == null) return;

        int columns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(1, count) / (float)MaxRowsPerColumn));
        _rootRect.sizeDelta = new Vector2(columns * ColumnStep, IconStep * MaxRowsPerColumn);
    }

    private void HideIconsFrom(int index)
    {
        for (int i = index; i < _icons.Count; i++)
        {
            _icons[i].Clear();
        }
    }

    private void HideAllIcons()
    {
        if (_visibleCount == 0)
        {
            SetVisible(false);
            return;
        }

        HideIconsFrom(0);
        _visibleCount = 0;
        SetVisible(false);
    }

    private void SetVisible(bool visible)
    {
        if (_canvasGroup == null) return;

        _canvasGroup.alpha = visible ? 1f : 0f;
        _canvasGroup.interactable = visible;
        _canvasGroup.blocksRaycasts = visible;
    }

    private static bool TryGetControlledActor(out Actor actor)
    {
        actor = null;
        if (!ControllableUnit.isControllingUnit()) return false;

        actor = ControllableUnit.getControllableUnit();
        return actor != null && !actor.isRekt();
    }
}
