using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Blueprints;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Editor;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Core.SkillLibV3.Wanfa;
using Cultiway.UI.Components;
using Cultiway.Utils;
using Friflo.Engine.ECS;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Cultiway.UI.Prefab;

public sealed class SkillTooltip : APrefabPreview<SkillTooltip>
{
    private sealed class ViewModel
    {
        public string Name;
        public string Summary;
        public string Description;
        public string BottomDescription;
        public SkillEntityAnimation Animation;
        public float BaseFrameInterval = 0.1f;
        public readonly List<(string Label, string Value, string Color)> Lines = new();
        public readonly List<SkillModifierTooltipModel> Modifiers = new();
    }

    private static readonly SkillBlueprintCompiler Compiler = new();
    private static readonly SkillBlueprintExporter Exporter = new();
    private static ViewModel _pending;

    private Tooltip _tooltip;
    private Image _avatar;
    private Text _summary;
    private GameObject _modifierGrid;
    private LayoutElement _modifierGridLayout;
    private ObjectPoolGenericMono<SkillModifierIcon> _modifierPool;
    private readonly SkillAnimationPreviewPlayer _animationPreview = new();

    protected override void OnInit()
    {
        _tooltip = GetComponent<Tooltip>();
        _avatar = transform.Find("SkillSummary/Avatar/Frame").GetComponent<Image>();
        _summary = transform.Find("SkillSummary/Summary").GetComponent<Text>();
        _modifierGrid = transform.Find("ModifierGrid").gameObject;
        _modifierGridLayout = _modifierGrid.GetComponent<LayoutElement>();
        var item = _modifierGrid.transform.Find("Item").GetComponent<SkillModifierIcon>();
        _modifierPool = new ObjectPoolGenericMono<SkillModifierIcon>(item, _modifierGrid.transform);
    }

    public static void Show(GameObject source, SkillBlueprint blueprint)
    {
        Show(source, Build(blueprint, default, null));
    }

    public static void Show(GameObject source, Entity container)
    {
        var exported = Exporter.Export(container, new SkillBlueprintExportOptions
        {
            PreserveContainerNameAsCustom = true
        });
        if (exported.Blueprint == null) return;

        var name = container.HasName ? container.Name.value : null;
        Show(source, Build(exported.Blueprint, container, name));
    }

    private static void Show(GameObject source, ViewModel model)
    {
        _pending = model;
        try
        {
            Tooltip.show(source, WorldboxGame.Tooltips.Skill.id, new TooltipData());
        }
        finally
        {
            _pending = null;
        }
    }

    internal void SetupPending()
    {
        Init();
        var model = _pending;
        if (model == null) return;

        _tooltip.name.text = model.Name;
        _summary.text = model.Summary;
        _tooltip.setDescription(model.Description);
        foreach (var line in model.Lines)
        {
            _tooltip.addLineText(line.Label, line.Value, line.Color, pLocalize: false, pLimitValue: 80);
        }
        _tooltip.setBottomDescription(model.BottomDescription);

        _animationPreview.Configure(model.Animation, model.BaseFrameInterval);
        _avatar.sprite = _animationPreview.CurrentSprite;
        _avatar.transform.parent.gameObject.SetActive(_animationPreview.CurrentSprite != null);

        _modifierPool.clear();
        foreach (var modifier in model.Modifiers)
        {
            _modifierPool.getNext().Setup(modifier);
        }
        _modifierPool.disableInactive();
        _modifierGrid.SetActive(model.Modifiers.Count > 0);
        if (model.Modifiers.Count > 0)
        {
            var rows = Mathf.CeilToInt(model.Modifiers.Count / 8f);
            _modifierGridLayout.minHeight = rows * 18f + 4f;
            _modifierGridLayout.preferredHeight = _modifierGridLayout.minHeight;
        }
    }

    private void Update()
    {
        if (_animationPreview.Advance(Time.unscaledDeltaTime))
        {
            _avatar.sprite = _animationPreview.CurrentSprite;
        }
    }

    private static ViewModel Build(SkillBlueprint blueprint, Entity runtimeContainer, string nameOverride)
    {
        var model = new ViewModel
        {
            Name = string.IsNullOrWhiteSpace(nameOverride)
                ? WanfaPavilionService.Instance.GetDisplayName(blueprint)
                : nameOverride
        };

        var entity = string.IsNullOrWhiteSpace(blueprint.EntityAssetId)
            ? null
            : ModClass.I.SkillV3.SkillLib.get(blueprint.EntityAssetId);
        SkillEntityAnimation animation = null;
        if (entity != null && entity.IsAnimationIndexValid(blueprint.AnimationIndex))
        {
            animation = entity.GetAnimation(blueprint.AnimationIndex);
            model.Animation = animation;
        }

        var trajectory = string.IsNullOrWhiteSpace(blueprint.TrajectoryAssetId)
            ? null
            : ModClass.I.SkillV3.TrajLib.get(blueprint.TrajectoryAssetId);
        model.Description = trajectory == null || string.IsNullOrEmpty(trajectory.EditorDescriptionKey)
            ? "Cultiway.Wanfa.UI.Tooltip.Skill.Description".Localize()
            : trajectory.EditorDescriptionKey.Localize();

        var entityName = GetAssetName(blueprint.EntityAssetId, "Cultiway.Wanfa.UI.State.NoEntity");
        var trajectoryName = GetAssetName(blueprint.TrajectoryAssetId, "Cultiway.Wanfa.UI.State.NoTrajectory");
        model.Summary = $"{entityName}\n{trajectoryName}";
        var entitySemantics = entity?.Semantics.Resolve(ModClass.L.SemanticLibrary).ToArray();
        if (entitySemantics is { Length: > 0 })
        {
            AddLine(model, "Cultiway.Wanfa.UI.Overview.Tags".Localize(),
                string.Join("、", entitySemantics.Select(semantic => semantic.GetName())));
        }

        var compiled = runtimeContainer;
        var recycleCompiled = false;
        if (compiled.IsNull)
        {
            var result = Compiler.Compile(blueprint, SkillBlueprintCompileMode.Preview);
            if (result.Success)
            {
                compiled = result.Container;
                recycleCompiled = true;
            }
        }

        if (!compiled.IsNull)
        {
            var container = compiled.GetComponent<SkillContainer>();
            if (animation != null)
            {
                model.BaseFrameInterval = container.MotionProfile.FrameInterval;
            }
            AddLine(model, "Cultiway.Wanfa.UI.Overview.ItemLevel".Localize(),
                SkillCastResourceFormatter.FormatItemLevel(container.CastResourceRequirement,
                    compiled.GetComponent<ItemLevel>()));
            AddLine(model, "Cultiway.Wanfa.UI.Overview.VfxElement".Localize(), container.VfxElement.id.Localize());
            AddLine(model, "Cultiway.Wanfa.UI.Overview.MotionProfile".Localize(),
                container.MotionProfile.id.Localize());
            AddLine(model, "Cultiway.Wanfa.UI.Overview.CollisionRadius".Localize(),
                ResolveCollisionRadius(compiled, entity).ToString("0.##", CultureInfo.InvariantCulture));
            AddLine(model, "Cultiway.Wanfa.UI.Overview.CastResource".Localize(),
                SkillCastResourceFormatter.Format(container.CastResourceRequirement));
            AddLine(model, "Cultiway.Wanfa.UI.Overview.StepDemand".Localize(),
                SkillCastCost.CalculateStepDemand(compiled).ToString("0.##", CultureInfo.InvariantCulture));
        }

        if (recycleCompiled) SkillBlueprintCompiler.Recycle(compiled);

        if (blueprint.Modifiers == null)
        {
            AddLine(model, "Cultiway.Wanfa.UI.Overview.Modifiers".Localize(),
                "Cultiway.Wanfa.UI.State.Damaged".Localize(), "#FB2C21");
        }
        else
        {
            foreach (var spec in blueprint.Modifiers)
            {
                model.Modifiers.Add(SkillModifierTooltipModel.FromSpec(spec));
            }
        }

        var validation = WanfaPavilionService.Instance.Validate(blueprint);
        if (validation.Issues.Count > 0)
        {
            foreach (var issue in validation.Issues)
            {
                if (issue.Severity != SkillValidationSeverity.Error) continue;
                AddLine(model, "Cultiway.Wanfa.UI.Overview.Error".Localize(), issue.Message, "#FB2C21");
            }
        }

        model.BottomDescription = BuildBottomDescription(blueprint, runtimeContainer.IsNull);
        return model;
    }

    private static float ResolveCollisionRadius(Entity compiled, SkillEntityAsset entity)
    {
        if (entity == null || !entity.PrefabEntity.TryGetComponent(out ColliderSphere collider)) return 0f;
        return SkillEffectRadius.ResolveContainer(compiled, collider.Radius);
    }

    private static string BuildBottomDescription(SkillBlueprint blueprint, bool isBlueprint)
    {
        var parts = new List<string>();
        if (isBlueprint)
        {
            parts.Add(string.Format("Cultiway.Wanfa.UI.Format.Revision".Localize(), blueprint.Revision));
        }
        else
        {
            parts.Add("Cultiway.Wanfa.UI.Tooltip.Skill.ActorOwned".Localize());
        }
        if (!string.IsNullOrWhiteSpace(blueprint.Category)) parts.Add(blueprint.Category);
        var modifierCount = blueprint.Modifiers == null ? 0 : blueprint.Modifiers.Count;
        parts.Add(string.Format("Cultiway.Wanfa.UI.Format.ModifierCount".Localize(), modifierCount));
        return string.Join("  ·  ", parts);
    }

    private static string GetAssetName(string id, string fallbackKey)
    {
        return string.IsNullOrWhiteSpace(id) ? fallbackKey.Localize() : id.Localize();
    }

    private static void AddLine(ViewModel model, string label, string value, string color = null)
    {
        model.Lines.Add((label, value, color));
    }

    private static void _init()
    {
        var obj = Instantiate(Resources.Load<GameObject>(WorldboxGame.Tooltips.Tip.prefab_id),
            ModClass.I.PrefabLibrary);
        obj.name = "tooltip_cultiway_wanfa_skill";
        obj.GetComponent<Tooltip>().background.raycastTarget = true;

        CreateSkillSummary(obj.transform);
        CreateModifierGrid(obj.transform);

        Prefab = obj.AddComponent<SkillTooltip>();
    }

    private static void CreateSkillSummary(Transform parent)
    {
        var summary = new GameObject("SkillSummary", typeof(RectTransform), typeof(LayoutElement));
        summary.transform.SetParent(parent, false);
        summary.transform.SetSiblingIndex(1);
        var summaryLayout = summary.GetComponent<LayoutElement>();
        summaryLayout.minHeight = 32f;
        summaryLayout.preferredHeight = 32f;

        var avatar = new GameObject("Avatar", typeof(RectTransform), typeof(Image));
        avatar.transform.SetParent(summary.transform, false);
        var avatarRect = avatar.GetComponent<RectTransform>();
        avatarRect.anchorMin = avatarRect.anchorMax = new Vector2(0f, 0.5f);
        avatarRect.anchoredPosition = new Vector2(16f, 0f);
        avatarRect.sizeDelta = new Vector2(28f, 28f);
        var avatarBackground = avatar.GetComponent<Image>();
        avatarBackground.sprite = UiResources.GetSprite(UiResources.WindowInner);
        avatarBackground.type = Image.Type.Sliced;
        avatarBackground.color = new Color(1f, 1f, 1f, 0.5f);

        var frame = new GameObject("Frame", typeof(RectTransform), typeof(Image));
        frame.transform.SetParent(avatar.transform, false);
        var frameRect = frame.GetComponent<RectTransform>();
        frameRect.anchorMin = frameRect.anchorMax = new Vector2(0.5f, 0.5f);
        frameRect.sizeDelta = new Vector2(24f, 24f);
        frame.GetComponent<Image>().preserveAspect = true;

        var textObject = new GameObject("Summary", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(summary.transform, false);
        var textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = new Vector2(34f, 1f);
        textRect.offsetMax = new Vector2(-3f, -1f);
        var text = textObject.GetComponent<Text>();
        text.font = Cultiway.UI.UiTheme.Current.Font;
        text.fontSize = 7;
        text.alignment = TextAnchor.MiddleLeft;
        text.color = Color.white;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 5;
        text.resizeTextMaxSize = 7;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
    }

    private static void CreateModifierGrid(Transform parent)
    {
        var grid = new GameObject("ModifierGrid", typeof(RectTransform), typeof(Image), typeof(GridLayoutGroup),
            typeof(LayoutElement));
        grid.transform.SetParent(parent, false);
        grid.transform.SetSiblingIndex(3);
        var background = grid.GetComponent<Image>();
        background.sprite = UiResources.GetSprite(UiResources.WindowInner);
        background.type = Image.Type.Sliced;
        background.color = new Color(1f, 1f, 1f, 0.35f);
        var layout = grid.GetComponent<GridLayoutGroup>();
        layout.padding = new RectOffset(3, 3, 2, 2);
        layout.cellSize = new Vector2(16f, 16f);
        layout.spacing = new Vector2(2f, 2f);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 8;

        SkillModifierIcon.Create(grid.transform, "Item", 16f).gameObject.SetActive(false);
    }
}
