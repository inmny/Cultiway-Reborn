using Cultiway.Abstract;
using Cultiway.Core.Components;
using Cultiway.UI.Prefab;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI;

public class AdditionCityWindow : MonoBehaviour
{
    private CityWindow                      _city_window;
    private bool                            _initialized;
    private ContentGrid                     _item_grid;
    private MonoObjPool<SpecialItemDisplay> _special_item_pool;

    private void Awake()
    {
        TryInit();
    }

    private void OnEnable()
    {
        TryInit();

        LoadItems();
    }
    [Hotfixable]
    private void LoadItems()
    {
        _special_item_pool.Clear();
        var items = _city_window.city.GetExtend().GetSpecialItems();
        foreach (SpecialItem item in items)
        {
            SpecialItemDisplay display = _special_item_pool.GetNext();
            display.Setup(item);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<ScrollWindow>().transform_content);
    }

    private void TryInit()
    {
        if (_initialized) return;
        _initialized = true;
        _city_window = GetComponent<CityWindow>();
        GetComponent<ScrollWindow>().scrollRect.enabled = true;

        _item_grid = ContentGrid.Instantiate(GetComponent<ScrollWindow>().transform_content, pName: "ContentItem");
        _item_grid.Setup(192, "Cultiway.UI.ContentItem", new Vector2(18, 18), new Vector2(2, 2));

        _city_window.list_content.Add(_item_grid.transform);
        _city_window._list_inventory_containers.Add(_item_grid.transform);

        _special_item_pool = new MonoObjPool<SpecialItemDisplay>(SpecialItemDisplay.Prefab, _item_grid.Grid.transform);
    }
}