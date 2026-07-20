using System;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.AIGC;
using Cultiway.Content.Components;
using Cultiway.Content.Semantics;
using Cultiway.Core.Components;
using Cultiway.Core.Semantics;
using Friflo.Engine.ECS;
using NeoModLoader.api.attributes;
using NeoModLoader.General;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI.Prefab;

public class SpecialItemTooltip : APrefabPreview<SpecialItemTooltip>
{
    private const string SemanticIconsRowName = "Semantic Icons";
    private const string TooltipIconsRowResource = "tooltips/TooltipIconsRow";
    private const int MaxSemanticIcons = 8;
    private const int SemanticIconColumns = MaxSemanticIcons;
    private const float SemanticIconSize = 14f;
    private const float SemanticIconsRowWidth = SemanticIconColumns * SemanticIconSize;

    private static Action<GameObject>                         _initiators;
    private static Action<SpecialItemTooltip, string, Entity> _setup_actions;
    private        TooltipIconsRow                            _semantic_icons;
    public         Tooltip                                    Tooltip { get; private set; }

    protected override void OnInit()
    {
        Tooltip = GetComponent<Tooltip>();
        var semanticIconsTransform = transform.Find(SemanticIconsRowName);
        if (semanticIconsTransform == null)
            throw new InvalidOperationException($"特殊物品 Tooltip 缺少 {SemanticIconsRowName}");
        _semantic_icons = semanticIconsTransform.GetComponent<TooltipIconsRow>();
    }

    [Hotfixable]
    public void Setup(string type, Entity entity)
    {
        Init();
        if (entity.TryGetComponent(out EntityName entity_name))
            Tooltip.name.text = entity_name.value;
        else
            Tooltip.name.text = entity.Id.ToString();

        if (entity.TryGetComponent(out ItemLevel level))
        {
            Tooltip.addDescription(level.GetName());
            Tooltip.addDescription("\n");
        }
        Tooltip.addDescription(LM.Get(entity.GetComponent<ItemShape>().shape_id));
        if (entity.TryGetComponent(out ElementRoot element_root))
        {
            Tooltip.addDescription("\n");
            Tooltip.addDescription(element_root.Type.GetName());
            for (var i = 0; i <= ElementIndex.Entropy; i++)
                Tooltip.addLineIntText(ElementIndex.ElementNames[i], (int)(100 * element_root[i]));
        }
        if (entity.Tags.Has<TagIngredient>())
        {
            var ingredient = IngredientNameGenerator.CreateContext(entity);
            Tooltip.addDescription("\n");
            if (!string.IsNullOrEmpty(ingredient.SourceName))
            {
                Tooltip.addLineText("来源", ingredient.SourceName, pLocalize: false);
            }
            var primary = IngredientNameGenerator.LocalizeElement(ingredient.PrimaryElementIndex);
            if (!string.IsNullOrEmpty(primary))
            {
                Tooltip.addLineText("主性", primary, pLocalize: false);
            }
        }
        SetupSemanticIcons(entity);

        _setup_actions?.Invoke(this, type, entity);
        if (entity.HasComponent<AliveTimer>() && entity.HasComponent<AliveTimeLimit>())
            Tooltip.addBottomDescription(
                $"离消失还剩:{(int)((entity.GetComponent<AliveTimeLimit>().value - entity.GetComponent<AliveTimer>().value) / TimeScales.SecPerYear)}年");
    }

    private void SetupSemanticIcons(Entity entity)
    {
        if (!entity.Tags.Has<TagIngredient>())
        {
            _semantic_icons.gameObject.SetActive(false);
            return;
        }

        var ranks = IngredientSemanticService.Build(entity).GetDirectRanked(SemanticQueryPolicy.Default);
        var iconCount = 0;
        for (var i = 0; i < ranks.Count && iconCount < MaxSemanticIcons; i++)
        {
            var icon = ranks[i].semantic.GetIcon();
            if (icon == null) continue;
            _semantic_icons.addIcon(icon);
            iconCount++;
        }

        _semantic_icons.init(Tooltip, Tooltip.data);
    }

    internal void HideSemanticIcons()
    {
        Init();
        _semantic_icons.gameObject.SetActive(false);
    }

    private static void _init()
    {
        GameObject obj = Instantiate(Resources.Load<GameObject>(WorldboxGame.Tooltips.Tip.prefab_id),
            ModClass.I.PrefabLibrary);

        CreateSemanticIconsRow(obj.transform);
        _initiators?.Invoke(obj);
        Prefab = obj.AddComponent<SpecialItemTooltip>();
    }

    private static void CreateSemanticIconsRow(Transform parent)
    {
        var template = Resources.Load<TooltipIconsRow>(TooltipIconsRowResource);
        if (template == null)
            throw new InvalidOperationException($"无法加载原版 Tooltip 图标栏: {TooltipIconsRowResource}");

        var row = Instantiate(template, parent, false);
        row.name = SemanticIconsRowName;
        row.transform.SetSiblingIndex(2);
        var layout = row.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = SemanticIconSize;
        layout.preferredHeight = SemanticIconSize;
        ((RectTransform)row.transform).sizeDelta = new Vector2(SemanticIconsRowWidth, SemanticIconSize);
        var grid = row.items_parent.GetComponent<GridLayoutGroup>();
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = SemanticIconColumns;
        row.gameObject.SetActive(false);
    }

    public static void RegisterInitiator(Action<GameObject> initiator)
    {
        _initiators += initiator;
    }

    public static void RegisterSetupAction(Action<SpecialItemTooltip, string, Entity> setup_action)
    {
        _setup_actions += setup_action;
    }
}
