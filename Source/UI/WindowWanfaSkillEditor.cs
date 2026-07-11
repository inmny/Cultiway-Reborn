using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Core.SkillLibV3.Wanfa;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Blueprints;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Editor;
using Cultiway.Core.SkillLibV3.Utils;
using Friflo.Engine.ECS;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;
using Cultiway.Utils.Extension;
using NeoModLoader.api;
using Cultiway.UI.Components;
using Cultiway.UI.Prefab;

namespace Cultiway.UI;

public sealed class WindowWanfaSkillEditor : AbstractWideWindow<WindowWanfaSkillEditor>
{
    private enum ExitMode
    {
        Close,
        Back
    }

    public const string Id = "Cultiway.UI.WindowWanfaSkillEditor";
    private static SkillBlueprint _pendingDraft;
    private static bool _pendingExisting;
    private static bool _pendingActorEdit;
    private static Actor _pendingSourceActor;
    private static Entity _pendingSourceSkill;
    private static WindowWanfaSkillEditor _instance;

    private readonly Stack<SkillBlueprint> _undo = new();
    private readonly Stack<SkillBlueprint> _redo = new();
    private SkillBlueprint _draft;
    private SkillBlueprint _savedDraft;
    private bool _isExisting;
    private bool _closingApproved;
    private bool _resumeAfterClose;
    private bool _resumeAfterTestCast;
    private bool _actorEdit;
    private bool _actorReplacementPending;
    private ExitMode _pendingExitMode;
    private Actor _sourceActor;
    private Entity _sourceSkill;
    private int _page;
    private InputField _nameInput;
    private Button _nameModeButton;
    private Toggle _aiNaming;
    private InputField _categoryInput;
    private Image _previewImage;
    private Text _previewSummary;
    private Text _draftState;
    private Button[] _tabButtons;
    private Button _undoButton;
    private Button _redoButton;
    private Button _testCastButton;
    private MonoObjPool<WanfaEditorRow> _rowPool;
    private GameObject _confirmPanel;
    private CanvasGroup _editorCanvasGroup;
    private Entity _previewContainer;
    private Sprite[] _frames = Array.Empty<Sprite>();
    private int _frameIndex;
    private float _frameTimer;

    public static void Open(SkillBlueprint blueprint, bool existing)
    {
        _pendingDraft = blueprint.DeepClone();
        _pendingExisting = existing;
        _pendingActorEdit = false;
        _pendingSourceActor = null;
        _pendingSourceSkill = default;
        ScrollWindow.showWindow(Id);
    }

    public static void OpenForActor(SkillBlueprint blueprint, Actor actor, Entity sourceSkill)
    {
        _pendingDraft = blueprint.DeepClone();
        _pendingExisting = true;
        _pendingActorEdit = true;
        _pendingSourceActor = actor;
        _pendingSourceSkill = sourceSkill;
        ScrollWindow.showWindow(Id);
    }

    internal static void ClearWorldState()
    {
        _pendingDraft = null;
        _pendingExisting = false;
        _pendingActorEdit = false;
        _pendingSourceActor = null;
        _pendingSourceSkill = default;
        if (_instance == null) return;

        _instance._closingApproved = true;
        _instance._resumeAfterClose = false;
        _instance._resumeAfterTestCast = false;
        _instance._actorEdit = false;
        _instance._actorReplacementPending = false;
        _instance._sourceActor = null;
        _instance._sourceSkill = default;
        _instance.ReleasePreview();
        _instance._draft = null;
        if (_instance.gameObject.activeInHierarchy)
        {
            _instance.GetComponent<ScrollWindow>().clickHide();
        }
    }

    protected override void Init()
    {
        _instance = this;
        BackgroundTransform.Find("Scroll View").gameObject.SetActive(false);
        var root = WanfaUiFactory.CreateLayout(BackgroundTransform, "EditorRoot", false, 520f, 238f, 3f);
        _editorCanvasGroup = root.AddComponent<CanvasGroup>();
        root.transform.localPosition = new Vector3(0f, -8f);

        var header = WanfaUiFactory.CreateLayout(root.transform, "Header", true, 520f, 24f, 4f);
        _nameInput = WanfaUiFactory.CreateInput(header.transform, "Name", string.Empty,
            "Cultiway.Wanfa.UI.Placeholder.SkillName".Localize(), 165f, 22f);
        _nameInput.characterLimit = 24;
        _nameInput.onEndEdit.AddListener(ApplyCustomName);
        WanfaUiFactory.SetTooltip(_nameInput, "Cultiway.Wanfa.UI.Placeholder.SkillName",
            "Cultiway.Wanfa.UI.Tooltip.SkillName");
        _nameModeButton = WanfaUiFactory.CreateIconTextButton(header.transform, "NameMode", WanfaUiIcons.NamingMode,
            "Cultiway.Wanfa.UI.NameMode.Rule".Localize(), 88f, 22f, ToggleNameMode);
        WanfaUiFactory.SetTooltip(_nameModeButton.gameObject, "Cultiway.Wanfa.UI.Tooltip.NameMode.Title",
            "Cultiway.Wanfa.UI.Tooltip.NameMode");
        _aiNaming = WanfaUiFactory.CreateIconToggle(header.transform, "AiNaming", WanfaUiIcons.AiNaming, true, 28f,
            22f);
        WanfaUiFactory.SetTooltip(_aiNaming, "Cultiway.Wanfa.UI.Label.AiNaming",
            "Cultiway.Wanfa.UI.Tooltip.AiNaming");
        _aiNaming.onValueChanged.AddListener(value =>
        {
            if (_draft == null || _draft.AiNamingEnabled == value) return;
            ApplyMutation(() =>
            {
                _draft.AiNamingEnabled = value;
                if (!value) _draft.GeneratedName = null;
            });
        });
        _draftState = WanfaUiFactory.CreateText(header.transform, "DraftState", "", 227f, 22f, 6);

        var preview = WanfaUiFactory.CreateLayout(root.transform, "Preview", true, 520f, 46f, 6f);
        var imageObject = new GameObject("Image", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        imageObject.transform.SetParent(preview.transform, false);
        WanfaUiFactory.SetLayout(imageObject.transform, 46f, 46f);
        _previewImage = imageObject.GetComponent<Image>();
        _previewImage.preserveAspect = true;
        WanfaUiFactory.SetTooltip(imageObject, () =>
        {
            if (_draft != null) SkillTooltip.Show(imageObject, _draft);
        });
        _previewSummary = WanfaUiFactory.CreateText(preview.transform, "Summary", string.Empty, 356f, 46f, 7,
            TextAnchor.MiddleLeft);
        _categoryInput = WanfaUiFactory.CreateInput(preview.transform, "Category", string.Empty,
            "Cultiway.Wanfa.UI.Placeholder.Category".Localize(), 106f, 22f);
        _categoryInput.characterLimit = 12;
        _categoryInput.onEndEdit.AddListener(ApplyCategory);
        WanfaUiFactory.SetTooltip(_categoryInput, "Cultiway.Wanfa.UI.Placeholder.Category",
            "Cultiway.Wanfa.UI.Tooltip.Category");

        var tabs = WanfaUiFactory.CreateLayout(root.transform, "Tabs", true, 520f, 22f, 4f);
        var names = new[]
        {
            "Cultiway.Wanfa.UI.Tab.Entity".Localize(),
            "Cultiway.Wanfa.UI.Tab.Trajectory".Localize(),
            "Cultiway.Wanfa.UI.Tab.Modifier".Localize(),
            "Cultiway.Wanfa.UI.Tab.Overview".Localize()
        };
        var icons = new[]
        {
            WanfaUiIcons.Entity, WanfaUiIcons.Trajectory, WanfaUiIcons.Modifier, WanfaUiIcons.Overview
        };
        var tooltipKeys = new[]
        {
            "Cultiway.Wanfa.UI.Tooltip.Tab.Entity", "Cultiway.Wanfa.UI.Tooltip.Tab.Trajectory",
            "Cultiway.Wanfa.UI.Tooltip.Tab.Modifier", "Cultiway.Wanfa.UI.Tooltip.Tab.Overview"
        };
        _tabButtons = new Button[names.Length];
        for (var i = 0; i < names.Length; i++)
        {
            var page = i;
            _tabButtons[i] = WanfaUiFactory.CreateIconTextButton(tabs.transform, names[i], icons[i], names[i], 126f,
                21f, () => SelectPage(page));
            WanfaUiFactory.SetTooltip(_tabButtons[i].gameObject, names[i], tooltipKeys[i]);
        }

        var content = WanfaUiFactory.CreateScrollContent(root.transform, "EditorContent", 520f, 108f);
        _rowPool = new MonoObjPool<WanfaEditorRow>(WanfaEditorRow.Prefab, content);

        var footer = WanfaUiFactory.CreateLayout(root.transform, "Footer", true, 520f, 25f, 4f);
        _undoButton = CreateFooterButton(footer.transform, "Undo", WanfaUiIcons.Undo,
            "Cultiway.Wanfa.UI.Action.Undo", "Cultiway.Wanfa.UI.Tooltip.Undo", Undo);
        _redoButton = CreateFooterButton(footer.transform, "Redo", WanfaUiIcons.Undo,
            "Cultiway.Wanfa.UI.Action.Redo", "Cultiway.Wanfa.UI.Tooltip.Redo", Redo);
        WanfaUiFactory.SetButtonIcon(_redoButton, WanfaUiIcons.Undo, true);
        CreateFooterButton(footer.transform, "Discard", WanfaUiIcons.Reset,
            "Cultiway.Wanfa.UI.Action.DiscardChanges", "Cultiway.Wanfa.UI.Tooltip.DiscardChanges",
            DiscardChanges);
        CreateFooterButton(footer.transform, "SaveCopy", WanfaUiIcons.Copy,
            "Cultiway.Wanfa.UI.Action.SaveCopy", "Cultiway.Wanfa.UI.Tooltip.SaveCopy", SaveCopy);
        CreateFooterButton(footer.transform, "Save", WanfaUiIcons.Save,
            "Cultiway.Wanfa.UI.Action.Save", "Cultiway.Wanfa.UI.Tooltip.Save", () => TrySave());
        _testCastButton = CreateFooterButton(footer.transform, "Test", WanfaUiIcons.Play,
            "Cultiway.Wanfa.UI.Action.TestCast", "Cultiway.Wanfa.UI.Tooltip.TestCast", TestCast);

        CreateConfirmationPanel();
        PositionBackButton();
    }

    public override void OnNormalEnable()
    {
        StartCoroutine(BindBackButtonAfterScrollWindowStart());
        if (_resumeAfterTestCast)
        {
            _resumeAfterTestCast = false;
            _closingApproved = false;
            _testCastButton.gameObject.SetActive(!_actorEdit);
            RefreshAll();
            return;
        }
        if (_resumeAfterClose)
        {
            _resumeAfterClose = false;
            _testCastButton.gameObject.SetActive(!_actorEdit);
            RefreshAll();
            SetConfirmation(true);
            return;
        }

        _draft = (_pendingDraft ?? WanfaPavilionService.Instance.CreateDraft()).DeepClone();
        _savedDraft = _draft.DeepClone();
        _isExisting = _pendingExisting;
        _actorEdit = _pendingActorEdit;
        _actorReplacementPending = false;
        _sourceActor = _pendingSourceActor;
        _sourceSkill = _pendingSourceSkill;
        _pendingDraft = null;
        _pendingExisting = false;
        _pendingActorEdit = false;
        _pendingSourceActor = null;
        _pendingSourceSkill = default;
        _closingApproved = false;
        _pendingExitMode = ExitMode.Close;
        _undo.Clear();
        _redo.Clear();
        _page = 0;
        _testCastButton.gameObject.SetActive(!_actorEdit);
        SetConfirmation(false);
        RefreshAll();
    }

    public override void OnNormalDisable()
    {
        ReleasePreview();
        if (_draft == null || !IsDirty() || _closingApproved) return;
        _resumeAfterClose = true;
        ModClass.I.StartCoroutine(ReopenAfterClose());
    }

    private void Update()
    {
        if (_frames.Length == 0) return;
        _frameTimer += Time.unscaledDeltaTime;
        if (_frameTimer < 0.1f) return;
        _frameTimer = 0f;
        _frameIndex = (_frameIndex + 1) % _frames.Length;
        _previewImage.sprite = _frames[_frameIndex];
    }

    private IEnumerator ReopenAfterClose()
    {
        yield return new WaitForEndOfFrame();
        if (!_resumeAfterClose) yield break;
        ScrollWindow.showWindow(Id, true, false);
    }

    private void RefreshAll()
    {
        _undoButton.interactable = _undo.Count > 0;
        _redoButton.interactable = _redo.Count > 0;
        RefreshPreview();
        RefreshHeader();
        RefreshPage();
    }

    private static Button CreateFooterButton(Transform parent, string name, string iconPath, string titleKey,
        string descriptionKey, UnityEngine.Events.UnityAction action)
    {
        var button = WanfaUiFactory.CreateIconButton(parent, name, iconPath, 30f, 22f, action);
        WanfaUiFactory.SetTooltip(button.gameObject, titleKey, descriptionKey);
        return button;
    }

    private void RefreshHeader()
    {
        _nameInput.SetTextWithoutNotify(_draft.NameMode == SkillBlueprintNameMode.Custom
            ? _draft.CustomName
            : WanfaPavilionService.Instance.GetDisplayName(_draft));
        _nameModeButton.GetComponentInChildren<Text>().text =
            _draft.NameMode == SkillBlueprintNameMode.Custom
                ? "Cultiway.Wanfa.UI.NameMode.Custom".Localize()
                : "Cultiway.Wanfa.UI.NameMode.Rule".Localize();
        _aiNaming.SetIsOnWithoutNotify(_draft.AiNamingEnabled);
        _aiNaming.interactable = _draft.NameMode == SkillBlueprintNameMode.Rule;
        _categoryInput.SetTextWithoutNotify(_draft.Category);
        var revision = _isExisting
            ? string.Format("Cultiway.Wanfa.UI.Format.Revision".Localize(), _draft.Revision)
            : "Cultiway.Wanfa.UI.State.NewBlueprint".Localize();
        var state = IsDirty()
            ? "Cultiway.Wanfa.UI.State.Unsaved".Localize()
            : "Cultiway.Wanfa.UI.State.Saved".Localize();
        _draftState.text = $"{revision}  {state}";
    }

    private void RefreshPreview()
    {
        RefreshRuleName();
        var entity = string.IsNullOrWhiteSpace(_draft.EntityAssetId)
            ? null
            : ModClass.I.SkillV3.SkillLib.get(_draft.EntityAssetId);
        _frames = entity == null || !entity.IsAnimationIndexValid(_draft.AnimationIndex)
            ? Array.Empty<Sprite>()
            : entity.GetAnimation(_draft.AnimationIndex).Frames;
        _frameIndex = 0;
        _frameTimer = 0f;
        _previewImage.sprite = _frames.Length == 0 ? null : _frames[0];
        var entityName = string.IsNullOrWhiteSpace(_draft.EntityAssetId)
            ? "Cultiway.Wanfa.UI.State.NoEntity".Localize()
            : _draft.EntityAssetId.Localize();
        var trajectoryName = string.IsNullOrWhiteSpace(_draft.TrajectoryAssetId)
            ? "Cultiway.Wanfa.UI.State.NoTrajectory".Localize()
            : _draft.TrajectoryAssetId.Localize();
        _previewSummary.text = $"{entityName}\n{trajectoryName}  ·  " +
                               string.Format("Cultiway.Wanfa.UI.Format.ModifierCount".Localize(),
                                   _draft.Modifiers.Count);
    }

    private void RefreshRuleName()
    {
        if (_draft.NameMode != SkillBlueprintNameMode.Rule || !string.IsNullOrWhiteSpace(_draft.RuleName)) return;

        var namingDraft = _draft.DeepClone();
        namingDraft.NameMode = SkillBlueprintNameMode.Rule;
        namingDraft.RuleName = null;
        namingDraft.GeneratedName = null;
        namingDraft.AiNamingEnabled = false;
        var compiled = new SkillBlueprintCompiler().Compile(namingDraft, SkillBlueprintCompileMode.Preview);
        if (!compiled.Success) return;

        _draft.RuleName = compiled.Container.Name.value;
        SkillBlueprintCompiler.Recycle(compiled.Container);
    }

    private void RefreshPage()
    {
        _rowPool.Clear();
        for (var i = 0; i < _tabButtons.Length; i++) _tabButtons[i].interactable = i != _page;
        switch (_page)
        {
            case 0:
                BuildEntityPage();
                break;
            case 1:
                BuildTrajectoryPage();
                break;
            case 2:
                BuildModifierPage();
                break;
            default:
                BuildOverviewPage();
                break;
        }
    }

    private void BuildEntityPage()
    {
        foreach (var entity in ModClass.I.SkillV3.SkillLib.list
                     .Where(item => item.EditorSelectable &&
                                    WanfaPavilionService.Instance.ActivePolicy.IsEntityAvailable(item.id))
                     .OrderBy(item => item.EditorSortOrder))
        {
            var selected = entity.id == _draft.EntityAssetId;
            var trajectoryId = WanfaPavilionService.Instance.ResolveAvailableTrajectoryId(entity,
                selected ? _draft.TrajectoryAssetId : null);
            var detail = $"{entity.EditorCategoryKey.Localize()} · {string.Join("、", entity.SeriesTags.Select(SkillTags.GetDisplayName))}";
            if (trajectoryId == null)
            {
                detail += " · " + "Cultiway.Wanfa.UI.Detail.NoAvailableTrajectory".Localize();
            }
            var row = _rowPool.GetNext();
            row.Setup(entity.id.Localize(), detail,
                selected
                    ? "Cultiway.Wanfa.UI.Action.Selected".Localize()
                    : "Cultiway.Wanfa.UI.Action.Select".Localize(),
                !selected && trajectoryId != null, () => SelectEntity(entity.id),
                selected ? WanfaUiIcons.Confirm : WanfaUiIcons.Select);
            if (selected && entity.Animations.Count > 1)
            {
                BuildAnimationControls(row, entity);
            }
        }
    }

    private void BuildAnimationControls(WanfaEditorRow row, SkillEntityAsset entity)
    {
        var controls = row.UseInlineControls(86f);
        var previous = WanfaUiFactory.CreateIconButton(controls, "Previous", WanfaUiIcons.Previous,
            18f, 20f, () => StepAnimation(entity, -1), 3f);
        WanfaUiFactory.SetTooltip(previous.gameObject, "Cultiway.Wanfa.UI.Action.PreviousAnimation",
            "Cultiway.Wanfa.UI.Tooltip.PreviousAnimation");

        SkillEntityAnimation animation = null;
        if (entity.IsAnimationIndexValid(_draft.AnimationIndex))
        {
            animation = entity.GetAnimation(_draft.AnimationIndex);
        }
        var preview = new GameObject("Preview", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        preview.transform.SetParent(controls, false);
        WanfaUiFactory.SetLayout(preview.transform, 20f, 20f);
        var previewImage = preview.GetComponent<Image>();
        previewImage.sprite = animation == null || animation.Frames.Length == 0 ? null : animation.Frames[0];
        previewImage.preserveAspect = true;
        previewImage.raycastTarget = false;

        WanfaUiFactory.CreateText(controls, "Index",
            $"{_draft.AnimationIndex + 1}/{entity.Animations.Count}", 24f, 20f, 7,
            TextAnchor.MiddleCenter, FontStyle.Bold);

        var next = WanfaUiFactory.CreateIconButton(controls, "Next", WanfaUiIcons.Next,
            18f, 20f, () => StepAnimation(entity, 1), 3f);
        WanfaUiFactory.SetTooltip(next.gameObject, "Cultiway.Wanfa.UI.Action.NextAnimation",
            "Cultiway.Wanfa.UI.Tooltip.NextAnimation");
    }

    private void StepAnimation(SkillEntityAsset entity, int direction)
    {
        ApplyMutation(() =>
        {
            var count = entity.Animations.Count;
            var current = ((_draft.AnimationIndex % count) + count) % count;
            _draft.AnimationIndex = (current + count + direction) % count;
        });
    }

    private void BuildTrajectoryPage()
    {
        foreach (var trajectory in ModClass.I.SkillV3.TrajLib.list
                     .Where(item => item.EditorSelectable || item.id == _draft.TrajectoryAssetId)
                     .OrderBy(item => item.EditorSortOrder))
        {
            var candidate = _draft.DeepClone();
            candidate.TrajectoryAssetId = trajectory.id;
            var selected = trajectory.id == _draft.TrajectoryAssetId;
            var compatibility = trajectory.CheckEditorCompatibility(SkillEditContext.Create(candidate), !selected);
            if (!WanfaPavilionService.Instance.ActivePolicy.IsTrajectoryAvailable(trajectory.id))
            {
                compatibility.AddErrorKey("policy.trajectory_locked",
                    "Cultiway.Wanfa.Validation.policy.trajectory_locked", trajectory.id);
            }
            var reason = compatibility.IsCompatible
                ? trajectory.EditorDescriptionKey.Localize()
                : string.Join("；", compatibility.Issues.Select(issue => issue.Message));
            _rowPool.GetNext().Setup(trajectory.id.Localize(), reason,
                selected
                    ? "Cultiway.Wanfa.UI.Action.Selected".Localize()
                    : "Cultiway.Wanfa.UI.Action.Select".Localize(),
                compatibility.IsCompatible && !selected,
                () => ApplyMutation(() => _draft.TrajectoryAssetId = trajectory.id),
                selected ? WanfaUiIcons.Confirm : WanfaUiIcons.Select);
        }
    }

    private void BuildModifierPage()
    {
        var context = SkillEditContext.Create(_draft);
        var draftValidation = WanfaPavilionService.Instance.Validate(_draft);
        foreach (var modifier in ModClass.I.SkillV3.ModifierLib.list
                     .Where(item => item.EditorComponentType != null)
                     .Where(item => item.EditorSelectable ||
                                    _draft.Modifiers.Any(spec => spec.AssetId == item.id))
                     .OrderBy(item => item.EditorCategoryKey).ThenBy(item => item.EditorSortOrder))
        {
            var spec = _draft.Modifiers.FirstOrDefault(item => item.AssetId == modifier.id);
            var selected = spec != null;
            var available = WanfaPavilionService.Instance.ActivePolicy.IsModifierAvailable(modifier.id);
            if (!selected && (!modifier.EditorSelectable || !available)) continue;

            var checkedSpec = spec ?? modifier.CreateDefaultSpec();
            var compatibility = modifier.CheckEditorCompatibility(context, checkedSpec);
            if (!selected)
            {
                var candidate = _draft.DeepClone();
                candidate.Modifiers.Add(checkedSpec);
                compatibility.Merge(WanfaPavilionService.Instance.Validate(candidate));
            }
            var detail = $"{modifier.EditorCategoryKey.Localize()} · {GetRarityName(modifier.Rarity)}";
            var selectedError = selected
                ? draftValidation.Issues.FirstOrDefault(issue =>
                    issue.Severity == SkillValidationSeverity.Error &&
                    issue.SubjectId == modifier.id)
                : null;
            if (selectedError != null)
            {
                detail += $" · {selectedError.Message}";
            }
            else if (!compatibility.IsCompatible)
            {
                detail += $" · {compatibility.Issues.First(issue =>
                    issue.Severity == SkillValidationSeverity.Error).Message}";
            }
            var row = _rowPool.GetNext();
            row.Setup(modifier.id.Localize(), detail,
                selected
                    ? "Cultiway.Wanfa.UI.Action.Remove".Localize()
                    : "Cultiway.Wanfa.UI.Action.Add".Localize(),
                selected || compatibility.IsCompatible,
                () => ToggleModifier(modifier), selected ? WanfaUiIcons.Remove : WanfaUiIcons.Add,
                SkillModifierTooltipModel.FromSpec(checkedSpec));
            if (!selected || !modifier.EditorSelectable || !available) continue;

            BuildFieldControls(row, modifier, spec);
        }

        foreach (var spec in _draft.Modifiers.Where(IsMissingModifier))
        {
            var missingSpec = spec;
            _rowPool.GetNext().Setup(string.Format("Cultiway.Wanfa.UI.Format.MissingModifier".Localize(),
                    spec.AssetId),
                "Cultiway.Wanfa.UI.Detail.MissingModifier".Localize(),
                "Cultiway.Wanfa.UI.Action.Remove".Localize(), true,
                () => ApplyMutation(() => _draft.Modifiers.Remove(missingSpec)), WanfaUiIcons.Remove);
        }
    }

    private static bool IsMissingModifier(SkillModifierSpec spec)
    {
        if (string.IsNullOrWhiteSpace(spec.AssetId)) return true;
        var modifier = ModClass.I.SkillV3.ModifierLib.get(spec.AssetId);
        return modifier == null || modifier.EditorComponentType == null;
    }

    private void BuildFieldControls(WanfaEditorRow row, SkillModifierAsset modifier, SkillModifierSpec spec)
    {
        var needsRepair = NeedsParameterRepair(modifier, spec);
        var height = (modifier.EditorFields.Count + (needsRepair ? 1 : 0)) * 24f;
        WanfaUiFactory.SetLayout(row.Controls, 500f, height);
        row.SetHeight(32f + height);
        if (needsRepair)
        {
            var repair = WanfaUiFactory.CreateLayout(row.Controls, "Repair", true, 490f, 22f, 3f);
            WanfaUiFactory.CreateText(repair.transform, "Label",
                "Cultiway.Wanfa.UI.Detail.InvalidParameters".Localize(),
                330f, 22f, 7);
            var repairButton = WanfaUiFactory.CreateIconTextButton(repair.transform, "Action", WanfaUiIcons.Reset,
                "Cultiway.Wanfa.UI.Action.RepairParameters".Localize(), 96f, 20f,
                () => RepairParameters(spec.AssetId, modifier));
            WanfaUiFactory.SetTooltip(repairButton.gameObject, "Cultiway.Wanfa.UI.Action.RepairParameters",
                "Cultiway.Wanfa.UI.Tooltip.RepairParameters");
        }
        foreach (var field in modifier.EditorFields)
        {
            var storedValue = GetStoredFieldValue(spec, field);
            var controls = WanfaUiFactory.CreateLayout(row.Controls, field.ParameterKey, true, 490f, 22f, 3f);
            WanfaUiFactory.CreateText(controls.transform, "Label", field.DisplayName, 130f, 22f, 7);
            var fieldDescription = string.Format("Cultiway.Wanfa.UI.Tooltip.Field".Localize(), field.DisplayName,
                field.MinValue * field.DisplayScale, field.MaxValue * field.DisplayScale,
                field.Step * field.DisplayScale, field.Unit);
            if (field.Kind == SkillEditorFieldKind.Toggle)
            {
                if (!bool.TryParse(storedValue, out var toggleValue))
                {
                    toggleValue = bool.Parse(field.DefaultValue);
                }
                var toggle = WanfaUiFactory.CreateToggle(controls.transform, "Value", string.Empty,
                    toggleValue, 80f, 20f);
                WanfaUiFactory.SetTooltip(toggle, field.DisplayName, fieldDescription);
                toggle.onValueChanged.AddListener(value =>
                    SetField(spec.AssetId, field, value.ToString()));
                continue;
            }
            if (field.Kind is SkillEditorFieldKind.Text or SkillEditorFieldKind.StringSet)
            {
                var textInput = WanfaUiFactory.CreateInput(controls.transform, "Value",
                    field.ToDisplayValue(storedValue), string.Empty, 260f, 20f);
                WanfaUiFactory.SetTooltip(textInput, field.DisplayName, fieldDescription);
                textInput.onEndEdit.AddListener(value => SetField(spec.AssetId, field, value));
                continue;
            }
            var decrease = WanfaUiFactory.CreateButton(controls.transform, "Decrease", "-", 24f, 20f,
                () => StepField(spec.AssetId, field, -1));
            WanfaUiFactory.SetTooltip(decrease.gameObject, "Cultiway.Wanfa.UI.Action.Decrease",
                fieldDescription);
            var input = WanfaUiFactory.CreateInput(controls.transform, "Value",
                field.ToDisplayValue(storedValue), string.Empty, 100f, 20f);
            WanfaUiFactory.SetTooltip(input, field.DisplayName, fieldDescription);
            input.onEndEdit.AddListener(value => SetField(spec.AssetId, field, value));
            var increase = WanfaUiFactory.CreateButton(controls.transform, "Increase", "+", 24f, 20f,
                () => StepField(spec.AssetId, field, 1));
            WanfaUiFactory.SetTooltip(increase.gameObject, "Cultiway.Wanfa.UI.Action.Increase",
                fieldDescription);
            WanfaUiFactory.CreateText(controls.transform, "Unit", field.Unit, 44f, 22f, 6);
        }
    }

    private void BuildOverviewPage()
    {
        ReleasePreview();
        var compiled = new SkillBlueprintCompiler().Compile(_draft, SkillBlueprintCompileMode.Preview);
        if (compiled.Success) _previewContainer = compiled.Container;

        AddOverviewLine("Cultiway.Wanfa.UI.Overview.Name".Localize(),
            WanfaPavilionService.Instance.GetDisplayName(_draft));
        AddOverviewLine("Cultiway.Wanfa.UI.Overview.EntityTrajectory".Localize(),
            $"{GetAssetLabel(_draft.EntityAssetId, "Cultiway.Wanfa.UI.State.NoEntity".Localize())} / " +
            GetAssetLabel(_draft.TrajectoryAssetId, "Cultiway.Wanfa.UI.State.NoTrajectory".Localize()));
        if (compiled.Success)
        {
            var container = compiled.Container.GetComponent<SkillContainer>();
            AddOverviewLine("Cultiway.Wanfa.UI.Overview.VfxElement".Localize(),
                container.VfxElement.id.Localize());
            AddOverviewLine("Cultiway.Wanfa.UI.Overview.MotionProfile".Localize(),
                container.MotionProfile.id.Localize());
            AddOverviewLine("Cultiway.Wanfa.UI.Overview.CollisionRadius".Localize(),
                ResolveCollisionRadius(compiled.Container).ToString("0.##", CultureInfo.InvariantCulture));
            AddModifierRadiusOverviews(compiled.Container);
            AddOverviewLine("Cultiway.Wanfa.UI.Overview.CastResource".Localize(),
                FormatCastResources(container.CastResourceRequirement));
            AddOverviewLine("Cultiway.Wanfa.UI.Overview.StepDemand".Localize(),
                SkillCastCost.CalculateStepDemand(compiled.Container)
                .ToString("0.##", CultureInfo.InvariantCulture));
        }

        var validation = WanfaPavilionService.Instance.Validate(_draft);
        foreach (var issue in validation.Issues)
        {
            AddOverviewLine(issue.Severity == SkillValidationSeverity.Error
                ? "Cultiway.Wanfa.UI.Overview.Error".Localize()
                : "Cultiway.Wanfa.UI.Overview.Warning".Localize(), issue.Message);
        }
        if (validation.Issues.Count == 0)
        {
            AddOverviewLine("Cultiway.Wanfa.UI.Overview.Validation".Localize(),
                "Cultiway.Wanfa.UI.Overview.Valid".Localize());
        }
    }

    private static string FormatCastResources(SkillCastResourceRequirement requirement)
    {
        var separator = requirement.Mode == SkillCastResourceRequirementMode.AllOf ? " + " : " / ";
        return string.Join(separator, requirement.ResourceAssetIds.Select(id => id.Localize()));
    }

    private void AddOverviewLine(string title, string value)
    {
        _rowPool.GetNext().Setup(title, value, string.Empty, false, null);
    }

    private static string GetAssetLabel(string id, string fallback)
    {
        return string.IsNullOrWhiteSpace(id) ? fallback : id.Localize();
    }

    private static string GetRarityName(SkillModifierRarity rarity)
    {
        return $"Cultiway.SkillModifier.Rarity.{rarity}".Localize();
    }

    private void AddModifierRadiusOverviews(Entity compiled)
    {
        var scale = compiled.GetComponent<EffectRadiusScale>().Value;
        foreach (var spec in _draft.Modifiers)
        {
            var modifier = ModClass.I.SkillV3.ModifierLib.get(spec.AssetId);
            var field = modifier?.EditorFields.FirstOrDefault(item => item.ParameterKey == "Radius");
            if (field == null) continue;
            var radius = spec.GetFloat(field.ParameterKey) * scale;
            AddOverviewLine($"{modifier.id.Localize()} · {field.DisplayName}",
                radius.ToString("0.##", CultureInfo.InvariantCulture));
        }
    }

    private float ResolveCollisionRadius(Entity compiled)
    {
        var entity = ModClass.I.SkillV3.SkillLib.get(_draft.EntityAssetId);
        var radius = entity.PrefabEntity.GetComponent<ColliderSphere>().Radius;
        return SkillEffectRadius.ResolveContainer(compiled, radius);
    }

    private void SelectPage(int page)
    {
        _page = page;
        if (_page != 3) ReleasePreview();
        RefreshPage();
    }

    private void SelectEntity(string entityId)
    {
        ApplyMutation(() =>
        {
            _draft.EntityAssetId = entityId;
            _draft.AnimationIndex = 0;
            var entity = ModClass.I.SkillV3.SkillLib.get(entityId);
            var trajectoryId = WanfaPavilionService.Instance.ResolveAvailableTrajectoryId(entity,
                _draft.TrajectoryAssetId);
            _draft.TrajectoryAssetId = trajectoryId == null ? string.Empty : trajectoryId;
        });
    }

    private void ToggleModifier(SkillModifierAsset modifier)
    {
        ApplyMutation(() =>
        {
            var existing = _draft.Modifiers.FindIndex(item => item.AssetId == modifier.id);
            if (existing >= 0) _draft.Modifiers.RemoveAt(existing);
            else _draft.Modifiers.Add(modifier.CreateDefaultSpec());
        });
    }

    private void StepField(string modifierId, SkillEditorFieldAsset field, int direction)
    {
        var spec = _draft.Modifiers.First(item => item.AssetId == modifierId);
        var value = GetStoredFieldValue(spec, field);
        if (!field.TryValidate(value, out var error))
        {
            WorldTip.showNow(error, false, "top", 3f);
            return;
        }
        string next;
        if (field.Kind == SkillEditorFieldKind.Integer)
        {
            var number = int.Parse(value, CultureInfo.InvariantCulture);
            number = Mathf.Clamp(number + (int)field.Step * direction, (int)field.MinValue, (int)field.MaxValue);
            next = number.ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            var number = double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
            number = Math.Max(field.MinValue, Math.Min(field.MaxValue, number + field.Step * direction));
            next = ((float)number).ToString("R", CultureInfo.InvariantCulture);
        }
        SetStoredField(modifierId, field, next);
    }

    private void SetField(string modifierId, SkillEditorFieldAsset field, string value)
    {
        if (!field.TryConvertDisplayValue(value, out var storedValue, out var error))
        {
            WorldTip.showNow(error, false, "top", 3f);
            RefreshPage();
            return;
        }
        SetStoredField(modifierId, field, storedValue);
    }

    private void SetStoredField(string modifierId, SkillEditorFieldAsset field, string value)
    {
        ApplyMutation(() =>
        {
            var spec = _draft.Modifiers.First(item => item.AssetId == modifierId);
            if (spec.Parameters == null)
            {
                spec.Parameters = new Dictionary<string, string>(StringComparer.Ordinal);
            }
            spec.Parameters[field.ParameterKey] = value;
        });
    }

    private static string GetStoredFieldValue(SkillModifierSpec spec, SkillEditorFieldAsset field)
    {
        if (spec.Parameters != null &&
            spec.Parameters.TryGetValue(field.ParameterKey, out var value) && value != null)
        {
            return value;
        }
        return field.DefaultValue;
    }

    private static bool NeedsParameterRepair(SkillModifierAsset modifier, SkillModifierSpec spec)
    {
        if (spec.Parameters == null || spec.Parameters.Count != modifier.EditorFields.Count) return true;
        foreach (var field in modifier.EditorFields)
        {
            if (!spec.Parameters.TryGetValue(field.ParameterKey, out var value) ||
                value == null ||
                !field.TryNormalizeStoredValue(value, out var normalizedValue, out _) ||
                !string.Equals(value, normalizedValue, StringComparison.Ordinal)) return true;
        }
        return false;
    }

    private void RepairParameters(string modifierId, SkillModifierAsset modifier)
    {
        ApplyMutation(() =>
        {
            var spec = _draft.Modifiers.First(item => item.AssetId == modifierId);
            var repaired = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var field in modifier.EditorFields)
            {
                if (spec.Parameters != null &&
                    spec.Parameters.TryGetValue(field.ParameterKey, out var value) &&
                    value != null &&
                    field.TryNormalizeStoredValue(value, out var normalizedValue, out _))
                {
                    repaired[field.ParameterKey] = normalizedValue;
                }
                else
                {
                    repaired[field.ParameterKey] = field.DefaultValue;
                }
            }
            spec.Parameters = repaired;
        });
    }

    private void ApplyCustomName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            RefreshHeader();
            return;
        }
        if (_draft.NameMode == SkillBlueprintNameMode.Custom && _draft.CustomName == value) return;
        ApplyMutation(() =>
        {
            _draft.NameMode = SkillBlueprintNameMode.Custom;
            _draft.CustomName = value.Trim();
        });
    }

    private void ApplyCategory(string value)
    {
        var category = value.Trim();
        if (string.Equals(_draft.Category, category, StringComparison.Ordinal)) return;
        ApplyMutation(() => _draft.Category = category);
    }

    private void ToggleNameMode()
    {
        ApplyMutation(() =>
        {
            if (_draft.NameMode == SkillBlueprintNameMode.Custom)
            {
                _draft.NameMode = SkillBlueprintNameMode.Rule;
            }
            else
            {
                _draft.NameMode = SkillBlueprintNameMode.Custom;
                _draft.CustomName = WanfaPavilionService.Instance.GetDisplayName(_draft);
            }
        });
    }

    private void ApplyMutation(Action mutation)
    {
        var previousSignature = SkillBlueprintSignature.Build(_draft);
        _undo.Push(_draft.DeepClone());
        _redo.Clear();
        mutation();
        if (!string.Equals(previousSignature, SkillBlueprintSignature.Build(_draft), StringComparison.Ordinal))
        {
            _draft.RuleName = null;
            _draft.GeneratedName = null;
        }
        _draft.UpdatedAtUtcTicks = DateTime.UtcNow.Ticks;
        ReleasePreview();
        RefreshAll();
    }

    private void Undo()
    {
        if (_undo.Count == 0) return;
        _redo.Push(_draft.DeepClone());
        _draft = _undo.Pop();
        ReleasePreview();
        RefreshAll();
    }

    private void Redo()
    {
        if (_redo.Count == 0) return;
        _undo.Push(_draft.DeepClone());
        _draft = _redo.Pop();
        ReleasePreview();
        RefreshAll();
    }

    private void DiscardChanges()
    {
        _draft = _savedDraft.DeepClone();
        _undo.Clear();
        _redo.Clear();
        ReleasePreview();
        RefreshAll();
    }

    private bool TrySave()
    {
        if (_isExisting && !IsDirty())
        {
            if (_actorReplacementPending) return TryReplaceActorSkill(_draft);
            WorldTip.showNow("Cultiway.Wanfa.UI.Tip.NoChanges".Localize(), false, "top", 2f);
            return true;
        }
        var result = _isExisting
            ? WanfaPavilionService.Instance.Update(_draft)
            : WanfaPavilionService.Instance.SaveNew(_draft);
        return HandleSaveResult(result, _actorEdit);
    }

    private void SaveCopy()
    {
        var copy = _draft.CreateCopy();
        var result = WanfaPavilionService.Instance.SaveNew(copy);
        if (HandleSaveResult(result, false) && _actorEdit)
        {
            _actorReplacementPending = true;
        }
    }

    private bool HandleSaveResult(WanfaPavilionSaveResult result, bool replaceActorSkill)
    {
        if (result.Status == WanfaPavilionSaveStatus.Saved)
        {
            _draft = result.Blueprint.DeepClone();
            _savedDraft = _draft.DeepClone();
            _isExisting = true;
            _undo.Clear();
            _redo.Clear();
            RefreshAll();
            if (replaceActorSkill)
            {
                _actorReplacementPending = true;
                if (!TryReplaceActorSkill(_draft)) return false;
                WorldTip.showNow("Cultiway.Wanfa.UI.Tip.ActorSkillReplaced".Localize(), false, "top", 2f);
                return true;
            }

            WorldTip.showNow("Cultiway.Wanfa.UI.Tip.BlueprintSaved".Localize(), false, "top", 2f);
            return true;
        }
        if (result.Status == WanfaPavilionSaveStatus.Duplicate)
        {
            WorldTip.showNow("Cultiway.Wanfa.UI.Tip.DuplicateBlueprint".Localize(), false, "top", 3f);
            return false;
        }
        var message = "Cultiway.Wanfa.UI.Tip.SaveFailed".Localize();
        if (result.Validation != null)
        {
            var error = result.Validation.Issues.FirstOrDefault(issue =>
                issue.Severity == SkillValidationSeverity.Error);
            if (error != null) message = error.Message;
        }
        WorldTip.showNow(message, false, "top", 3f);
        return false;
    }

    private bool TryReplaceActorSkill(SkillBlueprint blueprint)
    {
        if (_sourceActor.isRekt() || !_sourceActor.isAlive())
        {
            WorldTip.showNow("Cultiway.Wanfa.UI.Tip.ActorReplaceFailed".Localize(), false, "top", 3f);
            return false;
        }

        var compiled = new SkillBlueprintCompiler().Compile(blueprint, SkillBlueprintCompileMode.Runtime);
        if (!compiled.Success)
        {
            WorldTip.showNow("Cultiway.Wanfa.UI.Tip.ActorReplaceFailed".Localize(), false, "top", 3f);
            return false;
        }

        var result = SkillOwnershipService.Replace(_sourceActor.GetExtend(), _sourceSkill, compiled.Container);
        if (result != SkillOwnershipResult.Replaced)
        {
            SkillBlueprintCompiler.Recycle(compiled.Container);
            WorldTip.showNow("Cultiway.Wanfa.UI.Tip.ActorReplaceFailed".Localize(), false, "top", 3f);
            return false;
        }

        _sourceSkill = compiled.Container;
        _actorReplacementPending = false;
        return true;
    }

    private void TestCast()
    {
        if (!WanfaPavilionService.Instance.Validate(_draft).IsCompatible)
        {
            WorldTip.showNow("Cultiway.Wanfa.UI.Tip.TestUnavailable".Localize(), false, "top", 3f);
            return;
        }
        _closingApproved = true;
        WanfaPavilionService.Instance.RequestTestCast(_draft);
    }

    internal static void ResumeAfterTestCast()
    {
        if (_instance == null || _instance._draft == null) return;
        _instance._resumeAfterTestCast = true;
        ScrollWindow.showWindow(Id, true, false);
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
        _confirmPanel = WanfaUiFactory.CreateLayout(BackgroundTransform, "UnsavedConfirmation", false, 270f, 94f, 6f, alignment: TextAnchor.MiddleCenter);
        _confirmPanel.transform.localPosition = Vector3.zero;
        var background = _confirmPanel.AddComponent<Image>();
        background.sprite = SpriteTextureLoader.getSprite("ui/special/windowEmptyFrame");
        background.type = Image.Type.Sliced;
        WanfaUiFactory.CreateText(_confirmPanel.transform, "Message",
            "Cultiway.Wanfa.UI.Tip.UnsavedChanges".Localize(), 260f, 30f, 9, TextAnchor.MiddleCenter,
            FontStyle.Bold);
        var actions = WanfaUiFactory.CreateLayout(_confirmPanel.transform, "Actions", true, 260f, 28f, 6f);
        var saveClose = WanfaUiFactory.CreateIconTextButton(actions.transform, "SaveClose", WanfaUiIcons.Save,
            "Cultiway.Wanfa.UI.Action.SaveAndClose".Localize(), 84f, 24f, () =>
        {
            if (!TrySave()) return;
            CompleteExit();
        });
        WanfaUiFactory.SetTooltip(saveClose.gameObject, "Cultiway.Wanfa.UI.Action.SaveAndClose",
            "Cultiway.Wanfa.UI.Tooltip.SaveAndClose");
        var discardClose = WanfaUiFactory.CreateIconTextButton(actions.transform, "DiscardClose",
            WanfaUiIcons.Reset, "Cultiway.Wanfa.UI.Action.Discard".Localize(), 76f, 24f, () =>
        {
            CompleteExit();
        });
        WanfaUiFactory.SetTooltip(discardClose.gameObject, "Cultiway.Wanfa.UI.Action.Discard",
            "Cultiway.Wanfa.UI.Tooltip.DiscardAndClose");
        var continueEditing = WanfaUiFactory.CreateIconTextButton(actions.transform, "Continue",
            WanfaUiIcons.Edit, "Cultiway.Wanfa.UI.Action.ContinueEditing".Localize(), 82f, 24f,
            () =>
            {
                _pendingExitMode = ExitMode.Close;
                SetConfirmation(false);
            });
        WanfaUiFactory.SetTooltip(continueEditing.gameObject, "Cultiway.Wanfa.UI.Action.ContinueEditing",
            "Cultiway.Wanfa.UI.Tooltip.ContinueEditing");
        _confirmPanel.SetActive(false);
    }

    private void SetConfirmation(bool visible)
    {
        _confirmPanel.SetActive(visible);
        _editorCanvasGroup.interactable = !visible;
    }

    private void PositionBackButton()
    {
        var back = transform.Find("BackButtonContainer");
        var position = back.localPosition;
        position.x = -180f;
        back.localPosition = position;
    }

    private IEnumerator BindBackButtonAfterScrollWindowStart()
    {
        yield return null;
        var back = transform.Find("BackButtonContainer");
        foreach (var button in back.GetComponentsInChildren<Button>(true))
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(RequestBack);
        }
    }

    private bool IsDirty()
    {
        if (!_isExisting) return true;
        return JsonConvert.SerializeObject(_draft) != JsonConvert.SerializeObject(_savedDraft);
    }

    private void ReleasePreview()
    {
        if (_previewContainer.IsNull) return;
        SkillBlueprintCompiler.Recycle(_previewContainer);
        _previewContainer = default;
    }
}
