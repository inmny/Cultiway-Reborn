using System;
using Cultiway.Abstract;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI.Prefab;

/// <summary>法宝编辑器页面中的可复用条目，控制区由页面按需构建。</summary>
public sealed class BaibaoEditorRow : APrefabPreview<BaibaoEditorRow>
{
    private UiListRowChrome _chrome;
    private Image _icon;
    private Text _title;
    private Text _detail;
    private Button _action;
    private Transform _labels;
    private Transform _inlineControls;
    private Transform _controls;

    public Transform Controls
    {
        get
        {
            Init();
            return _controls;
        }
    }

    protected override void OnInit()
    {
        _chrome = UiListRowChrome.From(gameObject);
        _icon = transform.Find("Header/Icon").GetComponent<Image>();
        _title = transform.Find("Header/Labels/Title").GetComponent<Text>();
        _detail = transform.Find("Header/Labels/Detail").GetComponent<Text>();
        _labels = transform.Find("Header/Labels");
        _inlineControls = transform.Find("Header/InlineControls");
        _action = transform.Find("Header/Action").GetComponent<Button>();
        _controls = transform.Find("Controls");
    }

    public void Setup(string title, string detail, string actionLabel, string actionIcon, bool selected,
        bool interactable, Action action, Sprite icon = null)
    {
        Init();
        ClearControls();
        SetHeight(38f);
        UiLayout.SetSize(_labels, 184f, 34f);
        UiLayout.SetSize(_action.transform, 82f, 24f);
        _title.text = title;
        _detail.text = detail;
        _icon.sprite = icon ?? SpriteTextureLoader.getSprite(BaibaoUiIcons.Composition);
        _icon.preserveAspect = true;
        UiTooltip.Set(_icon.gameObject, title, detail);
        UiTooltip.Set(_title.gameObject, title, detail);
        _chrome.SetState(selected ? UiControlState.Selected : UiControlState.Normal);

        bool showAction = action != null || !string.IsNullOrEmpty(actionLabel);
        _action.gameObject.SetActive(showAction);
        if (!showAction) return;
        _action.interactable = interactable;
        _action.onClick.RemoveAllListeners();
        if (action != null) _action.onClick.AddListener(action.Invoke);
        UiElements.SetButtonIcon(_action, actionIcon);
        UiElements.SetButtonLabel(_action, actionLabel);
        UiTooltip.Set(_action.gameObject, actionLabel, detail);
    }

    public void SetTooltip(string title, string description, string detail = null)
    {
        Init();
        UiTooltip.Set(_icon.gameObject, title, description, detail);
        UiTooltip.Set(_title.gameObject, title, description, detail);
    }

    public void SetActionTooltip(string title, string description)
    {
        Init();
        if (_action.gameObject.activeSelf) UiTooltip.Set(_action.gameObject, title, description);
    }

    public Transform UseInlineControls(float width, float actionWidth)
    {
        Init();
        float availableWidth = actionWidth > 0f ? 263f : 266f;
        float labelsWidth = Mathf.Max(72f, availableWidth - width - actionWidth);
        UiLayout.SetSize(_labels, labelsWidth, 34f);
        UiLayout.SetSize(_inlineControls, width, 30f);
        _inlineControls.gameObject.SetActive(true);
        if (_action.gameObject.activeSelf) UiLayout.SetSize(_action.transform, actionWidth, 24f);
        return _inlineControls;
    }

    public Transform UseControls(float height)
    {
        Init();
        _controls.gameObject.SetActive(true);
        UiLayout.SetSize(_controls, 312f, height);
        SetHeight(38f + height);
        return _controls;
    }

    public void SetHeight(float height)
    {
        UiLayout.SetSize(transform, 320f, height);
    }

    private void ClearControls()
    {
        for (int i = _controls.childCount - 1; i >= 0; i--)
            UnityEngine.Object.DestroyImmediate(_controls.GetChild(i).gameObject);
        for (int i = _inlineControls.childCount - 1; i >= 0; i--)
            UnityEngine.Object.DestroyImmediate(_inlineControls.GetChild(i).gameObject);
        _controls.gameObject.SetActive(false);
        _inlineControls.gameObject.SetActive(false);
    }

    private static void _init()
    {
        GameObject root = UiLayout.Create(ModClass.I.PrefabLibrary, nameof(BaibaoEditorRow), false,
            320f, 38f, 0f);
        UiListRowChrome.Attach(root, false);
        GameObject header = UiLayout.Create(root.transform, "Header", true, 312f, 38f, 3f);
        header.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(6, 4, 2, 2);
        GameObject iconObject = new("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        iconObject.transform.SetParent(header.transform, false);
        UiLayout.SetSize(iconObject.transform, 30f, 30f);
        GameObject labels = UiLayout.Create(header.transform, "Labels", false, 184f, 34f, 0f);
        UiElements.CreateText(labels.transform, "Title", string.Empty, 184f, 18f, 8, TextAnchor.MiddleLeft,
            FontStyle.Bold);
        UiElements.CreateText(labels.transform, "Detail", string.Empty, 184f, 16f, 6,
            TextAnchor.MiddleLeft);
        GameObject inlineControls = UiLayout.Create(header.transform, "InlineControls", true, 0f, 30f, 2f,
            TextAnchor.MiddleCenter);
        inlineControls.SetActive(false);
        UiElements.CreateIconTextButton(header.transform, "Action", UiIcons.Select, string.Empty, 82f,
            24f, () => { });
        UiLayout.Create(root.transform, "Controls", false, 312f, 0f, 2f);
        Prefab = root.AddComponent<BaibaoEditorRow>();
    }
}
