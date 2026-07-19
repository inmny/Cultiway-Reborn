using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Artifacts.Baibao;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core.Components;
using Cultiway.UI.Components;
using Cultiway.UI.Prefab;
using NeoModLoader.api;
using NeoModLoader.General;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI;

/// <summary>以蓝图草稿驱动的多页法宝编辑器。</summary>
public sealed class WindowBaibaoForge : AbstractWideWindow<WindowBaibaoForge>
{
    private enum ExitMode
    {
        Close,
        Back,
    }

    private enum EditorPage
    {
        Basic,
        Composition,
        Appearance,
        Ability,
        Overview,
    }

    private sealed class EditorDraft
    {
        public ArtifactBlueprint Blueprint;
        public bool AutoName;
        public bool AutoAppearance;

        public EditorDraft DeepClone()
        {
            return new EditorDraft
            {
                Blueprint = Blueprint.DeepClone(),
                AutoName = AutoName,
                AutoAppearance = AutoAppearance,
            };
        }
    }

    public const string Id = "Cultiway.UI.WindowBaibaoForge";
    public static readonly Vector2 WindowSize = new(600f, 380f);
    private const float RootHeight = 338f;
    private const float EditorContentHeight = 254f;
    private static ArtifactBlueprint _pendingBlueprint;
    private static bool _pendingCopyOnly;
    private static bool _pendingNew;

    private readonly Stack<EditorDraft> _undo = new();
    private readonly Stack<EditorDraft> _redo = new();
    private ArtifactShapeAsset[] _shapes = [];
    private ArtifactAtomAsset[] _atoms = [];
    private EditorDraft _draft;
    private EditorDraft _savedDraft;
    private bool _existing;
    private bool _canUpdate;
    private bool _saveAsCopy;
    private bool _closingApproved;
    private bool _resumeAfterClose;
    private ExitMode _pendingExitMode;
    private EditorPage _page;
    private CanvasGroup _editorCanvas;
    private InputField _nameInput;
    private Button _nameMode;
    private Text _draftState;
    private BaibaoArtifactPreview _preview;
    private Button[] _tabButtons;
    private readonly UiSegmentedTabs _tabs = new();
    private MonoObjPool<BaibaoEditorRow> _rowPool;
    private Button _undoButton;
    private Button _redoButton;
    private Button _saveButton;
    private BaibaoAtomPicker _atomPicker;
    private UiModal _confirmation;

    public static void Open()
    {
        _pendingBlueprint = null;
        _pendingCopyOnly = false;
        _pendingNew = true;
        ScrollWindow.showWindow(Id);
    }

    public static void Open(ArtifactBlueprint blueprint)
    {
        _pendingBlueprint = blueprint.DeepClone();
        _pendingCopyOnly = blueprint.OriginKind != ArtifactBlueprintOriginKind.Forged;
        _pendingNew = false;
        ScrollWindow.showWindow(Id);
    }

    public static void OpenCopy(ArtifactBlueprint blueprint)
    {
        _pendingBlueprint = blueprint.DeepClone();
        _pendingCopyOnly = true;
        _pendingNew = false;
        ScrollWindow.showWindow(Id);
    }

    protected override void Init()
    {
        UiWindowContext context = UiWindowContext.Bind(BackgroundTransform);

        GameObject root = UiLayout.Create(BackgroundTransform, "BaibaoEditorRoot", false, 520f,
            RootHeight, 4f);
        root.transform.localPosition = new Vector3(0f, -8f);
        _editorCanvas = root.AddComponent<CanvasGroup>();
        CreateHeader(root.transform);

        GameObject body = UiLayout.Create(root.transform, "Body", true, 520f, 280f, 4f,
            TextAnchor.UpperLeft);
        CreatePreview(body.transform);
        GameObject editor = UiLayout.Create(body.transform, "Editor", false, 358f, 280f, 4f);
        CreateTabs(editor.transform);
        UiScrollPane editorPane = UiScrollPane.CreateVertical(editor.transform, "EditorContent", 358f,
            EditorContentHeight);
        editorPane.AttachOriginalScrollbar(context.ScrollbarTemplate);
        editorPane.SetSurface(UiSurface.WindowEmpty, UiTheme.Current.Metrics.SpacingMd);
        _rowPool = new MonoObjPool<BaibaoEditorRow>(BaibaoEditorRow.Prefab, editorPane.Content);

        CreateFooter(root.transform);
        _atomPicker = new BaibaoAtomPicker(BackgroundTransform, _editorCanvas, context.ScrollbarTemplate,
            () => _draft.Blueprint, () => _draft.AutoName, () => _draft.AutoAppearance,
            ToggleAtomFromPicker);
        CreateConfirmationPanel();
    }

    public override void OnNormalEnable()
    {
        StartCoroutine(BindBackButtonAfterScrollWindowStart());
        _atomPicker.Hide();
        if (_resumeAfterClose)
        {
            _resumeAfterClose = false;
            RefreshAll();
            SetConfirmation(true);
            return;
        }

        RefreshAssets();
        _draft = _pendingNew || _pendingBlueprint == null
            ? CreateNewDraft()
            : CreateDraft(_pendingBlueprint);
        _saveAsCopy = _pendingCopyOnly;
        _existing = !_pendingNew && !_saveAsCopy && _pendingBlueprint?.Id != null;
        _canUpdate = _existing && _pendingBlueprint.OriginKind == ArtifactBlueprintOriginKind.Forged;
        _savedDraft = _draft.DeepClone();
        _pendingBlueprint = null;
        _pendingCopyOnly = false;
        _pendingNew = false;
        _closingApproved = false;
        _pendingExitMode = ExitMode.Close;
        _page = EditorPage.Basic;
        _undo.Clear();
        _redo.Clear();
        SetConfirmation(false);
        RefreshAll();
    }

    public override void OnNormalDisable()
    {
        _atomPicker.Hide();
        if (_draft == null || !IsDirty() || _closingApproved) return;
        _resumeAfterClose = true;
        World.world.StartCoroutine(ReopenAfterClose());
    }

    private void CreateHeader(Transform root)
    {
        GameObject header = UiLayout.Create(root, "Header", true, 520f, 24f, 4f);
        _nameInput = UiElements.CreateInput(header.transform, "Name", string.Empty,
            "Cultiway.Baibao.UI.Placeholder.ArtifactName".Localize(), 196f, 22f);
        _nameInput.characterLimit = 24;
        _nameInput.onEndEdit.AddListener(ApplyCustomName);
        UiTooltip.Set(_nameInput, "Cultiway.Baibao.UI.Placeholder.ArtifactName",
            "Cultiway.Baibao.UI.Tooltip.ArtifactName");
        _nameMode = UiElements.CreateIconTextButton(header.transform, "NameMode", UiIcons.Edit,
            string.Empty, 92f, 22f, ToggleNameMode);
        UiTooltip.Set(_nameMode.gameObject, "Cultiway.Baibao.UI.Tooltip.NameMode.Title",
            "Cultiway.Baibao.UI.Tooltip.NameMode");
        _draftState = UiElements.CreateText(header.transform, "DraftState", string.Empty, 224f, 22f, 6,
            TextAnchor.MiddleRight);
    }

    private void CreatePreview(Transform body)
    {
        GameObject root = UiElements.CreatePanel(body, "Preview", false, 158f, 280f, 3f,
            TextAnchor.UpperCenter);
        _preview = new BaibaoArtifactPreview(root.transform, 146f, 268f);
    }

    private void CreateTabs(Transform editor)
    {
        GameObject tabs = UiLayout.Create(editor, "Tabs", true, 358f, 22f, 4f);
        string[] names =
        {
            "Cultiway.Baibao.UI.Tab.Basic", "Cultiway.Baibao.UI.Tab.Composition",
            "Cultiway.Baibao.UI.Tab.Appearance", "Cultiway.Baibao.UI.Tab.Ability",
            "Cultiway.Baibao.UI.Tab.Overview",
        };
        string[] icons =
        {
            BaibaoUiIcons.Pavilion, BaibaoUiIcons.Composition, BaibaoUiIcons.Appearance,
            BaibaoUiIcons.Ability, UiIcons.Info,
        };
        _tabButtons = new Button[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            EditorPage page = (EditorPage)i;
            _tabButtons[i] = UiElements.CreateIconTextButton(tabs.transform, page.ToString(), icons[i],
                names[i].Localize(), 68f, 21f, () => SelectPage(page));
            _tabs.Add(_tabButtons[i]);
            UiTooltip.Set(_tabButtons[i].gameObject, names[i],
                $"Cultiway.Baibao.UI.Tooltip.Tab.{page}".Localize());
        }
    }

    private void CreateFooter(Transform root)
    {
        GameObject footer = UiLayout.Create(root, "Footer", true, 520f, 26f, 4f);
        _undoButton = CreateFooterButton(footer.transform, "Undo", UiIcons.Undo,
            "Cultiway.Baibao.UI.Action.Undo", "Cultiway.Baibao.UI.Tooltip.Undo", Undo);
        _redoButton = CreateFooterButton(footer.transform, "Redo", UiIcons.Undo,
            "Cultiway.Baibao.UI.Action.Redo", "Cultiway.Baibao.UI.Tooltip.Redo", Redo);
        UiElements.SetButtonIcon(_redoButton, UiIcons.Undo, true);
        CreateFooterButton(footer.transform, "Reset", UiIcons.Reset,
            "Cultiway.Baibao.UI.Action.DiscardChanges", "Cultiway.Baibao.UI.Tooltip.DiscardChanges",
            DiscardChanges);
        UiElements.CreateText(footer.transform, "Spacer", string.Empty, 312f, 24f);
        CreateFooterButton(footer.transform, "SaveCopy", UiIcons.Copy,
            "Cultiway.Baibao.UI.Action.SaveCopy", "Cultiway.Baibao.UI.Tooltip.SaveCopy", SaveCopy, 34f);
        _saveButton = CreateFooterButton(footer.transform, "Save", UiIcons.Save,
            "Cultiway.Baibao.UI.Action.Save", "Cultiway.Baibao.UI.Tooltip.Save", TrySave, 38f);
        CreateFooterButton(footer.transform, "Close", UiIcons.Cancel,
            "Cultiway.Baibao.UI.Action.Cancel", "Cultiway.Baibao.UI.Tooltip.Cancel", RequestBack);
    }

    private static Button CreateFooterButton(Transform parent, string name, string icon, string title,
        string description, UnityEngine.Events.UnityAction action, float width = 28f)
    {
        Button button = UiElements.CreateIconButton(parent, name, icon, width, 23f, action);
        UiTooltip.Set(button.gameObject, title, description);
        return button;
    }

    private void RefreshAssets()
    {
        _shapes = ModClass.L.ItemShapeLibrary.list.OfType<ArtifactShapeAsset>()
            .OrderBy(shape => shape.id, StringComparer.Ordinal).ToArray();
        _atoms = Cultiway.Content.Libraries.Manager.ArtifactAtomLibrary.All
            .OrderBy(atom => atom.category).ThenByDescending(atom => atom.priority)
            .ThenBy(atom => atom.id, StringComparer.Ordinal).ToArray();
    }

    private EditorDraft CreateNewDraft()
    {
        ArtifactAtomAsset[] defaults =
        {
            _atoms.FirstOrDefault(atom => atom.category == ArtifactAtomCategory.Material),
            _atoms.FirstOrDefault(atom => atom.category == ArtifactAtomCategory.Finish),
        };
        ArtifactComposeResult result = ArtifactComposer.ComposeDesign(new ArtifactDesignRequest
        {
            Shape = _shapes[0],
            Level = ItemLevel.FromValue(0),
            Atoms = defaults.Where(atom => atom != null).ToArray(),
        });
        return new EditorDraft
        {
            Blueprint = ArtifactBlueprintCodec.FromComposeResult(result),
            AutoName = true,
            AutoAppearance = true,
        };
    }

    private static EditorDraft CreateDraft(ArtifactBlueprint blueprint)
    {
        ArtifactShapeAsset shape = (ArtifactShapeAsset)ModClass.L.ItemShapeLibrary.get(blueprint.ShapeId);
        ArtifactComposeResult automatic = ArtifactComposer.ComposeDesign(new ArtifactDesignRequest
        {
            Shape = shape,
            Level = blueprint.Level,
            AtomEntries = blueprint.AtomData.entries ?? [],
        });
        return new EditorDraft
        {
            Blueprint = blueprint.DeepClone(),
            AutoName = automatic.Name == blueprint.Name,
            AutoAppearance = automatic.Appearance.GetCacheKey() == blueprint.Appearance.GetCacheKey(),
        };
    }

    private void RefreshAll()
    {
        if (_draft == null) return;
        _undoButton.interactable = _undo.Count > 0;
        _redoButton.interactable = _redo.Count > 0;
        _saveButton.interactable = !_existing || IsDirty();
        RefreshHeader();
        RefreshPreview();
        RefreshPage();
    }

    private void RefreshHeader()
    {
        _nameInput.SetTextWithoutNotify(_draft.Blueprint.Name);
        UiElements.SetButtonLabel(_nameMode, _draft.AutoName
            ? "Cultiway.Baibao.UI.NameMode.Auto".Localize()
            : "Cultiway.Baibao.UI.NameMode.Custom".Localize());
        string source = _saveAsCopy
            ? "Cultiway.Baibao.UI.State.CopyDraft".Localize()
            : _existing
                ? "Cultiway.Baibao.UI.State.ExistingBlueprint".Localize()
                : "Cultiway.Baibao.UI.State.NewBlueprint".Localize();
        string state = IsDirty()
            ? "Cultiway.Baibao.UI.State.Unsaved".Localize()
            : "Cultiway.Baibao.UI.State.Saved".Localize();
        _draftState.text = $"{source}  ·  {state}";
    }

    private void RefreshPreview()
    {
        ArtifactBlueprint blueprint = _draft.Blueprint;
        _preview.Show(blueprint, true, "Cultiway.Baibao.UI.State.BlueprintValid".Localize(),
            new Color(0.65f, 1f, 0.72f, 1f));
    }

    private void RefreshPage()
    {
        _rowPool.Clear();
        _tabs.SetSelected((int)_page);
        switch (_page)
        {
            case EditorPage.Basic:
                BuildBasicPage();
                break;
            case EditorPage.Composition:
                BuildCompositionPage();
                break;
            case EditorPage.Appearance:
                BuildAppearancePage();
                break;
            case EditorPage.Ability:
                BuildAbilityPage();
                break;
            case EditorPage.Overview:
                BuildOverviewPage();
                break;
        }
    }

    private void BuildBasicPage()
    {
        for (int i = 0; i < _shapes.Length; i++)
        {
            ArtifactShapeAsset shape = _shapes[i];
            bool selected = shape.id == _draft.Blueprint.ShapeId;
            ArtifactBlueprint candidate = selected ? _draft.Blueprint : BuildShapeCandidate(shape);
            int templateCount = ArtifactAppearanceCatalogLoader.Current
                .TemplatesForShape(shape.appearance_family).Count;
            _rowPool.GetNext().Setup(BaibaoPresentation.GetShapeName(shape),
                string.Format("Cultiway.Baibao.UI.Format.ShapeTemplates".Localize(), templateCount),
                selected ? "Cultiway.Baibao.UI.Action.Selected".Localize() :
                    "Cultiway.Baibao.UI.Action.Select".Localize(),
                selected ? UiIcons.Confirm : UiIcons.Select, selected, !selected,
                () => SelectShape(shape), BaibaoPavilionService.Instance.GetPreviewIcon(candidate));
        }

        BaibaoEditorRow stageRow = _rowPool.GetNext();
        stageRow.Setup("Cultiway.Baibao.UI.Label.QualityStage".Localize(), _draft.Blueprint.Level.GetName(),
            string.Empty, UiIcons.Select, false, false, null, SpriteTextureLoader.getSprite(UiIcons.Info));
        Transform stageControls = stageRow.UseControls(28f);
        GameObject stages = UiLayout.Create(stageControls, "Stages", true, 306f, 24f, 3f);
        for (int i = 0; i < 4; i++)
        {
            int stage = i;
            Button button = UiElements.CreateButton(stages.transform, $"Stage{stage}",
                $"{LM.Get($"Cultiway.Stage.{stage}")}阶", 74f, 22f, () => SetQualityStage(stage));
            UiStateStyle.SetSelected(button, _draft.Blueprint.Level.Stage == stage);
        }

        BaibaoEditorRow levelRow = _rowPool.GetNext();
        levelRow.Setup("Cultiway.Baibao.UI.Label.QualityLevel".Localize(),
            string.Format("Cultiway.Baibao.UI.Format.QualityLevel".Localize(), _draft.Blueprint.Level.Level + 1),
            string.Empty, UiIcons.Select, false, false, null,
            SpriteTextureLoader.getSprite(UiIcons.Info));
        Transform levelControls = levelRow.UseControls(28f);
        GameObject level = UiLayout.Create(levelControls, "Level", true, 306f, 24f, 4f,
            TextAnchor.MiddleCenter);
        UiElements.CreateIconButton(level.transform, "Previous", UiIcons.Previous, 28f, 22f,
            () => StepQualityLevel(-1));
        UiElements.CreateText(level.transform, "Value", _draft.Blueprint.Level.GetName(), 212f, 22f, 8,
            TextAnchor.MiddleCenter, FontStyle.Bold);
        UiElements.CreateIconButton(level.transform, "Next", UiIcons.Next, 28f, 22f,
            () => StepQualityLevel(1));

        _rowPool.GetNext().Setup("Cultiway.Baibao.UI.Label.NameRule".Localize(),
            _draft.AutoName ? "Cultiway.Baibao.UI.Detail.AutoName".Localize() :
                "Cultiway.Baibao.UI.Detail.CustomName".Localize(),
            _draft.AutoName ? "Cultiway.Baibao.UI.Action.UseCustomName".Localize() :
                "Cultiway.Baibao.UI.Action.RestoreAutoName".Localize(),
            UiIcons.Edit, _draft.AutoName, true, ToggleNameMode,
            SpriteTextureLoader.getSprite(UiIcons.Edit));
    }

    private void BuildCompositionPage()
    {
        ArtifactAtomEntry[] entries = _draft.Blueprint.AtomData.entries ?? [];
        for (int categoryIndex = 0; categoryIndex < 3; categoryIndex++)
        {
            ArtifactAtomCategory category = (ArtifactAtomCategory)categoryIndex;
            (ArtifactAtomEntry Entry, ArtifactAtomAsset Atom)[] selected = entries
                .Select(entry => (Entry: entry,
                    Atom: Cultiway.Content.Libraries.Manager.ArtifactAtomLibrary.get(entry.atom_id)))
                .Where(item => item.Atom?.category == category)
                .OrderByDescending(item => item.Atom.priority)
                .ThenBy(item => item.Atom.id, StringComparer.Ordinal)
                .ToArray();

            BaibaoEditorRow header = _rowPool.GetNext();
            string categoryName = BaibaoPresentation.GetAtomCategoryName(category);
            header.Setup(categoryName,
                string.Format("Cultiway.Baibao.UI.Format.AtomCount".Localize(), selected.Length),
                category == ArtifactAtomCategory.Shape
                    ? "Cultiway.Baibao.UI.Action.ReplaceAtom".Localize()
                    : "Cultiway.Baibao.UI.Action.AddAtom".Localize(),
                category == ArtifactAtomCategory.Shape ? UiIcons.Select : UiIcons.Add,
                false, true, () => ShowAtomPicker(category),
                SpriteTextureLoader.getSprite(BaibaoPresentation.GetAtomCategoryIconPath(category)));
            header.SetTooltip(categoryName,
                $"Cultiway.Baibao.UI.AtomCategory.{category}.Description".Localize());

            for (int i = 0; i < selected.Length; i++)
            {
                ArtifactAtomEntry entry = selected[i].Entry;
                ArtifactAtomAsset atom = selected[i].Atom;
                bool shapeAtom = atom.category == ArtifactAtomCategory.Shape;
                BaibaoEditorRow row = _rowPool.GetNext();
                row.Setup(BaibaoPresentation.GetAtomName(atom),
                    BaibaoPresentation.GetAtomTraitSummary(atom, entry.strength, 2),
                    string.Empty, UiIcons.Remove, true, !shapeAtom,
                    shapeAtom ? null : () => RemoveAtom(entry.atom_id),
                    BaibaoPresentation.GetAtomIcon(atom));
                row.SetTooltip(BaibaoPresentation.GetAtomName(atom),
                    BaibaoPresentation.GetAtomDescription(atom),
                    BaibaoPresentation.GetAtomTooltipDetail(atom, entry.strength));
                if (!shapeAtom)
                    row.SetActionTooltip("Cultiway.Baibao.UI.Action.Remove".Localize(),
                        "Cultiway.Baibao.UI.Tooltip.RemoveAtom".Localize());
                Transform strength = row.UseInlineControls(94f, shapeAtom ? 0f : 28f);
                Button decrease = UiElements.CreateButton(strength, "Decrease", "-", 24f, 21f,
                    () => StepAtomStrength(entry.atom_id, -0.25f));
                UiElements.CreateText(strength, "Value", entry.strength.ToString("0.00"), 42f, 21f, 7,
                    TextAnchor.MiddleCenter, FontStyle.Bold);
                Button increase = UiElements.CreateButton(strength, "Increase", "+", 24f, 21f,
                    () => StepAtomStrength(entry.atom_id, 0.25f));
                UiTooltip.Set(decrease.gameObject, "Cultiway.Baibao.UI.Action.DecreaseStrength".Localize(),
                    "Cultiway.Baibao.UI.Tooltip.AtomStrength".Localize());
                UiTooltip.Set(increase.gameObject, "Cultiway.Baibao.UI.Action.IncreaseStrength".Localize(),
                    "Cultiway.Baibao.UI.Tooltip.AtomStrength".Localize());
            }
        }
    }

    private void BuildAppearancePage()
    {
        _rowPool.GetNext().Setup("Cultiway.Baibao.UI.Label.AppearanceMode".Localize(),
            _draft.AutoAppearance ? "Cultiway.Baibao.UI.Detail.AutoAppearance".Localize() :
                "Cultiway.Baibao.UI.Detail.CustomAppearance".Localize(),
            _draft.AutoAppearance ? "Cultiway.Baibao.UI.Action.UseCustomAppearance".Localize() :
                "Cultiway.Baibao.UI.Action.RestoreAutoAppearance".Localize(),
            BaibaoUiIcons.Appearance, _draft.AutoAppearance, true, ToggleAppearanceMode,
            SpriteTextureLoader.getSprite(BaibaoUiIcons.Appearance));

        ArtifactShapeAsset shape = CurrentShape();
        List<ArtifactAppearanceTemplateDef> templates = ArtifactAppearanceCatalogLoader.Current
            .TemplatesForShape(shape.appearance_family);
        for (int i = 0; i < templates.Count; i++)
        {
            ArtifactAppearanceTemplateDef template = templates[i];
            bool selected = _draft.Blueprint.Appearance.template_key == template.Key;
            ArtifactBlueprint candidate = selected ? _draft.Blueprint : BuildAppearanceCandidate(template);
            _rowPool.GetNext().Setup(BaibaoPresentation.GetTemplateName(template.Key),
                string.Format("Cultiway.Baibao.UI.Format.TemplateSlots".Localize(), template.Placements.Length),
                selected ? "Cultiway.Baibao.UI.Action.Selected".Localize() :
                    "Cultiway.Baibao.UI.Action.Select".Localize(),
                selected ? UiIcons.Confirm : UiIcons.Select, selected, !selected,
                () => SelectTemplate(template), BaibaoPavilionService.Instance.GetPreviewIcon(candidate));
        }
        BuildColorRoleControls();

        if (!ArtifactAppearanceCatalogLoader.Current.Templates.TryGetValue(
                _draft.Blueprint.Appearance.template_key, out ArtifactAppearanceTemplateDef currentTemplate))
            return;
        ArtifactAppearancePart[] parts = _draft.Blueprint.Appearance.parts ?? [];
        foreach (ArtifactAppearancePlacementDef placement in currentTemplate.Placements.OrderBy(item => item.Z))
        {
            int partIndex = Array.FindIndex(parts, part => part.slot == placement.Slot);
            if (partIndex < 0) continue;
            ArtifactAppearancePart part = parts[partIndex];
            ArtifactAppearanceModuleDef module = ArtifactAppearanceCatalogLoader.Current.Modules[placement.Module];
            BaibaoEditorRow row = _rowPool.GetNext();
            row.Setup(BaibaoPresentation.GetModuleName(module.Key),
                BaibaoPresentation.GetVariantName(module.Key, part.variant), string.Empty,
                BaibaoUiIcons.Appearance, false, false, null,
                SpriteTextureLoader.getSprite(BaibaoUiIcons.Appearance));
            ArtifactAppearanceVariantDef[] candidates = module.Variants
                .Where(variant => variant.GetAnchor(placement.Anchor) != null).ToArray();
            int variantRows = Mathf.CeilToInt(candidates.Length / 3f);
            float controlsHeight = variantRows * 24f + Mathf.Max(0, variantRows - 1) * 2f;
            Transform controls = row.UseControls(controlsHeight);
            BuildVariantControls(controls, placement, module, candidates, part);
        }
    }

    private void BuildColorRoleControls()
    {
        const int schemesPerLine = 12;
        ArtifactAppearanceCatalog catalog = ArtifactAppearanceCatalogLoader.Current;
        ArtifactAppearance appearance = _draft.Blueprint.Appearance;
        ArtifactAppearanceColorSchemeDef[] schemes = catalog.ColorSchemes.Values
            .OrderBy(scheme => scheme.Key, StringComparer.Ordinal)
            .ToArray();
        string summary = string.Join("  ·  ", catalog.ColorRoles.Select(role =>
            $"{ColorRoleName(role.Key)} {BaibaoPresentation.GetColorSchemeName(ResolveColorRoleScheme(appearance, role))}"));
        BaibaoEditorRow row = _rowPool.GetNext();
        row.Setup("Cultiway.Baibao.UI.Label.ColorPlan".Localize(), summary, string.Empty,
            BaibaoUiIcons.Appearance, false, false, null,
            SpriteTextureLoader.getSprite(BaibaoUiIcons.Appearance));
        row.SetTooltip("Cultiway.Baibao.UI.Label.ColorPlan".Localize(),
            "Cultiway.Baibao.UI.Tooltip.ColorPlan".Localize());

        int linesPerRole = Mathf.CeilToInt(schemes.Length / (float)schemesPerLine);
        int totalLines = catalog.ColorRoles.Length * linesPerRole;
        Transform controls = row.UseControls(totalLines * 22f + Mathf.Max(0, totalLines - 1) * 2f);
        for (int roleIndex = 0; roleIndex < catalog.ColorRoles.Length; roleIndex++)
        {
            ArtifactAppearanceColorRoleDef role = catalog.ColorRoles[roleIndex];
            string selectedScheme = ResolveColorRoleScheme(appearance, role);
            for (int start = 0; start < schemes.Length; start += schemesPerLine)
            {
                GameObject line = UiLayout.Create(controls, $"{role.Key}{start / schemesPerLine}", true,
                    306f, 22f, 2f, TextAnchor.MiddleLeft);
                UiElements.CreateText(line.transform, "Role",
                    start == 0 ? ColorRoleName(role.Key) : string.Empty,
                    48f, 21f, 7, TextAnchor.MiddleLeft, FontStyle.Bold);
                int count = Mathf.Min(schemesPerLine, schemes.Length - start);
                for (int i = 0; i < count; i++)
                {
                    ArtifactAppearanceColorSchemeDef scheme = schemes[start + i];
                    Button swatch = UiElements.CreateSwatchButton(line.transform, scheme.Key,
                        BaibaoPresentation.GetColorSchemeSwatch(scheme, role), 18f,
                        () => SelectColorRole(role.Key, scheme.Key));
                    UiStateStyle.SetSelected(swatch, selectedScheme == scheme.Key);
                    UiTooltip.Set(swatch.gameObject,
                        $"{ColorRoleName(role.Key)} · {BaibaoPresentation.GetColorSchemeName(scheme.Key)}",
                        ColorRoleDescription(role.Key));
                }
            }
        }
    }

    private void BuildVariantControls(
        Transform controls,
        ArtifactAppearancePlacementDef placement,
        ArtifactAppearanceModuleDef module,
        IReadOnlyList<ArtifactAppearanceVariantDef> variants,
        ArtifactAppearancePart selectedPart)
    {
        for (int start = 0; start < variants.Count; start += 3)
        {
            int count = Mathf.Min(3, variants.Count - start);
            GameObject line = UiLayout.Create(controls, $"Variants{start / 3}", true, 306f, 22f, 3f);
            float width = (306f - (count - 1) * 3f) / count;
            for (int i = 0; i < count; i++)
            {
                ArtifactAppearanceVariantDef variant = variants[start + i];
                Button button = UiElements.CreateButton(line.transform, variant.Key,
                    BaibaoPresentation.GetVariantName(module.Key, variant.Key), width, 21f,
                    () => SelectVariant(placement.Slot, variant.Key));
                UiStateStyle.SetSelected(button, selectedPart.variant == variant.Key);
            }
        }
    }

    private static string ResolveColorRoleScheme(
        ArtifactAppearance appearance,
        ArtifactAppearanceColorRoleDef role)
    {
        ArtifactAppearanceColorRole[] selections = appearance.color_roles ?? [];
        for (int i = 0; i < selections.Length; i++)
        {
            if (selections[i].role == role.Key) return selections[i].color_scheme;
        }
        ArtifactAppearancePart[] parts = appearance.parts ?? [];
        string fallback = parts.Select(part => part.color_scheme).FirstOrDefault(key => !string.IsNullOrEmpty(key));
        return !string.IsNullOrEmpty(fallback)
            ? fallback
            : ArtifactAppearanceCatalogLoader.Current.ColorSchemes.Keys
                .OrderBy(key => key, StringComparer.Ordinal)
                .First();
    }

    private static string ColorRoleName(string role)
    {
        return $"Cultiway.Baibao.Appearance.ColorRole.{role}".Localize();
    }

    private static string ColorRoleDescription(string role)
    {
        return $"Cultiway.Baibao.Appearance.ColorRole.{role}.Description".Localize();
    }

    private void BuildAbilityPage()
    {
        ArtifactAbilityInstance[] abilities = _draft.Blueprint.AbilitySet.abilities ?? [];
        if (abilities.Length == 0)
        {
            _rowPool.GetNext().Setup("Cultiway.Baibao.UI.State.NoAbility".Localize(),
                "Cultiway.Baibao.UI.Detail.AbilityDerived".Localize(), string.Empty,
                BaibaoUiIcons.Ability, false, false, null,
                SpriteTextureLoader.getSprite(BaibaoUiIcons.Ability));
            return;
        }
        for (int i = 0; i < abilities.Length; i++)
        {
            ArtifactAbilityInstance instance = abilities[i];
            ArtifactAbilityAsset asset = Cultiway.Content.Libraries.Manager.ArtifactAbilityLibrary
                .get(instance.ability_id);
            string type = asset?.active_use == null
                ? "Cultiway.Baibao.UI.Ability.Passive".Localize()
                : "Cultiway.Baibao.UI.Ability.Active".Localize();
            string description = BaibaoPresentation.GetAbilityDescription(instance);
            _rowPool.GetNext().Setup(BaibaoPresentation.GetAbilityName(instance.ability_id),
                string.IsNullOrWhiteSpace(description) ? type : $"{type}  ·  {description}",
                string.Empty, BaibaoUiIcons.Ability, false, false, null,
                SpriteTextureLoader.getSprite(BaibaoUiIcons.Ability));
        }
    }

    private void BuildOverviewPage()
    {
        ArtifactBlueprint blueprint = _draft.Blueprint;
        _rowPool.GetNext().Setup("Cultiway.Baibao.UI.Overview.Identity".Localize(),
            $"{blueprint.Name}  ·  {BaibaoPresentation.GetShapeName(blueprint)}  ·  {blueprint.Level.GetName()}",
            string.Empty, UiIcons.Info, false, false, null,
            BaibaoPavilionService.Instance.GetPreviewIcon(blueprint));
        string atoms = string.Join("、", (blueprint.AtomData.entries ?? [])
            .Select(entry => Cultiway.Content.Libraries.Manager.ArtifactAtomLibrary.get(entry.atom_id))
            .Where(atom => atom != null)
            .Select(BaibaoPresentation.GetAtomName));
        _rowPool.GetNext().Setup("Cultiway.Baibao.UI.Overview.Composition".Localize(), atoms,
            string.Empty, BaibaoUiIcons.Composition, false, false, null,
            SpriteTextureLoader.getSprite(BaibaoUiIcons.Composition));
        _rowPool.GetNext().Setup("Cultiway.Baibao.UI.Overview.Appearance".Localize(),
            $"{BaibaoPresentation.GetTemplateName(blueprint.Appearance.template_key)}  ·  " +
            string.Format("Cultiway.Baibao.UI.Format.TemplateSlots".Localize(),
                blueprint.Appearance.parts?.Length ?? 0), string.Empty, BaibaoUiIcons.Appearance, false, false, null,
            SpriteTextureLoader.getSprite(BaibaoUiIcons.Appearance));
        string abilities = string.Join("、", (blueprint.AbilitySet.abilities ?? [])
            .Select(ability => BaibaoPresentation.GetAbilityName(ability.ability_id)));
        _rowPool.GetNext().Setup("Cultiway.Baibao.UI.Overview.Abilities".Localize(),
            string.IsNullOrEmpty(abilities) ? "Cultiway.Baibao.UI.State.None".Localize() : abilities,
            string.Empty, BaibaoUiIcons.Ability, false, false, null,
            SpriteTextureLoader.getSprite(BaibaoUiIcons.Ability));
        string error = BaibaoPavilionService.Instance.Validate(blueprint);
        _rowPool.GetNext().Setup("Cultiway.Baibao.UI.Overview.Validation".Localize(),
            error ?? "Cultiway.Baibao.UI.State.BlueprintValid".Localize(), string.Empty,
            error == null ? UiIcons.Confirm : UiIcons.Info, error == null, false, null,
            SpriteTextureLoader.getSprite(error == null ? UiIcons.Confirm : UiIcons.Info));
    }

    private ArtifactBlueprint BuildShapeCandidate(ArtifactShapeAsset shape)
    {
        ArtifactComposeResult result = ArtifactComposer.ComposeDesign(new ArtifactDesignRequest
        {
            Shape = shape,
            Level = _draft.Blueprint.Level,
            AtomEntries = _draft.Blueprint.AtomData.entries ?? [],
        });
        return ArtifactBlueprintCodec.FromComposeResult(result);
    }

    private ArtifactBlueprint BuildAppearanceCandidate(ArtifactAppearanceTemplateDef template)
    {
        ArtifactAppearance appearance = BuildAppearance(template);
        ArtifactBlueprint candidate = _draft.Blueprint.DeepClone();
        candidate.Appearance = appearance;
        return candidate;
    }

    private ArtifactAppearance BuildAppearance(ArtifactAppearanceTemplateDef template)
    {
        ArtifactAppearanceCatalog catalog = ArtifactAppearanceCatalogLoader.Current;
        ArtifactAppearance currentAppearance = _draft.Blueprint.Appearance;
        ArtifactAppearancePart[] current = currentAppearance.parts ?? [];
        string defaultScheme = ResolveColorRoleScheme(currentAppearance, catalog.BaseColorRole);
        List<ArtifactAppearancePart> parts = new();
        foreach (ArtifactAppearancePlacementDef placement in template.Placements.OrderBy(item => item.Z))
        {
            ArtifactAppearanceModuleDef module = catalog.Modules[placement.Module];
            ArtifactAppearancePart existing = current.FirstOrDefault(part => part.slot == placement.Slot &&
                                                                              part.module == placement.Module);
            ArtifactAppearanceVariantDef variant = module.GetVariant(existing.variant);
            if (variant == null || variant.GetAnchor(placement.Anchor) == null)
            {
                variant = module.Variants.First(item => item.GetAnchor(placement.Anchor) != null);
            }
            string scheme = !string.IsNullOrEmpty(existing.color_scheme) &&
                            catalog.ColorSchemes.ContainsKey(existing.color_scheme)
                ? existing.color_scheme
                : defaultScheme;
            parts.Add(new ArtifactAppearancePart
            {
                slot = placement.Slot,
                module = placement.Module,
                variant = variant.Key,
                color_scheme = scheme,
                colors = existing.colors ?? [],
            });
        }
        return new ArtifactAppearance
        {
            template_key = template.Key,
            color_roles = currentAppearance.color_roles?.ToArray() ?? [],
            parts = parts.ToArray(),
        };
    }

    private ArtifactShapeAsset CurrentShape()
    {
        return (ArtifactShapeAsset)ModClass.L.ItemShapeLibrary.get(_draft.Blueprint.ShapeId);
    }

    private void Recompose()
    {
        ArtifactBlueprint previous = _draft.Blueprint;
        ArtifactComposeResult result = ArtifactComposer.ComposeDesign(new ArtifactDesignRequest
        {
            Shape = (ArtifactShapeAsset)ModClass.L.ItemShapeLibrary.get(previous.ShapeId),
            Level = previous.Level,
            AtomEntries = previous.AtomData.entries ?? [],
            Name = _draft.AutoName ? null : previous.Name,
            AppearanceOverride = _draft.AutoAppearance ? null : previous.Appearance,
        });
        ArtifactBlueprint next = ArtifactBlueprintCodec.FromComposeResult(result);
        ArtifactBlueprint metadata = previous.DeepClone();
        next.Id = metadata.Id;
        next.Extensions = metadata.Extensions;
        next.OriginKind = metadata.OriginKind;
        next.SourceActorId = metadata.SourceActorId;
        next.SourceActorName = metadata.SourceActorName;
        next.Favorite = metadata.Favorite;
        next.SortOrder = metadata.SortOrder;
        next.CreatedAtUtcTicks = metadata.CreatedAtUtcTicks;
        next.UpdatedAtUtcTicks = DateTime.UtcNow.Ticks;
        _draft.Blueprint = next;
    }

    private void ApplyMutation(Action mutation, bool recompose = true)
    {
        _undo.Push(_draft.DeepClone());
        _redo.Clear();
        mutation();
        if (recompose) Recompose();
        RefreshAll();
    }

    private void SelectShape(ArtifactShapeAsset shape)
    {
        if (_draft.Blueprint.ShapeId == shape.id) return;
        ApplyMutation(() =>
        {
            _draft.Blueprint.ShapeId = shape.id;
            _draft.AutoAppearance = true;
        });
    }

    private void SetQualityStage(int stage)
    {
        if (_draft.Blueprint.Level.Stage == stage) return;
        ApplyMutation(() =>
        {
            ItemLevel level = _draft.Blueprint.Level;
            level.Stage = stage;
            _draft.Blueprint.Level = level;
        });
    }

    private void StepQualityLevel(int direction)
    {
        int value = Mathf.Clamp(_draft.Blueprint.Level.Level + direction, 0, 8);
        if (value == _draft.Blueprint.Level.Level) return;
        ApplyMutation(() =>
        {
            ItemLevel level = _draft.Blueprint.Level;
            level.Level = value;
            _draft.Blueprint.Level = level;
        });
    }

    private void AddAtom(ArtifactAtomAsset atom)
    {
        ApplyMutation(() =>
        {
            List<ArtifactAtomEntry> entries = (_draft.Blueprint.AtomData.entries ?? []).ToList();
            entries.Add(new ArtifactAtomEntry { atom_id = atom.id, strength = 1f });
            _draft.Blueprint.AtomData = new ArtifactAtomData { entries = entries.ToArray() };
        });
    }

    private void ShowAtomPicker(ArtifactAtomCategory category)
    {
        _atomPicker.Show(category, _atoms);
    }

    private void ToggleAtomFromPicker(ArtifactAtomAsset atom)
    {
        ArtifactAtomEntry[] entries = _draft.Blueprint.AtomData.entries ?? [];
        if (entries.Any(entry => entry.atom_id == atom.id))
        {
            if (atom.category != ArtifactAtomCategory.Shape) RemoveAtom(atom.id);
            return;
        }
        if (atom.category != ArtifactAtomCategory.Shape)
        {
            AddAtom(atom);
            return;
        }

        ApplyMutation(() =>
        {
            List<ArtifactAtomEntry> next = entries.Where(entry =>
            {
                ArtifactAtomAsset current = Cultiway.Content.Libraries.Manager.ArtifactAtomLibrary.get(entry.atom_id);
                return current?.category != ArtifactAtomCategory.Shape;
            }).ToList();
            next.Add(new ArtifactAtomEntry { atom_id = atom.id, strength = 1f });
            _draft.Blueprint.AtomData = new ArtifactAtomData { entries = next.ToArray() };
        });
    }

    private void RemoveAtom(string atomId)
    {
        ApplyMutation(() =>
        {
            ArtifactAtomEntry[] entries = (_draft.Blueprint.AtomData.entries ?? [])
                .Where(entry => entry.atom_id != atomId).ToArray();
            _draft.Blueprint.AtomData = new ArtifactAtomData { entries = entries };
        });
    }

    private void StepAtomStrength(string atomId, float delta)
    {
        ApplyMutation(() =>
        {
            ArtifactAtomEntry[] entries = (_draft.Blueprint.AtomData.entries ?? []).ToArray();
            int index = Array.FindIndex(entries, entry => entry.atom_id == atomId);
            ArtifactAtomEntry entry = entries[index];
            entry.strength = Mathf.Max(0.25f, entry.strength + delta);
            entries[index] = entry;
            _draft.Blueprint.AtomData = new ArtifactAtomData { entries = entries };
        });
    }

    private void SelectTemplate(ArtifactAppearanceTemplateDef template)
    {
        ApplyMutation(() =>
        {
            _draft.AutoAppearance = false;
            _draft.Blueprint.Appearance = BuildAppearance(template);
        });
    }

    private void SelectVariant(string slot, string variant)
    {
        ApplyMutation(() =>
        {
            _draft.AutoAppearance = false;
            ArtifactAppearance appearance = _draft.Blueprint.Appearance;
            ArtifactAppearancePart[] parts = appearance.parts.ToArray();
            int index = Array.FindIndex(parts, part => part.slot == slot);
            ArtifactAppearancePart part = parts[index];
            part.variant = variant;
            parts[index] = part;
            appearance.parts = parts;
            _draft.Blueprint.Appearance = appearance;
        });
    }

    private void SelectColorRole(string roleKey, string schemeKey)
    {
        ApplyMutation(() =>
        {
            _draft.AutoAppearance = false;
            ArtifactAppearanceCatalog catalog = ArtifactAppearanceCatalogLoader.Current;
            ArtifactAppearance appearance = _draft.Blueprint.Appearance;
            ArtifactAppearanceColorRoleDef selectedRole = catalog.ColorRoles.First(role => role.Key == roleKey);
            ArtifactAppearanceColorRole[] roles = new ArtifactAppearanceColorRole[catalog.ColorRoles.Length];
            for (int i = 0; i < catalog.ColorRoles.Length; i++)
            {
                ArtifactAppearanceColorRoleDef role = catalog.ColorRoles[i];
                roles[i] = new ArtifactAppearanceColorRole
                {
                    role = role.Key,
                    color_scheme = role.Key == roleKey ? schemeKey : ResolveColorRoleScheme(appearance, role),
                };
            }
            appearance.color_roles = roles;

            string baseScheme = roles.First(role => role.role == catalog.BaseColorRole.Key).color_scheme;
            HashSet<string> selectedChannels = new(selectedRole.Channels, StringComparer.Ordinal);
            ArtifactAppearancePart[] parts = appearance.parts.ToArray();
            for (int i = 0; i < parts.Length; i++)
            {
                ArtifactAppearancePart part = parts[i];
                part.color_scheme = baseScheme;
                part.colors = (part.colors ?? [])
                    .Where(color => !selectedChannels.Contains(color.material))
                    .ToArray();
                parts[i] = part;
            }
            appearance.parts = parts;
            _draft.Blueprint.Appearance = appearance;
        });
    }

    private void ToggleAppearanceMode()
    {
        ApplyMutation(() => _draft.AutoAppearance = !_draft.AutoAppearance);
    }

    private void ApplyCustomName(string value)
    {
        string name = value.Trim();
        if (name.Length == 0 || !_draft.AutoName && name == _draft.Blueprint.Name)
        {
            RefreshHeader();
            return;
        }
        ApplyMutation(() =>
        {
            _draft.AutoName = false;
            _draft.Blueprint.Name = name;
        });
    }

    private void ToggleNameMode()
    {
        ApplyMutation(() => _draft.AutoName = !_draft.AutoName);
    }

    private void SelectPage(EditorPage page)
    {
        _page = page;
        RefreshPage();
    }

    private void Undo()
    {
        if (_undo.Count == 0) return;
        _redo.Push(_draft.DeepClone());
        _draft = _undo.Pop();
        RefreshAll();
    }

    private void Redo()
    {
        if (_redo.Count == 0) return;
        _undo.Push(_draft.DeepClone());
        _draft = _redo.Pop();
        RefreshAll();
    }

    private void DiscardChanges()
    {
        _draft = _savedDraft.DeepClone();
        _undo.Clear();
        _redo.Clear();
        RefreshAll();
    }

    private void TrySave()
    {
        if (_existing && !IsDirty())
        {
            WorldTip.showNow("Cultiway.Baibao.UI.Tip.NoChanges".Localize(), false, "top", 2f);
            return;
        }
        BaibaoSaveResult result = _canUpdate
            ? BaibaoPavilionService.Instance.Update(_draft.Blueprint)
            : _saveAsCopy
                ? BaibaoPavilionService.Instance.SaveCopy(_draft.Blueprint)
                : BaibaoPavilionService.Instance.SaveNew(_draft.Blueprint);
        HandleSaveResult(result, _canUpdate
            ? "Cultiway.Baibao.UI.Tip.Updated"
            : "Cultiway.Baibao.UI.Tip.Forged");
    }

    private void SaveCopy()
    {
        HandleSaveResult(BaibaoPavilionService.Instance.SaveCopy(_draft.Blueprint),
            "Cultiway.Baibao.UI.Tip.CopySaved");
    }

    private bool HandleSaveResult(BaibaoSaveResult result, string savedKey)
    {
        WindowBaibaoPavilion.ShowSaveResult(result, savedKey);
        if (result.Status != BaibaoSaveStatus.Saved) return false;
        _draft.Blueprint = result.Blueprint.DeepClone();
        _savedDraft = _draft.DeepClone();
        _existing = true;
        _canUpdate = true;
        _saveAsCopy = false;
        _undo.Clear();
        _redo.Clear();
        RefreshAll();
        return true;
    }

    private bool IsDirty()
    {
        if (!_existing) return true;
        return JsonConvert.SerializeObject(_draft.Blueprint) !=
               JsonConvert.SerializeObject(_savedDraft.Blueprint);
    }

    private IEnumerator ReopenAfterClose()
    {
        yield return new WaitForEndOfFrame();
        if (_resumeAfterClose) ScrollWindow.showWindow(Id, true, false);
    }

    private void RequestBack()
    {
        RequestExit(ExitMode.Back);
    }

    private void RequestExit(ExitMode mode)
    {
        _pendingExitMode = mode;
        if (IsDirty())
        {
            SetConfirmation(true);
            return;
        }
        CompleteExit();
    }

    private void CompleteExit()
    {
        _closingApproved = true;
        SetConfirmation(false);
        if (_pendingExitMode == ExitMode.Back)
        {
            WindowHistory.clickBack();
            return;
        }
        GetComponent<ScrollWindow>().clickHide();
    }

    private void CreateConfirmationPanel()
    {
        GameObject panel = UiLayout.Create(BackgroundTransform, "UnsavedConfirmation", false, 282f,
            94f, 6f, TextAnchor.MiddleCenter);
        panel.transform.localPosition = Vector3.zero;
        Image background = panel.AddComponent<Image>();
        UiResources.ApplySurface(background, UiSurface.WindowEmpty);
        UiElements.CreateText(panel.transform, "Message",
            "Cultiway.Baibao.UI.Tip.UnsavedChanges".Localize(), 272f, 30f, 9,
            TextAnchor.MiddleCenter, FontStyle.Bold);
        GameObject actions = UiLayout.Create(panel.transform, "Actions", true, 272f, 28f, 5f,
            TextAnchor.MiddleCenter);
        UiElements.CreateIconTextButton(actions.transform, "Save", UiIcons.Save,
            "Cultiway.Baibao.UI.Action.SaveAndClose".Localize(), 86f, 24f, () =>
            {
                BaibaoSaveResult result = _canUpdate
                    ? BaibaoPavilionService.Instance.Update(_draft.Blueprint)
                    : _saveAsCopy
                        ? BaibaoPavilionService.Instance.SaveCopy(_draft.Blueprint)
                        : BaibaoPavilionService.Instance.SaveNew(_draft.Blueprint);
                if (!HandleSaveResult(result, "Cultiway.Baibao.UI.Tip.Forged")) return;
                CompleteExit();
            });
        UiElements.CreateIconTextButton(actions.transform, "Discard", UiIcons.Reset,
            "Cultiway.Baibao.UI.Action.Discard".Localize(), 82f, 24f, CompleteExit);
        UiElements.CreateIconTextButton(actions.transform, "Continue", UiIcons.Edit,
            "Cultiway.Baibao.UI.Action.ContinueEditing".Localize(), 94f, 24f, () => SetConfirmation(false));
        _confirmation = new UiModal(panel, _editorCanvas);
    }

    private void SetConfirmation(bool visible)
    {
        if (visible) _confirmation.Show();
        else _confirmation.Hide();
    }

    private IEnumerator BindBackButtonAfterScrollWindowStart()
    {
        yield return null;
        Transform back = transform.Find("BackButtonContainer");
        foreach (Button button in back.GetComponentsInChildren<Button>(true))
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(RequestBack);
        }
    }
}
