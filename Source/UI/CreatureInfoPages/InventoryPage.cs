using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
using Cultiway.Content.Utils;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.Libraries;
using Cultiway.UI.Prefab;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Cultiway.UI.CreatureInfoPages;

/// <summary>
/// 单位信息窗口的背包页，按特殊物品分类提供筛选、排序和滚动浏览。
/// </summary>
public class InventoryPage : MonoBehaviour, IWorldBoundCreatureInfoPage
{
    private const string AllItemsIconPath = "ui/icons/iconFavoriteItems";
    private const float FilterBarHeight = 24f;
    private const float FilterBarSpacing = 4f;
    private const int ColumnCount = 7;

    private static readonly Vector2 CellSpacing = new(2f, 2f);

    private readonly List<CategoryTab> _categoryButtons = new();
    private readonly UiSegmentedTabs _categoryTabs = new();

    private Actor _actor;
    private ActorExtend _actorExtend;
    private Text _countText;
    private UiEmptyState _emptyState;
    private RectTransform _filterBar;
    private MonoObjPool<InventoryItemDisplay> _itemPool;
    private UiScrollPane _itemsPane;
    private SpecialItemCategoryAsset _selectedCategory;

    /// <summary>
    /// 为背包页构建固定筛选栏和滚动物品网格。
    /// </summary>
    public static void Setup(CreatureInfoPage page)
    {
        InventoryPage inventoryPage = page.gameObject.AddComponent<InventoryPage>();
        inventoryPage.Build(page.transform);
    }

    /// <summary>
    /// 将当前选中角色绑定到背包页并刷新物品快照。
    /// </summary>
    public static void Show(CreatureInfoPage page, Actor actor)
    {
        page.GetComponent<InventoryPage>().Bind(actor);
    }

    /// <summary>
    /// 一次性创建背包页的视觉结构和池化容器。
    /// </summary>
    private void Build(Transform parent)
    {
        GameObject root = new("Inventory Root", typeof(RectTransform));
        root.transform.SetParent(parent, false);
        UiLayout.Stretch(root.GetComponent<RectTransform>());

        Transform filterBar = CreateFilterBar(root.transform);
        _filterBar = filterBar.GetComponent<RectTransform>();
        CreateCategoryButton(
            filterBar,
            "All",
            AllItemsIconPath,
            "Cultiway.Inventory.Filter.All".Localize(),
            "Cultiway.Inventory.Filter.All.Description".Localize(),
            null);

        foreach (SpecialItemCategoryAsset category in ModClass.L.SpecialItemCategoryLibrary.GetOrdered())
        {
            CreateCategoryButton(
                filterBar,
                category.id,
                category.iconPath,
                category.GetName(),
                category.descriptionKey.Localize(),
                category);
        }

        CreateFlexibleSpacer(filterBar);
        _countText = UiElements.CreateText(filterBar, "Count", string.Empty, 58f, 22f, 6,
            TextAnchor.MiddleRight, FontStyle.Bold);
        UiTooltip.Set(
            _countText.gameObject,
            "Cultiway.Inventory.Count".Localize(),
            "Cultiway.Inventory.Count.Description".Localize());

        _itemsPane = UiScrollPane.CreateGrid(
            root.transform,
            "Items",
            1f,
            1f,
            ColumnCount,
            new Vector2(InventoryItemDisplay.CellSize, InventoryItemDisplay.CellSize),
            CellSpacing);
        UiLayout.Stretch(
            _itemsPane.Root,
            0f,
            0f,
            0f,
            FilterBarHeight + FilterBarSpacing);
        _itemsPane.AttachOriginalScrollbar(WindowNewCreatureInfo.PageScrollbarTemplate);
        _itemsPane.SetSurface(UiSurface.WindowEmpty, UiTheme.Current.Metrics.SpacingSm);
        GridLayoutGroup itemGrid = _itemsPane.Content.GetComponent<GridLayoutGroup>();
        itemGrid.childAlignment = TextAnchor.UpperLeft;
        itemGrid.padding = new RectOffset(4, 4, 4, 4);

        _itemPool = new MonoObjPool<InventoryItemDisplay>(
            InventoryItemDisplay.Prefab,
            _itemsPane.Content,
            deactive_action: display => display.Clear());
        _emptyState = new UiEmptyState(
            _itemsPane.Viewport,
            "Cultiway.Inventory.Empty".Localize(),
            190f,
            32f);
        _emptyState.SetVisible(false);
    }

    /// <summary>
    /// 创建固定在页面顶部的分类筛选栏。
    /// </summary>
    private static Transform CreateFilterBar(Transform parent)
    {
        GameObject bar = new("Filters", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        bar.transform.SetParent(parent, false);

        RectTransform rect = bar.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, FilterBarHeight);

        HorizontalLayoutGroup layout = bar.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = UiTheme.Current.Metrics.SpacingXs;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        return bar.transform;
    }

    /// <summary>
    /// 在分类按钮与数量文本之间创建弹性空间，使隐藏分类后数量仍保持右对齐。
    /// </summary>
    private static void CreateFlexibleSpacer(Transform parent)
    {
        GameObject spacer = new("Flexible Spacer", typeof(RectTransform), typeof(LayoutElement));
        spacer.transform.SetParent(parent, false);
        LayoutElement layout = spacer.GetComponent<LayoutElement>();
        layout.minWidth = 0f;
        layout.preferredWidth = 0f;
        layout.flexibleWidth = 1f;
    }

    /// <summary>
    /// 创建一个分类图标按钮并纳入互斥选中状态管理。
    /// </summary>
    private void CreateCategoryButton(
        Transform parent,
        string name,
        string iconPath,
        string title,
        string description,
        SpecialItemCategoryAsset category)
    {
        Button button = UiElements.CreateIconButton(
            parent,
            name,
            iconPath,
            28f,
            22f,
            () => SelectCategory(category),
            4f);
        UiTooltip.Set(button.gameObject, title, description);
        _categoryTabs.Add(button);
        _categoryButtons.Add(new CategoryTab(category, button));
    }

    /// <summary>
    /// 绑定角色；更换角色时重置分类并从滚动区顶部开始。
    /// </summary>
    private void Bind(Actor actor)
    {
        bool actorChanged = _actor != actor;
        _actor = actor;
        _actorExtend = actor.GetExtend();
        if (actorChanged)
        {
            _selectedCategory = null;
        }
        Refresh(actorChanged);
    }

    public void ClearWorldBinding()
    {
        _actor = null;
        _actorExtend = null;
        _selectedCategory = null;
        _itemPool.Clear();
    }

    /// <summary>
    /// 切换当前分类并重建可见物品。
    /// </summary>
    private void SelectCategory(SpecialItemCategoryAsset category)
    {
        if (_selectedCategory == category) return;

        _selectedCategory = category;
        Refresh(true);
    }

    /// <summary>
    /// 从角色库存创建稳定快照，统一刷新筛选状态、格子和空状态。
    /// </summary>
    private void Refresh(bool resetScroll)
    {
        if (_actorExtend == null) return;

        float previousScrollPosition = _itemsPane.ScrollRect.verticalNormalizedPosition;
        List<InventoryEntry> entries = CreateEntries();
        Dictionary<SpecialItemCategoryAsset, int> categoryCounts = CountCategories(entries);

        if (_selectedCategory != null &&
            (!categoryCounts.TryGetValue(_selectedCategory, out int selectedCount) || selectedCount == 0))
        {
            _selectedCategory = null;
        }

        _itemPool.Clear();
        int visibleCount = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            InventoryEntry entry = entries[i];
            if (_selectedCategory != null && entry.Category != _selectedCategory) continue;

            Entity item = entry.Item;
            UnityAction clickAction = entry.IsArtifact ? () => ToggleArtifact(item) : null;
            _itemPool.GetNext().Setup(
                item.GetComponent<SpecialItem>(),
                clickAction,
                entry.Equipped,
                entry.Equipped ? entry.State.GetStateColor() : Color.white);
            visibleCount++;
        }

        UpdateCategoryButtons(categoryCounts);
        _countText.text = string.Format(
            "Cultiway.Inventory.Format.Count".Localize(),
            visibleCount,
            entries.Count);
        _emptyState.Text.text = entries.Count == 0
            ? "Cultiway.Inventory.Empty".Localize()
            : "Cultiway.Inventory.FilterEmpty".Localize();
        _emptyState.SetVisible(visibleCount == 0);

        LayoutRebuilder.ForceRebuildLayoutImmediate(_itemsPane.Content.GetComponent<RectTransform>());
        if (resetScroll)
        {
            _itemsPane.ResetToTop();
        }
        else
        {
            _itemsPane.ScrollRect.verticalNormalizedPosition = previousScrollPosition;
        }
    }

    /// <summary>
    /// 解析物品分类、品阶和装备状态，并按页面规则生成排序后的库存快照。
    /// </summary>
    private List<InventoryEntry> CreateEntries()
    {
        List<InventoryEntry> entries = new();
        SpecialItemCategoryLibrary categories = ModClass.L.SpecialItemCategoryLibrary;
        foreach (Entity item in _actorExtend.GetItems())
        {
            SpecialItemCategoryAsset category = categories.Resolve(item);
            bool isArtifact = item.HasComponent<Artifact>();
            EquippedArtifactRelation relation = default;
            bool equipped = isArtifact &&
                            _actorExtend.TryGetArtifactEquipRelation(
                                item,
                                out relation);
            int level = item.TryGetComponent(out ItemLevel itemLevel)
                ? (int)itemLevel
                : -1;
            entries.Add(new InventoryEntry(
                item,
                category,
                isArtifact,
                equipped,
                equipped ? relation.state : default,
                level));
        }

        entries.Sort(CompareEntries);
        return entries;
    }

    /// <summary>
    /// 按分类、装备状态、品阶和实体 ID 形成确定性顺序。
    /// </summary>
    private static int CompareEntries(InventoryEntry left, InventoryEntry right)
    {
        int result = left.Category.order.CompareTo(right.Category.order);
        if (result != 0) return result;

        result = right.Equipped.CompareTo(left.Equipped);
        if (result != 0) return result;

        result = right.Level.CompareTo(left.Level);
        return result != 0 ? result : left.Item.Id.CompareTo(right.Item.Id);
    }

    /// <summary>
    /// 统计各分类包含的物品数量，用于禁用空分类。
    /// </summary>
    private static Dictionary<SpecialItemCategoryAsset, int> CountCategories(
        List<InventoryEntry> entries)
    {
        Dictionary<SpecialItemCategoryAsset, int> counts = new();
        for (int i = 0; i < entries.Count; i++)
        {
            SpecialItemCategoryAsset category = entries[i].Category;
            counts.TryGetValue(category, out int count);
            counts[category] = count + 1;
        }
        return counts;
    }

    /// <summary>
    /// 刷新分类按钮的选中态和可用状态。
    /// </summary>
    private void UpdateCategoryButtons(
        Dictionary<SpecialItemCategoryAsset, int> categoryCounts)
    {
        Button selectedButton = null;
        for (int i = 0; i < _categoryButtons.Count; i++)
        {
            CategoryTab tab = _categoryButtons[i];
            bool available = tab.Category == null ||
                             categoryCounts.TryGetValue(tab.Category, out int count) && count > 0;
            tab.Button.gameObject.SetActive(available);
            tab.Button.interactable = true;
            if (tab.Category == _selectedCategory)
            {
                selectedButton = tab.Button;
            }
        }

        _categoryTabs.SetSelected(selectedButton);
        LayoutRebuilder.ForceRebuildLayoutImmediate(_filterBar);
    }

    /// <summary>
    /// 切换法宝装备关系并刷新当前网格，同时保留滚动位置。
    /// </summary>
    private void ToggleArtifact(Entity item)
    {
        if (_actorExtend.IsArtifactEquipped(item))
        {
            _actorExtend.UnequipArtifact(item, suppressAutoEquip: true);
        }
        else
        {
            _actorExtend.EquipArtifact(item, locked: true);
        }
        Refresh(false);
    }

    /// <summary>
    /// 分类按钮及其对应分类；分类为 null 时表示“全部”。
    /// </summary>
    private readonly struct CategoryTab
    {
        public readonly SpecialItemCategoryAsset Category;
        public readonly Button Button;

        public CategoryTab(SpecialItemCategoryAsset category, Button button)
        {
            Category = category;
            Button = button;
        }
    }

    /// <summary>
    /// 单次刷新使用的库存展示快照。
    /// </summary>
    private readonly struct InventoryEntry
    {
        public readonly Entity Item;
        public readonly SpecialItemCategoryAsset Category;
        public readonly bool IsArtifact;
        public readonly bool Equipped;
        public readonly ArtifactControlState State;
        public readonly int Level;

        public InventoryEntry(
            Entity item,
            SpecialItemCategoryAsset category,
            bool isArtifact,
            bool equipped,
            ArtifactControlState state,
            int level)
        {
            Item = item;
            Category = category;
            IsArtifact = isArtifact;
            Equipped = equipped;
            State = state;
            Level = level;
        }
    }
}
