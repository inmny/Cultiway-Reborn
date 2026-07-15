using System;
using Cultiway.Abstract;
using Cultiway.Core.SkillLibV3;
using Cultiway.UI.Components;
using Friflo.Engine.ECS;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI.Prefab;

/// <summary>
/// 魔网浏览器左栏的池化行，可切换为分组标题、法术条目或分页控制。
/// </summary>
public sealed class MagicWebBrowserRow : APrefabPreview<MagicWebBrowserRow>
{
    private const string GroupBackgroundPath = UiResources.Button;
    private const string RowBackgroundPath = UiResources.WindowInner;

    // 左栏宽 258，扣除 Viewport 与 Content 各 2 像素的左右内边距后，子项可用宽度为 250。
    private const float RowWidth = 250f;

    private Image _background;
    private Image _entrySurface;
    private Button _rowButton;
    private GameObject _branch;
    private RectTransform _branchVertical;
    private Image _arrow;
    private Image _icon;
    private Transform _labels;
    private Text _name;
    private Text _detail;
    private Text _meta;
    private Button _previous;
    private Button _next;

    protected override void OnInit()
    {
        _background = GetComponent<Image>();
        _entrySurface = transform.Find("EntrySurface").GetComponent<Image>();
        _rowButton = GetComponent<Button>();
        _branch = transform.Find("Branch").gameObject;
        _branchVertical = transform.Find("Branch/Vertical").GetComponent<RectTransform>();
        _arrow = transform.Find("Arrow").GetComponent<Image>();
        _icon = transform.Find("Icon").GetComponent<Image>();
        _labels = transform.Find("Labels");
        _name = transform.Find("Labels/Name").GetComponent<Text>();
        _detail = transform.Find("Labels/Detail").GetComponent<Text>();
        _meta = transform.Find("Meta").GetComponent<Text>();
        _previous = transform.Find("Previous").GetComponent<Button>();
        _next = transform.Find("Next").GetComponent<Button>();
    }

    public void SetupGroup(string name, int count, bool expanded, Action open)
    {
        Init();
        UiLayout.SetSize(transform, RowWidth, 24f);
        UiLayout.SetSize(_labels, 194f, 22f);
        UiLayout.SetSize(_meta.transform, 34f, 22f);
        SetMode(showArrow: true, showIcon: false, showPager: false, showBranch: false);
        _arrow.sprite = UiResources.GetSprite(UiIcons.Next);
        _arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, expanded ? 90f : 0f);
        _name.text = name;
        _detail.text = string.Empty;
        _meta.text = count.ToString();
        _background.sprite = UiResources.GetSprite(GroupBackgroundPath);
        _background.color = Color.white;
        _entrySurface.gameObject.SetActive(false);
        SetRowAction(open);
    }

    public void SetupEntry(Entity container, Sprite icon, string name, string detail, string meta, bool selected,
        bool lastInGroup, Action select)
    {
        Init();
        UiLayout.SetSize(transform, RowWidth, 38f);
        UiLayout.SetSize(_labels, 150f, 34f);
        UiLayout.SetSize(_meta.transform, 44f, 22f);
        SetMode(showArrow: false, showIcon: true, showPager: false, showBranch: true);
        SetBranch(lastInGroup);
        _icon.sprite = icon;
        _name.text = name;
        _detail.text = detail;
        _meta.text = meta;
        _background.sprite = UiResources.GetSprite(RowBackgroundPath);
        _background.color = Color.clear;
        _entrySurface.color = selected
            ? UiTheme.Current.Palette.Selected
            : new Color(0.38f, 0.4f, 0.34f, 0.56f);
        _entrySurface.gameObject.SetActive(true);
        SetRowAction(select);
        UiTooltip.Set(_icon.gameObject, () => SkillTooltip.Show(_icon.gameObject, container));
    }

    public void SetupPager(int page, int pageCount, Action previous, Action next)
    {
        Init();
        UiLayout.SetSize(transform, RowWidth, 24f);
        UiLayout.SetSize(_labels, 170f, 22f);
        SetMode(showArrow: false, showIcon: false, showPager: true, showBranch: true);
        SetBranch(true);
        _name.text = string.Format("Cultiway.MagicWeb.UI.Format.Page".Localize(), page + 1, pageCount);
        _detail.text = string.Empty;
        _meta.text = string.Empty;
        _background.sprite = UiResources.GetSprite(RowBackgroundPath);
        _background.color = Color.clear;
        _entrySurface.color = new Color(0.36f, 0.37f, 0.32f, 0.48f);
        _entrySurface.gameObject.SetActive(true);
        _rowButton.interactable = false;
        _rowButton.onClick.RemoveAllListeners();
        SetPagerButton(_previous, page > 0, previous);
        SetPagerButton(_next, page + 1 < pageCount, next);
    }

    private void SetMode(bool showArrow, bool showIcon, bool showPager, bool showBranch)
    {
        _branch.SetActive(showBranch);
        _arrow.gameObject.SetActive(showArrow);
        _icon.gameObject.SetActive(showIcon);
        _meta.gameObject.SetActive(!showPager);
        _previous.gameObject.SetActive(showPager);
        _next.gameObject.SetActive(showPager);
        _detail.gameObject.SetActive(showIcon);
    }

    private void SetBranch(bool lastInGroup)
    {
        _branchVertical.anchorMin = lastInGroup ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, 0f);
        _branchVertical.anchorMax = new Vector2(0.5f, 1f);
        _branchVertical.offsetMin = new Vector2(-0.5f, 0f);
        _branchVertical.offsetMax = new Vector2(0.5f, 0f);
    }

    private void SetRowAction(Action action)
    {
        _rowButton.interactable = true;
        _rowButton.onClick.RemoveAllListeners();
        _rowButton.onClick.AddListener(action.Invoke);
    }

    private static void SetPagerButton(Button button, bool interactable, Action action)
    {
        button.interactable = interactable;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action.Invoke);
    }

    private static void _init()
    {
        var obj = UiLayout.Create(ModClass.I.PrefabLibrary, nameof(MagicWebBrowserRow), true, RowWidth,
            38f, 3f);
        var background = obj.AddComponent<Image>();
        background.sprite = UiResources.GetSprite(RowBackgroundPath);
        background.type = Image.Type.Sliced;
        var button = obj.AddComponent<Button>();
        button.targetGraphic = background;

        var entrySurface = new GameObject("EntrySurface", typeof(RectTransform), typeof(Image),
            typeof(LayoutElement));
        entrySurface.transform.SetParent(obj.transform, false);
        entrySurface.GetComponent<LayoutElement>().ignoreLayout = true;
        UiLayout.Stretch(entrySurface.GetComponent<RectTransform>(), 16f, 0f, 1f, 1f);
        var entrySurfaceImage = entrySurface.GetComponent<Image>();
        entrySurfaceImage.sprite = UiResources.GetSprite(RowBackgroundPath);
        entrySurfaceImage.type = Image.Type.Sliced;
        entrySurfaceImage.raycastTarget = false;

        var branch = new GameObject("Branch", typeof(RectTransform), typeof(LayoutElement));
        branch.transform.SetParent(obj.transform, false);
        UiLayout.SetSize(branch.transform, 16f, 34f);
        CreateBranchLine(branch.transform, "Vertical", new Vector2(0.5f, 0f), new Vector2(0.5f, 1f),
            new Vector2(-0.5f, 0f), new Vector2(0.5f, 0f));
        CreateBranchLine(branch.transform, "Horizontal", new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(0f, -0.5f), new Vector2(0f, 0.5f));

        var arrow = new GameObject("Arrow", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        arrow.transform.SetParent(obj.transform, false);
        UiLayout.SetSize(arrow.transform, 14f, 14f);
        arrow.GetComponent<Image>().preserveAspect = true;

        var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        icon.transform.SetParent(obj.transform, false);
        UiLayout.SetSize(icon.transform, 30f, 30f);
        icon.GetComponent<Image>().preserveAspect = true;

        var labels = UiLayout.Create(obj.transform, "Labels", false, 160f, 34f, 0f);
        UiElements.CreateText(labels.transform, "Name", string.Empty, 160f, 18f, 7, TextAnchor.MiddleLeft,
            FontStyle.Bold);
        UiElements.CreateText(labels.transform, "Detail", string.Empty, 160f, 16f, 6);
        UiElements.CreateText(obj.transform, "Meta", string.Empty, 34f, 22f, 6, TextAnchor.MiddleRight);
        UiElements.CreateIconButton(obj.transform, "Previous", UiIcons.Previous, 22f, 22f, () => { });
        UiElements.CreateIconButton(obj.transform, "Next", UiIcons.Next, 22f, 22f, () => { });
        Prefab = obj.AddComponent<MagicWebBrowserRow>();
    }

    private static void CreateBranchLine(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        var line = new GameObject(name, typeof(RectTransform), typeof(Image));
        line.transform.SetParent(parent, false);
        var rect = line.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        var image = line.GetComponent<Image>();
        image.color = new Color(1f, 0.82f, 0.18f, 0.72f);
        image.raycastTarget = false;
    }
}
