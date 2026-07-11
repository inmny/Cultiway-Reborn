using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Content.Components.Skill;
using Cultiway.Content.WanfaPavilion;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Blueprints;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Editor;
using Cultiway.Utils;
using Friflo.Engine.ECS;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Cultiway.Content.UI.Prefab;

public sealed class WanfaSkillTooltip : APrefabPreview<WanfaSkillTooltip>
{
    private sealed class ViewModel
    {
        public string Name;
        public string Summary;
        public string Description;
        public string BottomDescription;
        public Sprite[] Frames = Array.Empty<Sprite>();
        public readonly List<(string Label, string Value, string Color)> Lines = new();
        public readonly List<WanfaModifierTooltipModel> Modifiers = new();
    }

    private static readonly SkillBlueprintCompiler Compiler = new();
    private static readonly SkillBlueprintExporter Exporter = new();
    private static ViewModel _pending;

    private Tooltip _tooltip;
    private Image _avatar;
    private Text _summary;
    private GameObject _modifierGrid;
    private LayoutElement _modifierGridLayout;
    private ObjectPoolGenericMono<WanfaModifierIcon> _modifierPool;
    private Sprite[] _frames = Array.Empty<Sprite>();
    private int _frameIndex;
    private float _frameTimer;

    protected override void OnInit()
    {
        _tooltip = GetComponent<Tooltip>();
        _avatar = transform.Find("SkillSummary/Avatar/Frame").GetComponent<Image>();
        _summary = transform.Find("SkillSummary/Summary").GetComponent<Text>();
        _modifierGrid = transform.Find("ModifierGrid").gameObject;
        _modifierGridLayout = _modifierGrid.GetComponent<LayoutElement>();
        var item = _modifierGrid.transform.Find("Item").GetComponent<WanfaModifierIcon>();
        _modifierPool = new ObjectPoolGenericMono<WanfaModifierIcon>(item, _modifierGrid.transform);
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
            Tooltip.show(source, Tooltips.WanfaSkill.id, new TooltipData());
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

        _frames = model.Frames;
        _frameIndex = 0;
        _frameTimer = 0f;
        _avatar.sprite = _frames.Length == 0 ? null : _frames[0];
        _avatar.transform.parent.gameObject.SetActive(_frames.Length > 0);

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
        if (_frames.Length < 2) return;
        _frameTimer += Time.unscaledDeltaTime;
        if (_frameTimer < 0.1f) return;

        _frameTimer = 0f;
        _frameIndex = (_frameIndex + 1) % _frames.Length;
        _avatar.sprite = _frames[_frameIndex];
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
        if (entity != null)
        {
            model.Frames = entity.PrefabEntity.GetComponent<AnimData>().frames;
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
        if (entity != null && entity.SeriesTags.Count > 0)
        {
            AddLine(model, "Cultiway.Wanfa.UI.Overview.Tags".Localize(),
                string.Join("、", entity.SeriesTags.Select(SkillTags.GetDisplayName)));
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
            AddLine(model, "Cultiway.Wanfa.UI.Overview.VfxElement".Localize(), container.VfxElement.id.Localize());
            AddLine(model, "Cultiway.Wanfa.UI.Overview.MotionProfile".Localize(),
                container.MotionProfile.id.Localize());
            AddLine(model, "Cultiway.Wanfa.UI.Overview.CollisionRadius".Localize(),
                ResolveCollisionRadius(blueprint, entity).ToString("0.##", CultureInfo.InvariantCulture));
            AddLine(model, "Cultiway.Wanfa.UI.Overview.StepWakan".Localize(),
                SkillCastCost.CalculateStepWakanCost(compiled).ToString("0.##", CultureInfo.InvariantCulture));
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
                model.Modifiers.Add(WanfaModifierTooltipModel.FromSpec(spec));
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

    private static float ResolveCollisionRadius(SkillBlueprint blueprint, SkillEntityAsset entity)
    {
        if (entity == null || !entity.PrefabEntity.TryGetComponent(out ColliderSphere collider)) return 0f;

        var scale = 1f;
        if (blueprint.Modifiers == null) return collider.Radius;
        var huge = blueprint.Modifiers.FirstOrDefault(item => item != null && item.AssetId == SkillModifiers.Huge.id);
        if (huge != null && huge.Parameters != null &&
            huge.Parameters.TryGetValue(nameof(HugeModifier.Value), out var value) &&
            float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var multiplier))
        {
            scale = multiplier;
        }
        return collider.Radius * scale;
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

        Prefab = obj.AddComponent<WanfaSkillTooltip>();
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
        avatarBackground.sprite = SpriteTextureLoader.getSprite("ui/special/windowInnerSliced");
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
        text.font = UIUtils.GetCurrentFont();
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
        background.sprite = SpriteTextureLoader.getSprite("ui/special/windowInnerSliced");
        background.type = Image.Type.Sliced;
        background.color = new Color(1f, 1f, 1f, 0.35f);
        var layout = grid.GetComponent<GridLayoutGroup>();
        layout.padding = new RectOffset(3, 3, 2, 2);
        layout.cellSize = new Vector2(16f, 16f);
        layout.spacing = new Vector2(2f, 2f);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 8;

        WanfaModifierIcon.Create(grid.transform, "Item", 16f).gameObject.SetActive(false);
    }
}
