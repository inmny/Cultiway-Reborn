using System;
using System.Collections.Generic;
using NeoModLoader.General;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Cultiway.UI;

internal sealed class PowerTabGroupLayout
{
    private const float ButtonSize = 32f;
    private const float ColumnSpacing = 4f;
    private const float TopRowY = 34f;
    private const float BottomRowY = -2f;
    private const float GroupHeight = 100f;
    private const float SeparatorHeight = ButtonSize * 2f + ColumnSpacing;

    private readonly List<Section> sections = new();
    private readonly Dictionary<string, Section> sectionsById = new(StringComparer.Ordinal);
    private readonly HashSet<string> entryIds = new(StringComparer.Ordinal);
    private readonly LayoutElement layoutElement;

    internal RectTransform Root { get; }

    internal PowerTabGroupLayout(string name, Transform parent)
    {
        Root = new GameObject(name, typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
        Root.SetParent(parent, false);
        Root.pivot = new Vector2(0f, 0.5f);
        Root.sizeDelta = new Vector2(0f, GroupHeight);
        layoutElement = Root.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = GroupHeight;
        Root.gameObject.SetActive(false);
    }

    internal void AddSection(string sectionId, int order)
    {
        if (sectionsById.ContainsKey(sectionId))
        {
            throw new InvalidOperationException($"神力 Tab 分区重复注册: {Root.name}/{sectionId}");
        }

        var section = new Section(sectionId, order);
        sections.Add(section);
        sectionsById.Add(sectionId, section);
        Rebuild();
    }

    internal void AddButton(string sectionId, int order, string stableId, PowerButton button)
    {
        AddEntry(sectionId, new Entry(order, stableId, EntryKind.Button, button, null, null));
    }

    internal void AddButtonPair(string sectionId, int order, string stableId, PowerButton topButton,
        PowerButton bottomButton)
    {
        AddEntry(sectionId, new Entry(order, stableId, EntryKind.ButtonPair, topButton, bottomButton, null));
    }

    internal void AddSeparator(string sectionId, int order, string stableId)
    {
        GameObject separator = Object.Instantiate(ResourcesFinder.FindResource<GameObject>("_line"), Root);
        separator.name = $"_line_{stableId}";
        separator.GetComponent<Image>().enabled = true;
        separator.SetActive(true);
        AddEntry(sectionId, new Entry(order, stableId, EntryKind.Separator, null, null, separator));
    }

    internal void SetActive(bool active)
    {
        Root.gameObject.SetActive(active);
    }

    private void AddEntry(string sectionId, Entry entry)
    {
        if (!sectionsById.TryGetValue(sectionId, out Section section))
        {
            throw new InvalidOperationException($"神力 Tab 条目引用了未注册分区: {Root.name}/{sectionId}");
        }

        if (!entryIds.Add(entry.StableId))
        {
            throw new InvalidOperationException($"神力 Tab 条目 ID 重复: {Root.name}/{entry.StableId}");
        }

        AttachButton(entry.TopButton);
        AttachButton(entry.BottomButton);
        section.Entries.Add(entry);
        Rebuild();
    }

    private void AttachButton(PowerButton button)
    {
        if (button == null) return;

        button.transform.SetParent(Root, false);
        button.transform.localScale = Vector3.one;
        button.rect_transform = button.GetComponent<RectTransform>();
    }

    private void Rebuild()
    {
        sections.Sort(CompareSections);

        float cursorX = 0f;
        float rightEdge = 0f;
        bool bottomRowNext = false;
        int siblingIndex = 0;

        for (int sectionIndex = 0; sectionIndex < sections.Count; sectionIndex++)
        {
            List<Entry> entries = sections[sectionIndex].Entries;
            entries.Sort(CompareEntries);
            for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
            {
                Entry entry = entries[entryIndex];
                switch (entry.Kind)
                {
                    case EntryKind.Button:
                        PlaceButton(entry.TopButton, cursorX, bottomRowNext ? BottomRowY : TopRowY, siblingIndex++);
                        rightEdge = Mathf.Max(rightEdge, cursorX + ButtonSize);
                        if (bottomRowNext)
                        {
                            cursorX += ButtonSize + ColumnSpacing;
                        }

                        bottomRowNext = !bottomRowNext;
                        break;
                    case EntryKind.ButtonPair:
                        FinishPartialColumn(ref cursorX, ref bottomRowNext);
                        PlaceButton(entry.TopButton, cursorX, TopRowY, siblingIndex++);
                        PlaceButton(entry.BottomButton, cursorX, BottomRowY, siblingIndex++);
                        rightEdge = Mathf.Max(rightEdge, cursorX + ButtonSize);
                        cursorX += ButtonSize + ColumnSpacing;
                        break;
                    case EntryKind.Separator:
                        FinishPartialColumn(ref cursorX, ref bottomRowNext);
                        float separatorWidth = PlaceSeparator(entry.Separator, cursorX, siblingIndex++);
                        rightEdge = Mathf.Max(rightEdge, cursorX + separatorWidth);
                        cursorX += separatorWidth + ColumnSpacing;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        Root.sizeDelta = new Vector2(rightEdge, GroupHeight);
        layoutElement.minWidth = rightEdge;
        layoutElement.preferredWidth = rightEdge;
        LayoutRebuilder.MarkLayoutForRebuild(Root);
    }

    private static void FinishPartialColumn(ref float cursorX, ref bool bottomRowNext)
    {
        if (!bottomRowNext) return;

        cursorX += ButtonSize + ColumnSpacing;
        bottomRowNext = false;
    }

    private static void PlaceButton(PowerButton button, float x, float y, int siblingIndex)
    {
        RectTransform rect = button.rect_transform;
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(ButtonSize, ButtonSize);
        rect.anchoredPosition = new Vector2(x + ButtonSize * 0.5f, y);
        rect.localScale = Vector3.one;
        rect.SetSiblingIndex(siblingIndex);
    }

    private static float PlaceSeparator(GameObject separator, float x, int siblingIndex)
    {
        RectTransform rect = separator.GetComponent<RectTransform>();
        float width = rect.sizeDelta.x;
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(width, SeparatorHeight);
        rect.anchoredPosition = new Vector2(x + width * 0.5f, (TopRowY + BottomRowY) * 0.5f);
        rect.localScale = Vector3.one;
        rect.SetSiblingIndex(siblingIndex);
        return width;
    }

    private static int CompareSections(Section left, Section right)
    {
        int orderComparison = left.Order.CompareTo(right.Order);
        return orderComparison != 0 ? orderComparison : StringComparer.Ordinal.Compare(left.Id, right.Id);
    }

    private static int CompareEntries(Entry left, Entry right)
    {
        int orderComparison = left.Order.CompareTo(right.Order);
        return orderComparison != 0
            ? orderComparison
            : StringComparer.Ordinal.Compare(left.StableId, right.StableId);
    }

    private sealed class Section
    {
        internal readonly string Id;
        internal readonly int Order;
        internal readonly List<Entry> Entries = new();

        internal Section(string id, int order)
        {
            Id = id;
            Order = order;
        }
    }

    private sealed class Entry
    {
        internal readonly int Order;
        internal readonly string StableId;
        internal readonly EntryKind Kind;
        internal readonly PowerButton TopButton;
        internal readonly PowerButton BottomButton;
        internal readonly GameObject Separator;

        internal Entry(int order, string stableId, EntryKind kind, PowerButton topButton, PowerButton bottomButton,
            GameObject separator)
        {
            Order = order;
            StableId = stableId;
            Kind = kind;
            TopButton = topButton;
            BottomButton = bottomButton;
            Separator = separator;
        }
    }

    private enum EntryKind
    {
        Button,
        ButtonPair,
        Separator
    }
}
