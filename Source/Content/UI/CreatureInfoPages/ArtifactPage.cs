using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
using Cultiway.Content.Utils;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.UI.Prefab;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using NeoModLoader.General;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.Content.UI.CreatureInfoPages;

/// <summary>
/// 单位信息窗口的"法宝"页：列出已装备法器，并用图标状态区分待命、运转与超载。
/// </summary>
public class ArtifactPage : MonoBehaviour
{
    private MonoObjPool<SpecialItemDisplay> _special_item_pool;
    private static Vector2 _size;

    public static void Setup(CreatureInfoPage page)
    {
        var this_page = page.gameObject.AddComponent<ArtifactPage>();
        var grid = page.gameObject.AddComponent<GridLayoutGroup>();
        var fitter = page.gameObject.AddComponent<ContentSizeFitter>();
        page.GetComponent<RectTransform>().pivot = new(0.5f, 1);
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        grid.cellSize = new(18, 18);
        grid.spacing = new(2, 2);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;

        this_page._special_item_pool = new MonoObjPool<SpecialItemDisplay>(SpecialItemDisplay.Prefab, page.transform);
    }

    public static void Show(CreatureInfoPage page, Actor actor)
    {
        var size = page.GetComponent<RectTransform>().sizeDelta;
        if (size != _size)
        {
            _size = size;
            var grid = page.GetComponent<GridLayoutGroup>();
            grid.constraintCount = (int)(size.x / (grid.cellSize.x + grid.spacing.x));
            var padding = (int)((size.x - grid.constraintCount * (grid.cellSize.x + grid.spacing.x) + grid.spacing.x) / 2);
            grid.padding = new(padding, padding, 0, 0);
        }

        var this_page = page.GetComponent<ArtifactPage>();
        this_page._special_item_pool.Clear();
        var actorExtend = actor.GetExtend();
        List<Entity> artifacts = new(actorExtend.GetEquippedArtifacts());
        artifacts.Sort((left, right) =>
        {
            EquippedArtifactRelation leftRelation = actorExtend.E
                .GetRelation<EquippedArtifactRelation, Entity>(left);
            EquippedArtifactRelation rightRelation = actorExtend.E
                .GetRelation<EquippedArtifactRelation, Entity>(right);
            int stateOrder = rightRelation.state.CompareTo(leftRelation.state);
            return stateOrder != 0 ? stateOrder : left.Id.CompareTo(right.Id);
        });
        foreach (var item in artifacts)
        {
            SpecialItemDisplay display = this_page._special_item_pool.GetNext();
            EquippedArtifactRelation relation = actorExtend.E
                .GetRelation<EquippedArtifactRelation, Entity>(item);
            display.Setup(item.GetComponent<SpecialItem>(), () =>
            {
                actorExtend.UnequipArtifact(item, suppressAutoEquip: true);
                Show(page, actor);
            }, relation.state.GetStateColor());
        }
    }

    public static string GetTitle(ActorExtend actor)
    {
        ArtifactLoadoutState state = actor.GetArtifactLoadoutState();
        float used = state.prepared_load + state.operating_load;
        return $"{LM.Get(nameof(ArtifactPage))} {used:0.#}/{actor.Base.stats[WorldboxGame.BaseStats.DivineSense.id]:0.#}";
    }
}
