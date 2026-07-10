using Cultiway.Abstract;
using Cultiway.Content.Extensions;
using Cultiway.Core.Components;
using Cultiway.UI.Prefab;
using Cultiway.Utils.Extension;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.Content.UI.CreatureInfoPages;

/// <summary>
/// 单位信息窗口的"法宝"页：以图标网格列出该单位已经装备的法器，
/// hover 显示由 <see cref="SpecialItemTooltip"/> 渲染的 tooltip（名字/器形/品阶）。
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
        var artifacts = actorExtend.GetEquippedArtifacts();
        foreach (var item in artifacts)
        {
            SpecialItemDisplay display = this_page._special_item_pool.GetNext();
            display.Setup(item.GetComponent<SpecialItem>(), () =>
            {
                actorExtend.UnequipArtifact(item);
                Show(page, actor);
            });
        }
    }
}
