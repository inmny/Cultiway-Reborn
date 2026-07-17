using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Content.Artifacts.Baibao;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core.Semantics;
using Cultiway.UI.Prefab;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI.Components;

/// <summary>法宝编辑器使用的可搜索 atom 目录，并展示候选项经过正式组合器后的实际影响。</summary>
internal sealed class BaibaoAtomPicker
{
    private enum SortMode
    {
        Relevance,
        Name,
        Contribution,
    }

    private const float PanelWidth = 510f;
    private const float InnerWidth = 498f;
    private const float BodyHeight = 244f;

    private readonly Func<ArtifactBlueprint> _getBlueprint;
    private readonly Func<bool> _getAutoName;
    private readonly Func<bool> _getAutoAppearance;
    private readonly Action<ArtifactAtomAsset> _toggle;
    private readonly UiModal _modal;
    private readonly UiSegmentedTabs _categoryTabs = new();
    private readonly Button[] _categoryButtons = new Button[3];
    private readonly Text _title;
    private readonly InputField _search;
    private readonly Button _sortButton;
    private readonly Text _resultCount;
    private readonly Transform _facetContent;
    private readonly UiScrollPane _catalogPane;
    private readonly UiScrollPane _detailPane;
    private readonly MonoObjPool<BaibaoAtomCatalogRow> _rowPool;
    private readonly UiEmptyState _empty;

    private ArtifactAtomAsset[] _atoms = [];
    private ArtifactAtomCategory _category;
    private string _semanticId;
    private SortMode _sortMode;
    private ArtifactAtomAsset _focused;

    public BaibaoAtomPicker(
        Transform parent,
        CanvasGroup owner,
        Transform scrollbarTemplate,
        Func<ArtifactBlueprint> getBlueprint,
        Func<bool> getAutoName,
        Func<bool> getAutoAppearance,
        Action<ArtifactAtomAsset> toggle)
    {
        _getBlueprint = getBlueprint;
        _getAutoName = getAutoName;
        _getAutoAppearance = getAutoAppearance;
        _toggle = toggle;

        GameObject panel = UiLayout.Create(parent, "BaibaoAtomPicker", false, PanelWidth, 338f, 4f,
            TextAnchor.UpperCenter);
        panel.transform.localPosition = new Vector3(0f, -4f);
        VerticalLayoutGroup panelLayout = panel.GetComponent<VerticalLayoutGroup>();
        panelLayout.padding = new RectOffset(6, 6, 6, 6);
        Image background = panel.AddComponent<Image>();
        UiResources.ApplySurface(background, UiSurface.WindowEmpty);

        GameObject header = UiLayout.Create(panel.transform, "Header", true, InnerWidth, 24f, 4f);
        _title = UiElements.CreateText(header.transform, "Title", string.Empty, 466f, 24f, 9,
            TextAnchor.MiddleLeft, FontStyle.Bold);
        Button close = UiElements.CreateIconButton(header.transform, "Close", UiIcons.Cancel, 28f, 22f, Hide);
        UiTooltip.Set(close.gameObject, "Cultiway.Baibao.UI.AtomPicker.Close".Localize(),
            "Cultiway.Baibao.UI.AtomPicker.Close.Description".Localize());

        CreateCategoryTabs(panel.transform);

        GameObject toolbar = UiLayout.Create(panel.transform, "Toolbar", true, InnerWidth, 22f, 4f);
        _search = UiSearchField.Create(toolbar.transform, "Search", string.Empty,
            "Cultiway.Baibao.UI.AtomPicker.Search".Localize(), 342f, 22f).Input;
        _search.onValueChanged.AddListener(_ => RefreshCatalog(true));
        _sortButton = UiElements.CreateIconTextButton(toolbar.transform, "Sort", UiIcons.Sort, string.Empty,
            104f, 22f, CycleSort);
        UiTooltip.Set(_sortButton.gameObject, "Cultiway.Baibao.UI.AtomPicker.Sort".Localize(),
            "Cultiway.Baibao.UI.AtomPicker.Sort.Description".Localize());
        _resultCount = UiElements.CreateText(toolbar.transform, "Count", string.Empty, 44f, 22f, 6,
            TextAnchor.MiddleRight);

        GameObject body = UiLayout.Create(panel.transform, "Body", true, InnerWidth, BodyHeight, 4f,
            TextAnchor.UpperLeft);
        UiScrollPane facets = UiScrollPane.CreateVertical(body.transform, "SemanticFilters", 96f, BodyHeight);
        facets.AttachOriginalScrollbar(scrollbarTemplate);
        facets.SetSurface(UiSurface.WindowInner, UiTheme.Current.Metrics.SpacingXs);
        _facetContent = facets.Content;

        _catalogPane = UiScrollPane.CreateVertical(body.transform, "Catalog", 248f, BodyHeight);
        _catalogPane.AttachOriginalScrollbar(scrollbarTemplate);
        _catalogPane.SetSurface(UiSurface.WindowInner, UiTheme.Current.Metrics.SpacingXs);
        _rowPool = new MonoObjPool<BaibaoAtomCatalogRow>(BaibaoAtomCatalogRow.Prefab, _catalogPane.Content);
        _empty = new UiEmptyState(_catalogPane.Root,
            "Cultiway.Baibao.UI.AtomPicker.Empty".Localize(), 210f, 32f);

        _detailPane = UiScrollPane.CreateVertical(body.transform, "Details", 146f, BodyHeight);
        _detailPane.AttachOriginalScrollbar(scrollbarTemplate);
        _detailPane.SetSurface(UiSurface.WindowInner, UiTheme.Current.Metrics.SpacingXs);
        _modal = new UiModal(panel, owner);
        UpdateSortLabel();
    }

    public void Show(ArtifactAtomCategory category, IReadOnlyList<ArtifactAtomAsset> atoms)
    {
        _atoms = atoms.ToArray();
        _category = category;
        _semanticId = null;
        _focused = null;
        _search.SetTextWithoutNotify(string.Empty);
        _modal.Show();
        RefreshCategory();
    }

    public void Hide()
    {
        _modal.Hide();
    }

    private void CreateCategoryTabs(Transform parent)
    {
        GameObject tabs = UiLayout.Create(parent, "Categories", true, InnerWidth, 22f, 4f,
            TextAnchor.MiddleCenter);
        for (int i = 0; i < _categoryButtons.Length; i++)
        {
            ArtifactAtomCategory category = (ArtifactAtomCategory)i;
            _categoryButtons[i] = UiElements.CreateIconTextButton(tabs.transform, category.ToString(),
                BaibaoPresentation.GetAtomCategoryIconPath(category),
                BaibaoPresentation.GetAtomCategoryName(category), 163f, 22f, () => SelectCategory(category));
            _categoryTabs.Add(_categoryButtons[i]);
        }
    }

    private void SelectCategory(ArtifactAtomCategory category)
    {
        if (_category == category) return;
        _category = category;
        _semanticId = null;
        _focused = null;
        RefreshCategory();
    }

    private void RefreshCategory()
    {
        _categoryTabs.SetSelected((int)_category);
        _title.text = string.Format("Cultiway.Baibao.UI.AtomPicker.Title".Localize(),
            BaibaoPresentation.GetAtomCategoryName(_category));
        RebuildSemanticFilters();
        RefreshCatalog(true);
    }

    private void RebuildSemanticFilters()
    {
        UiLayout.ClearChildren(_facetContent);
        Button all = UiElements.CreateButton(_facetContent, "All",
            "Cultiway.Baibao.UI.AtomPicker.AllSemantics".Localize(), 72f, 22f, () => SelectSemantic(null));
        UiStateStyle.SetSelected(all, string.IsNullOrEmpty(_semanticId));
        UiTooltip.Set(all.gameObject, "Cultiway.Baibao.UI.AtomPicker.AllSemantics".Localize(),
            "Cultiway.Baibao.UI.AtomPicker.AllSemantics.Description".Localize());

        Dictionary<string, SemanticAsset> semantics = new(StringComparer.Ordinal);
        foreach (ArtifactAtomAsset atom in CategoryCandidates())
        {
            ArtifactMaterialTrait[] traits = atom.material_traits ?? [];
            for (int i = 0; i < traits.Length; i++)
            {
                if (ModClass.L.SemanticLibrary.TryResolve(traits[i].key, out SemanticAsset semantic))
                    semantics[semantic.id] = semantic;
            }
        }

        foreach (SemanticAsset semantic in semantics.Values
                     .OrderBy(value => value.Facet.GetName(), StringComparer.CurrentCulture)
                     .ThenBy(value => value.GetName(), StringComparer.CurrentCulture))
        {
            Button button = UiElements.CreateButton(_facetContent, semantic.id, semantic.GetName(),
                72f, 22f, () => SelectSemantic(semantic.id));
            Text label = button.GetComponentInChildren<Text>();
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 5;
            label.resizeTextMaxSize = 7;
            UiStateStyle.SetSelected(button, semantic.id == _semanticId);
            UiTooltip.Set(button.gameObject, $"{semantic.Facet.GetName()} · {semantic.GetName()}",
                semantic.GetDescription());
        }
    }

    private void SelectSemantic(string semanticId)
    {
        if (_semanticId == semanticId) return;
        _semanticId = semanticId;
        RebuildSemanticFilters();
        RefreshCatalog(true);
    }

    private void CycleSort()
    {
        _sortMode = (SortMode)(((int)_sortMode + 1) % 3);
        UpdateSortLabel();
        RefreshCatalog(true);
    }

    private void UpdateSortLabel()
    {
        UiElements.SetButtonLabel(_sortButton,
            $"Cultiway.Baibao.UI.AtomPicker.Sort.{_sortMode}".Localize());
    }

    private void RefreshCatalog(bool resetScroll = false)
    {
        ArtifactBlueprint blueprint = _getBlueprint();
        string query = _search.text.Trim();
        List<ArtifactAtomAsset> visible = CategoryCandidates()
            .Where(atom => MatchesSemantic(atom) && MatchesSearch(atom, query))
            .ToList();
        visible.Sort((left, right) => CompareAtoms(left, right, blueprint));

        if (_focused == null || !visible.Contains(_focused)) _focused = visible.FirstOrDefault();
        _rowPool.Clear();
        for (int i = 0; i < visible.Count; i++)
        {
            ArtifactAtomAsset atom = visible[i];
            bool selected = IsSelected(blueprint, atom);
            float strength = GetStrength(blueprint, atom);
            _rowPool.GetNext().Setup(atom, strength, selected, atom == _focused,
                () => Focus(atom), () => Toggle(atom));
        }
        _empty.SetVisible(visible.Count == 0);
        _resultCount.text = string.Format("Cultiway.Baibao.UI.AtomPicker.Count".Localize(), visible.Count);
        if (resetScroll) _catalogPane.ResetToTop();
        RefreshDetails();
    }

    private IEnumerable<ArtifactAtomAsset> CategoryCandidates()
    {
        ArtifactBlueprint blueprint = _getBlueprint();
        return _atoms.Where(atom => atom.category == _category &&
                                   (_category != ArtifactAtomCategory.Shape ||
                                    atom.artifact_shape?.id == blueprint.ShapeId));
    }

    private bool MatchesSemantic(ArtifactAtomAsset atom)
    {
        if (string.IsNullOrEmpty(_semanticId)) return true;
        ArtifactMaterialTrait[] traits = atom.material_traits ?? [];
        for (int i = 0; i < traits.Length; i++)
        {
            if (ModClass.L.SemanticLibrary.TryResolve(traits[i].key, out SemanticAsset semantic) &&
                semantic.id == _semanticId)
                return true;
        }
        return false;
    }

    private static bool MatchesSearch(ArtifactAtomAsset atom, string query)
    {
        return query.Length == 0 || BaibaoPresentation.GetAtomSearchText(atom)
            .IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0;
    }

    private int CompareAtoms(ArtifactAtomAsset left, ArtifactAtomAsset right, ArtifactBlueprint blueprint)
    {
        int selected = IsSelected(blueprint, right).CompareTo(IsSelected(blueprint, left));
        if (selected != 0) return selected;
        int result = _sortMode switch
        {
            SortMode.Name => string.Compare(BaibaoPresentation.GetAtomName(left),
                BaibaoPresentation.GetAtomName(right), StringComparison.CurrentCulture),
            SortMode.Contribution => Contribution(right).CompareTo(Contribution(left)),
            _ => right.priority.CompareTo(left.priority),
        };
        return result != 0 ? result : string.Compare(left.id, right.id, StringComparison.Ordinal);
    }

    private static float Contribution(ArtifactAtomAsset atom)
    {
        return (atom.material_traits ?? []).Sum(trait => Mathf.Abs(trait.value));
    }

    private void Focus(ArtifactAtomAsset atom)
    {
        if (_focused == atom) return;
        _focused = atom;
        RefreshCatalog();
    }

    private void Toggle(ArtifactAtomAsset atom)
    {
        if (atom.category == ArtifactAtomCategory.Shape && IsSelected(_getBlueprint(), atom)) return;
        _toggle(atom);
        _focused = atom;
        RebuildSemanticFilters();
        RefreshCatalog();
    }

    private void RefreshDetails()
    {
        UiLayout.ClearChildren(_detailPane.Content);
        if (_focused == null)
        {
            AddDetailText("Cultiway.Baibao.UI.AtomPicker.SelectPrompt".Localize(), 34f,
                TextAnchor.MiddleCenter);
            return;
        }

        ArtifactBlueprint blueprint = _getBlueprint();
        float strength = GetStrength(blueprint, _focused);
        GameObject header = UiLayout.Create(_detailPane.Content, "Header", true, 114f, 36f, 3f);
        GameObject icon = new("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        icon.transform.SetParent(header.transform, false);
        UiLayout.SetSize(icon.transform, 32f, 32f);
        icon.GetComponent<Image>().sprite = BaibaoPresentation.GetAtomIcon(_focused);
        icon.GetComponent<Image>().preserveAspect = true;
        UiElements.CreateText(header.transform, "Name", BaibaoPresentation.GetAtomName(_focused),
            79f, 34f, 8, TextAnchor.MiddleLeft, FontStyle.Bold);

        AddDetailText(BaibaoPresentation.GetAtomDescription(_focused),
            EstimateHeight(BaibaoPresentation.GetAtomDescription(_focused)));
        AddHeading("Cultiway.Baibao.UI.AtomPicker.Contributions".Localize());
        ArtifactMaterialTrait[] traits = _focused.material_traits ?? [];
        for (int i = 0; i < traits.Length; i++)
        {
            ArtifactMaterialTrait trait = traits[i];
            AddDetailText($"{BaibaoPresentation.GetTraitName(trait.key)} " +
                          BaibaoPresentation.FormatSigned(trait.value * strength), 14f);
        }

        if ((_focused.name_stems ?? []).Length > 0)
        {
            AddHeading("Cultiway.Baibao.UI.AtomPicker.NameStems".Localize());
            AddDetailText(string.Join("、", _focused.name_stems), 16f);
        }
        string bias = BaibaoPresentation.GetAtomBiasSummary(_focused);
        if (!string.IsNullOrEmpty(bias))
        {
            AddHeading("Cultiway.Baibao.UI.AtomPicker.AppearanceBias".Localize());
            AddDetailText(bias, EstimateHeight(bias));
        }

        AddHeading("Cultiway.Baibao.UI.AtomPicker.ActualImpact".Localize());
        ArtifactAtomImpact impact = ArtifactAtomImpactAnalyzer.Analyze(
            blueprint, _focused, _getAutoName(), _getAutoAppearance());
        AddImpactDetails(impact);
    }

    private void AddImpactDetails(ArtifactAtomImpact impact)
    {
        AddDetailText($"Cultiway.Baibao.UI.AtomPicker.Operation.{impact.Operation}".Localize(), 16f,
            TextAnchor.MiddleLeft, FontStyle.Bold);
        for (int i = 0; i < Mathf.Min(6, impact.TraitDeltas.Length); i++)
        {
            ArtifactMaterialTrait trait = impact.TraitDeltas[i];
            AddDetailText($"{BaibaoPresentation.GetTraitName(trait.key)} " +
                          BaibaoPresentation.FormatSigned(trait.value), 14f);
        }
        AddAbilityChanges("AddedAbility", impact.AddedAbilityIds);
        AddAbilityChanges("RemovedAbility", impact.RemovedAbilityIds);
        AddAbilityChanges("ChangedAbility", impact.ChangedAbilityIds);
        if (Mathf.Abs(impact.StabilityDelta) > 0.0001f)
            AddDetailText(string.Format("Cultiway.Baibao.UI.AtomPicker.StabilityDelta".Localize(),
                BaibaoPresentation.FormatSigned(impact.StabilityDelta)), 15f);
        if (Mathf.Abs(impact.ComplexityDelta) > 0.0001f)
            AddDetailText(string.Format("Cultiway.Baibao.UI.AtomPicker.ComplexityDelta".Localize(),
                BaibaoPresentation.FormatSigned(impact.ComplexityDelta)), 15f);
        if (impact.NameChanged)
            AddDetailText(string.Format("Cultiway.Baibao.UI.AtomPicker.NameChange".Localize(),
                impact.BeforeName, impact.AfterName), 24f);
        if (impact.AppearanceChangedPartCount > 0)
            AddDetailText(string.Format("Cultiway.Baibao.UI.AtomPicker.AppearanceChange".Localize(),
                impact.AppearanceChangedPartCount), 15f);
        if (!impact.HasChanges)
            AddDetailText("Cultiway.Baibao.UI.AtomPicker.NoStructuralChange".Localize(), 24f);
    }

    private void AddAbilityChanges(string localeSuffix, string[] abilityIds)
    {
        if (abilityIds.Length == 0) return;
        string names = string.Join("、", abilityIds.Select(BaibaoPresentation.GetAbilityName));
        string value = string.Format($"Cultiway.Baibao.UI.AtomPicker.{localeSuffix}".Localize(), names);
        AddDetailText(value, EstimateHeight(value));
    }

    private void AddHeading(string value)
    {
        AddDetailText(value, 18f, TextAnchor.MiddleLeft, FontStyle.Bold);
    }

    private void AddDetailText(
        string value,
        float height,
        TextAnchor alignment = TextAnchor.UpperLeft,
        FontStyle style = FontStyle.Normal)
    {
        Text text = UiElements.CreateText(_detailPane.Content, "Detail", value, 114f, height, 6,
            alignment, style);
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
    }

    private static float EstimateHeight(string value)
    {
        if (string.IsNullOrEmpty(value)) return 14f;
        int lines = value.Split('\n').Sum(line => Mathf.Max(1, Mathf.CeilToInt(line.Length / 15f)));
        return Mathf.Max(14f, lines * 9f + 4f);
    }

    private static bool IsSelected(ArtifactBlueprint blueprint, ArtifactAtomAsset atom)
    {
        return (blueprint.AtomData.entries ?? []).Any(entry => entry.atom_id == atom.id);
    }

    private static float GetStrength(ArtifactBlueprint blueprint, ArtifactAtomAsset atom)
    {
        ArtifactAtomEntry[] entries = blueprint.AtomData.entries ?? [];
        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i].atom_id == atom.id) return entries[i].strength;
        }
        return 1f;
    }
}
