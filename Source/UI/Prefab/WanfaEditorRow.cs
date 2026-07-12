using System;
using Cultiway.Abstract;
using Cultiway.UI.Components;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI.Prefab;

public sealed class WanfaEditorRow : APrefabPreview<WanfaEditorRow>
{
    public Transform Controls { get; private set; }
    public Transform InlineControls { get; private set; }
    private SkillModifierIcon _modifierIcon;
    private Image _assetIcon;
    private Text _title;
    private Text _detail;
    private Button _action;

    protected override void OnInit()
    {
        _modifierIcon = transform.Find("Header/ModifierIcon").GetComponent<SkillModifierIcon>();
        _assetIcon = transform.Find("Header/AssetIcon").GetComponent<Image>();
        _title = transform.Find("Header/Title").GetComponent<Text>();
        _detail = transform.Find("Header/Detail").GetComponent<Text>();
        InlineControls = transform.Find("Header/InlineControls");
        _action = transform.Find("Header/Action").GetComponent<Button>();
        Controls = transform.Find("Controls");
    }

    internal void Setup(string title, string detail, string actionLabel, bool interactable, Action action,
        string actionIconPath = WanfaUiIcons.Select, SkillModifierTooltipModel modifierIcon = null,
        string assetIconPath = null, string actionTooltipDescription = null)
    {
        Init();
        ClearControls();
        _modifierIcon.gameObject.SetActive(modifierIcon != null);
        if (modifierIcon != null) _modifierIcon.Setup(modifierIcon);
        _assetIcon.gameObject.SetActive(!string.IsNullOrWhiteSpace(assetIconPath));
        if (!string.IsNullOrWhiteSpace(assetIconPath))
        {
            _assetIcon.sprite = SpriteTextureLoader.getSprite(assetIconPath);
            WanfaUiFactory.SetTooltip(_assetIcon.gameObject, title, detail);
        }
        _title.text = title;
        _detail.text = detail;
        _action.gameObject.SetActive(action != null);
        _action.interactable = interactable;
        _action.onClick.RemoveAllListeners();
        if (action != null) _action.onClick.AddListener(action.Invoke);
        if (action != null)
        {
            WanfaUiFactory.SetButtonIcon(_action, actionIconPath);
            WanfaUiFactory.SetTooltip(_action.gameObject, actionLabel,
                actionTooltipDescription == null ? detail : actionTooltipDescription);
        }
        SetHeight(32f);
    }

    public void SetHeight(float height)
    {
        WanfaUiFactory.SetLayout(transform, 500f, height);
    }

    public void ClearControls()
    {
        for (var i = Controls.childCount - 1; i >= 0; i--)
        {
            var child = Controls.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
        for (var i = InlineControls.childCount - 1; i >= 0; i--)
        {
            var child = InlineControls.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
        InlineControls.gameObject.SetActive(false);
        WanfaUiFactory.SetLayout(_detail.transform, 324f, 28f);
    }

    public Transform UseInlineControls(float width)
    {
        WanfaUiFactory.SetLayout(_detail.transform, 320f - width, 28f);
        WanfaUiFactory.SetLayout(InlineControls, width, 28f);
        InlineControls.gameObject.SetActive(true);
        return InlineControls;
    }

    private static void _init()
    {
        var obj = WanfaUiFactory.CreateLayout(ModClass.I.PrefabLibrary, nameof(WanfaEditorRow), false, 500f, 32f, 2f);
        var background = obj.AddComponent<Image>();
        background.sprite = SpriteTextureLoader.getSprite("ui/special/windowInnerSliced");
        background.type = Image.Type.Sliced;
        var header = WanfaUiFactory.CreateLayout(obj.transform, "Header", true, 500f, 28f, 4f);
        var modifierIcon = SkillModifierIcon.Create(header.transform, "ModifierIcon", 24f);
        modifierIcon.gameObject.SetActive(false);
        var assetIcon = new GameObject("AssetIcon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        assetIcon.transform.SetParent(header.transform, false);
        WanfaUiFactory.SetLayout(assetIcon.transform, 24f, 24f);
        var assetImage = assetIcon.GetComponent<Image>();
        assetImage.preserveAspect = true;
        assetIcon.SetActive(false);
        WanfaUiFactory.CreateText(header.transform, "Title", string.Empty, 112f, 28f, 8, TextAnchor.MiddleLeft,
            FontStyle.Bold);
        WanfaUiFactory.CreateText(header.transform, "Detail", string.Empty, 324f, 28f, 6);
        var inlineControls = WanfaUiFactory.CreateLayout(header.transform, "InlineControls", true, 0f, 28f, 2f,
            TextAnchor.MiddleCenter);
        inlineControls.SetActive(false);
        WanfaUiFactory.CreateIconButton(header.transform, "Action", WanfaUiIcons.Select, 28f, 22f, () => { });
        WanfaUiFactory.CreateLayout(obj.transform, "Controls", false, 500f, 0f, 2f);
        Prefab = obj.AddComponent<WanfaEditorRow>();
    }
}
