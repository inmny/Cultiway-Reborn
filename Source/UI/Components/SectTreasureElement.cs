using System.Collections;
using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Content.Extensions;
using Cultiway.Core;
using Cultiway.Core.Libraries;
using Cultiway.UI.Prefab;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using strings;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Cultiway.UI.Components;

/// <summary>
/// 宗门藏宝阁标签页，按特殊物品分类显示宗门所有的库藏与外借物品。
/// </summary>
internal class SectTreasureElement : WindowMetaElement<Sect, SectData>
{
    private const float ContentWidth = 214f;
    private const float ShowStepTime = 0.015f;
    private const string TreasureIconPath = "ui/icons/iconFavoriteItems";

    private Transform _typeTabsContainer;
    private Transform _gridRoot;
    private MonoObjPool<SectTreasureItemDisplay> _itemPool;
    private Text _capacityText;
    private GameObject _emptyMessage;
    private SpecialItemCategoryAsset _selectedCategory;
    private bool _initialized;

    internal void Initialize(Transform typeTabsContainer)
    {
        if (_initialized) return;

        _initialized = true;
        _typeTabsContainer = typeTabsContainer;
        SetupLayout();
        RemoveVanillaBookElements();
        SetupTitle();
        SetupTypeTabs();
        _capacityText = CreateCapacityText(transform);
        _gridRoot = CreateItemGrid(transform);
        _itemPool = new MonoObjPool<SectTreasureItemDisplay>(SectTreasureItemDisplay.Prefab, _gridRoot);
        _emptyMessage = CreateEmptyMessage(transform);
    }

    public override IEnumerator showContent()
    {
        Initialize(_typeTabsContainer);

        Sect sect = meta_object;
        if (sect == null || sect.isRekt()) yield break;

        string capacityLabel = "Cultiway.Sect.Treasure.Capacity".Localize();
        _capacityText.text = $"{capacityLabel}: {SectTreasureRules.GetTreasureUsedCapacity(sect)} / {SectTreasureRules.GetTreasureCapacity(sect)}";
        List<Entity> items = GetVisibleItems(sect);
        bool hasItems = items.Count > 0;
        _emptyMessage.SetActive(!hasItems);
        _gridRoot.gameObject.SetActive(hasItems);

        for (int i = 0; i < items.Count; i++)
        {
            _itemPool.GetNext().Setup(items[i]);
            yield return new WaitForSecondsRealtime(ShowStepTime);
        }
    }

    public override void clear()
    {
        _itemPool?.Clear();
        _gridRoot.SetActiveIfPresent(false);
        if (_emptyMessage != null) _emptyMessage.SetActive(false);
        base.clear();
    }

    private List<Entity> GetVisibleItems(Sect sect)
    {
        List<Entity> items = new();
        foreach (Entity item in sect.GetTreasures())
        {
            SpecialItemCategoryAsset category = ModClass.L.SpecialItemCategoryLibrary.Resolve(item);
            if (_selectedCategory == null || category == _selectedCategory)
            {
                items.Add(item);
            }
        }

        items.Sort((left, right) =>
        {
            SpecialItemCategoryAsset leftCategory = ModClass.L.SpecialItemCategoryLibrary.Resolve(left);
            SpecialItemCategoryAsset rightCategory = ModClass.L.SpecialItemCategoryLibrary.Resolve(right);
            int categoryOrder = leftCategory.order.CompareTo(rightCategory.order);
            return categoryOrder != 0 ? categoryOrder : SectTreasureRules.GetTreasureValue(right).CompareTo(SectTreasureRules.GetTreasureValue(left));
        });
        return items;
    }

    private void SetupLayout()
    {
        VerticalLayoutGroup layout = GetComponent<VerticalLayoutGroup>() ?? gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = false;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.spacing = 4f;

        ContentSizeFitter fitter = GetComponent<ContentSizeFitter>() ?? gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
    }

    private void RemoveVanillaBookElements()
    {
        DestroyIfPresent("Books Grid");
        DestroyIfPresent("content_books_no_items");
        DestroyIfPresent("Title Books");
    }

    private void DestroyIfPresent(string objectName)
    {
        Transform target = transform.FindRecursive(objectName);
        if (target != null) Object.DestroyImmediate(target.gameObject);
    }

    private void SetupTitle()
    {
        Transform titleContainer = transform.FindRecursive("tab_title_container_books");
        titleContainer.Find("title_tab")?.GetComponent<LocalizedText>()?.setKeyAndUpdate("Cultiway.Sect.TreasurePavilion");
        Sprite icon = SpriteTextureLoader.getSprite(TreasureIconPath);
        SetTitleIcon(titleContainer, "icon_left", icon);
        SetTitleIcon(titleContainer, "icon_right", icon);
    }

    private void SetupTypeTabs()
    {
        TabTogglesGroup group = _typeTabsContainer.GetComponent<TabTogglesGroup>();
        group.tryAddButton(TreasureIconPath, "Cultiway.Sect.Treasure.All", new TabToggleAction(refresh), new TabToggleAction(() => _selectedCategory = null));
        foreach (SpecialItemCategoryAsset category in ModClass.L.SpecialItemCategoryLibrary.GetOrdered())
        {
            SpecialItemCategoryAsset selectedCategory = category;
            group.tryAddButton(category.iconPath, category.nameKey, new TabToggleAction(refresh), new TabToggleAction(() => _selectedCategory = selectedCategory));
        }

        _typeTabsContainer.Find("Cultiway.Sect.Treasure.All")?.GetComponentInChildren<TabToggle>(true)?.select();
    }

    private static Text CreateCapacityText(Transform parent)
    {
        GameObject row = new("treasure_capacity", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        row.transform.localScale = Vector3.one;

        Image background = row.GetComponent<Image>();
        background.sprite = SpriteTextureLoader.getSprite("ui/special/windowInnerSliced");
        background.type = Image.Type.Sliced;

        GameObject label = new("Label", typeof(RectTransform), typeof(Text), typeof(Shadow));
        label.transform.SetParent(row.transform, false);
        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        Text text = label.GetComponent<Text>();
        text.font = UIUtils.GetCurrentFont();
        text.fontSize = 7;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;

        Shadow shadow = label.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
        shadow.effectDistance = new Vector2(0.5f, -0.5f);

        LayoutElement layout = row.GetComponent<LayoutElement>();
        layout.preferredWidth = ContentWidth;
        layout.preferredHeight = 15f;
        return text;
    }

    private static Transform CreateItemGrid(Transform parent)
    {
        GameObject gridObject = new("treasure_item_grid", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement));
        gridObject.transform.SetParent(parent, false);
        gridObject.transform.localScale = Vector3.one;

        GridLayoutGroup grid = gridObject.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(18f, 18f);
        grid.spacing = new Vector2(3f, 3f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 10;
        grid.childAlignment = TextAnchor.UpperCenter;

        ContentSizeFitter fitter = gridObject.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        LayoutElement layout = gridObject.GetComponent<LayoutElement>();
        layout.preferredWidth = ContentWidth;
        return gridObject.transform;
    }

    private static GameObject CreateEmptyMessage(Transform parent)
    {
        GameObject message = new("content_sect_treasure_empty", typeof(RectTransform), typeof(Text), typeof(Shadow), typeof(LayoutElement));
        message.transform.SetParent(parent, false);

        Text text = message.GetComponent<Text>();
        text.font = UIUtils.GetCurrentFont();
        text.fontSize = 7;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;
        text.text = "Cultiway.Sect.Treasure.Empty".Localize();

        Shadow shadow = message.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
        shadow.effectDistance = new Vector2(0.5f, -0.5f);

        LayoutElement layout = message.GetComponent<LayoutElement>();
        layout.preferredWidth = ContentWidth;
        layout.preferredHeight = 20f;
        return message;
    }

    private static void SetTitleIcon(Transform titleContainer, string childName, Sprite icon)
    {
        Transform iconTransform = titleContainer.Find(childName);
        if (iconTransform != null)
        {
            iconTransform.GetComponent<Image>().sprite = icon;
        }
    }
}
