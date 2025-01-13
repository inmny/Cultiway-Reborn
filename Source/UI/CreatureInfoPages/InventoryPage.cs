using Cultiway.Abstract;
using Cultiway.Core.Components;
using Cultiway.UI.Prefab;
using Cultiway.Utils.Extension;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI.CreatureInfoPages;

public class InventoryPage : MonoBehaviour
{
    public static void Setup(CreatureInfoPage page)
    {
        var this_page = page.gameObject.AddComponent<InventoryPage>();
        var grid = page.gameObject.AddComponent<GridLayoutGroup>();
        var fitter = page.gameObject.AddComponent<ContentSizeFitter>();
        page.GetComponent<RectTransform>().pivot = new(0.5f, 1);
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        grid.cellSize = new(18, 18);
        grid.spacing = new(2, 2);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        
        var size = page.GetComponent<RectTransform>().sizeDelta;
        grid.constraintCount = (int)(size.x / (grid.cellSize.x + grid.spacing.x));
        var padding = (int)((size.x - grid.constraintCount * (grid.cellSize.x + grid.spacing.x) + grid.spacing.x) / 2);
        grid.padding = new(padding, padding, 0, 0);
        
        this_page._special_item_pool = new MonoObjPool<SpecialItemDisplay>(SpecialItemDisplay.Prefab, page.transform);
    }
    private MonoObjPool<SpecialItemDisplay> _special_item_pool;

    public static void Show(CreatureInfoPage page, Actor actor)
    {
        var this_page = page.GetComponent<InventoryPage>();
        this_page._special_item_pool.Clear();
        var items = actor.GetExtend().GetItems();
        foreach (var item in items)
        {
            SpecialItemDisplay display = this_page._special_item_pool.GetNext();
            display.Setup(item.GetComponent<SpecialItem>());
        }
    }
}