using System;
using Cultiway.Abstract;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI.Prefab;

/// <summary>法宝编辑器页面中的可复用条目，控制区由页面按需构建。</summary>
public sealed class BaibaoEditorRow : APrefabPreview<BaibaoEditorRow>
{
    private Image _background;
    private Image _icon;
    private Text _title;
    private Text _detail;
    private Button _action;
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
        _background = GetComponent<Image>();
        _icon = transform.Find("Header/Icon").GetComponent<Image>();
        _title = transform.Find("Header/Labels/Title").GetComponent<Text>();
        _detail = transform.Find("Header/Labels/Detail").GetComponent<Text>();
        _action = transform.Find("Header/Action").GetComponent<Button>();
        _controls = transform.Find("Controls");
    }

    public void Setup(string title, string detail, string actionLabel, string actionIcon, bool selected,
        bool interactable, Action action, Sprite icon = null)
    {
        Init();
        ClearControls();
        SetHeight(38f);
        _title.text = title;
        _detail.text = detail;
        _icon.sprite = icon ?? SpriteTextureLoader.getSprite(BaibaoUiIcons.Composition);
        _icon.preserveAspect = true;
        _background.color = selected ? BaibaoUiFactory.SelectionColor : Color.white;

        bool showAction = action != null || !string.IsNullOrEmpty(actionLabel);
        _action.gameObject.SetActive(showAction);
        if (!showAction) return;
        _action.interactable = interactable;
        _action.onClick.RemoveAllListeners();
        if (action != null) _action.onClick.AddListener(action.Invoke);
        WanfaUiFactory.SetButtonIcon(_action, actionIcon);
        BaibaoUiFactory.SetButtonLabel(_action, actionLabel);
    }

    public Transform UseControls(float height)
    {
        Init();
        _controls.gameObject.SetActive(true);
        WanfaUiFactory.SetLayout(_controls, 312f, height);
        SetHeight(38f + height);
        return _controls;
    }

    public void SetHeight(float height)
    {
        WanfaUiFactory.SetLayout(transform, 320f, height);
    }

    private void ClearControls()
    {
        for (int i = _controls.childCount - 1; i >= 0; i--)
            UnityEngine.Object.DestroyImmediate(_controls.GetChild(i).gameObject);
        _controls.gameObject.SetActive(false);
    }

    private static void _init()
    {
        GameObject root = WanfaUiFactory.CreateLayout(ModClass.I.PrefabLibrary, nameof(BaibaoEditorRow), false,
            320f, 38f, 0f);
        Image background = root.AddComponent<Image>();
        background.sprite = SpriteTextureLoader.getSprite("ui/special/windowInnerSliced");
        background.type = Image.Type.Sliced;
        GameObject header = WanfaUiFactory.CreateLayout(root.transform, "Header", true, 312f, 38f, 3f);
        header.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(6, 4, 2, 2);
        GameObject iconObject = new("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        iconObject.transform.SetParent(header.transform, false);
        WanfaUiFactory.SetLayout(iconObject.transform, 30f, 30f);
        GameObject labels = WanfaUiFactory.CreateLayout(header.transform, "Labels", false, 184f, 34f, 0f);
        WanfaUiFactory.CreateText(labels.transform, "Title", string.Empty, 184f, 18f, 8, TextAnchor.MiddleLeft,
            FontStyle.Bold);
        WanfaUiFactory.CreateText(labels.transform, "Detail", string.Empty, 184f, 16f, 6,
            TextAnchor.MiddleLeft);
        WanfaUiFactory.CreateIconTextButton(header.transform, "Action", BaibaoUiIcons.Select, string.Empty, 82f,
            24f, () => { });
        WanfaUiFactory.CreateLayout(root.transform, "Controls", false, 312f, 0f, 2f);
        Prefab = root.AddComponent<BaibaoEditorRow>();
    }
}
