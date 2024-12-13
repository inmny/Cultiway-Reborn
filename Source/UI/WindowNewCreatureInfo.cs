using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.UI.CreatureInfoPages;
using Cultiway.UI.Prefab;
using Cultiway.Utils.Extension;
using NeoModLoader.api;
using NeoModLoader.api.attributes;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI;

public class WindowNewCreatureInfo : AbstractWideWindow<WindowNewCreatureInfo>
{
    private static readonly List<PageRegistration> _page_registrations = new();

    private readonly List<Tuple<string, StatValue>> _stat_values = new();
    private          Actor                  _actor;
    private readonly List<CreatureInfoPage> _available_pages = new();
    private          string                 _current_page;

    private Transform               _page_container;
    private Transform               _page_entry_container;
    private MonoObjPool<TextButton> _page_entry_pool;

    private readonly Dictionary<string, CreatureInfoPage> _pages = new();

    public static void Show()
    {
        if (Instance == null) CreateAndInit("Cultiway.UI.WindowNewCreatureInfo");

        ScrollWindow.showWindow(WindowId);
    }

    protected override void Init()
    {
        VertFlexGrid stat_grid = VertFlexGrid.Instantiate(BackgroundTransform, pName: "Stat Grid");
        stat_grid.Setup(200, new Vector2(18, 25), new Vector2(4, 2));
        stat_grid.Background.enabled = false;
        stat_grid.transform.localPosition = new Vector3(-200, 0);

        var _pool = new MonoObjPool<StatValue>(StatValue.Prefab, stat_grid.transform);

        register_stats_asset(WorldboxGame.BaseStats.IronArmor);
        register_stats_asset(WorldboxGame.BaseStats.WoodArmor);
        register_stats_asset(WorldboxGame.BaseStats.WaterArmor);
        register_stats_asset(WorldboxGame.BaseStats.FireArmor);
        register_stats_asset(WorldboxGame.BaseStats.EarthArmor);
        register_stats_asset(WorldboxGame.BaseStats.NegArmor);
        register_stats_asset(WorldboxGame.BaseStats.PosArmor);
        register_stats_asset(WorldboxGame.BaseStats.EntropyArmor);
        register_stats_asset(WorldboxGame.BaseStats.IronMaster);
        register_stats_asset(WorldboxGame.BaseStats.WoodMaster);
        register_stats_asset(WorldboxGame.BaseStats.WaterMaster);
        register_stats_asset(WorldboxGame.BaseStats.FireMaster);
        register_stats_asset(WorldboxGame.BaseStats.EarthMaster);
        register_stats_asset(WorldboxGame.BaseStats.NegMaster);
        register_stats_asset(WorldboxGame.BaseStats.PosMaster);
        register_stats_asset(WorldboxGame.BaseStats.EntropyMaster);

        void register_stats_asset(BaseStatAsset asset)
        {
            StatValue stat = _pool.GetNext();
            stat.Setup(0,
                SpriteTextureLoader.getSprite($"cultiway/icons/stats/{asset.id}") ??
                SpriteTextureLoader.getSprite("ui/icons/iconDamage"), asset);
            _stat_values.Add(new Tuple<string, StatValue>(asset.id, stat));
        }

        void register_stats(string id, Sprite sprite)
        {
            StatValue stat = _pool.GetNext();
            stat.Setup(0, sprite, AssetManager.base_stats_library.get(id));
            _stat_values.Add(new Tuple<string, StatValue>(id, stat));
        }


        Transform scroll_view_transform = BackgroundTransform.Find("Scroll View");
        scroll_view_transform.localPosition = new Vector3(158, 111);
        scroll_view_transform.GetComponent<RectTransform>().sizeDelta = new Vector2(258, 30);
        _page_entry_container = scroll_view_transform.Find("Viewport/Content");
        _page_entry_container.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 30);
        var fitter = _page_entry_container.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        var layout = _page_entry_container.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlHeight = false;
        layout.childControlWidth = false;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = false;
        layout.spacing = 8;
        layout.padding = new RectOffset(4, 4, 0, 0);
        _page_entry_pool = new MonoObjPool<TextButton>(TextButton.Prefab, _page_entry_container);

        _page_container = new GameObject("Pages", typeof(Image), typeof(HorizontalLayoutGroup)).transform;
        _page_container.SetParent(BackgroundTransform);
        _page_container.localPosition = new Vector3(158, -14);
        _page_container.localScale = Vector3.one;
        var page_bg = _page_container.GetComponent<Image>();
        page_bg.sprite = SpriteTextureLoader.getSprite("ui/special/windowInnerSliced");
        page_bg.type = Image.Type.Sliced;
        _page_container.GetComponent<RectTransform>().sizeDelta = new Vector2(258, 220);

        RegisterPage(nameof(ElementRootPage), a => a.GetExtend().HasElementRoot(), p => { }, ElementRootPage.Show);

        create_pages();
    }

    public static void RegisterPage(string id, Func<Actor, bool> condition, Action<CreatureInfoPage> setup_action,
                                    Action<CreatureInfoPage, Actor> show_action)
    {
        _page_registrations.Add(new PageRegistration
        {
            id = id,
            condition = condition,
            setup_action = setup_action,
            show_action = show_action
        });
    }

    private void create_pages()
    {
        foreach (PageRegistration registration in _page_registrations)
        {
            CreatureInfoPage page = CreatureInfoPage.Instantiate(_page_container, pName: registration.id);
            registration.setup_action?.Invoke(page);
            _pages.Add(registration.id, page);
        }
    }

    [Hotfixable]
    public override void OnNormalEnable()
    {
        _actor = Config.selectedUnit;
        _page_entry_pool.Clear();
        _available_pages.Clear();

        if (_actor == null) return;
        foreach ((var id, StatValue stat) in _stat_values)
        {
            var value = _actor.stats[id];
            stat.Setup(value);
        }

        _current_page = null;

        foreach (PageRegistration registration in _page_registrations)
        {
            if (!registration.condition(_actor)) continue;

            if (!_pages.TryGetValue(registration.id, out CreatureInfoPage page)) continue;
            var id = registration.id;
            _available_pages.Add(page);
            TextButton entry = _page_entry_pool.GetNext();
            entry.Setup(registration.id, () =>
            {
                _current_page = id;
                UpdatePage();
            });
            entry.name = registration.id;
        }

        UpdatePage();
    }

    public void UpdatePage()
    {
        foreach (CreatureInfoPage page in _pages.Values) page.gameObject.SetActive(false);

        foreach (TextButton entry in _page_entry_pool.ActiveObjs) entry.Button.interactable = true;

        if (string.IsNullOrEmpty(_current_page) ||
            !_pages.TryGetValue(_current_page, out CreatureInfoPage current_page)) return;
        current_page.gameObject.SetActive(true);
        _page_registrations.Find(x => x.id == _current_page).show_action?.Invoke(current_page, _actor);
        _page_entry_pool.ActiveObjs.First(x => x.name == _current_page).Button.interactable = false;
    }

    private struct PageRegistration
    {
        public string                          id;
        public Func<Actor, bool>               condition;
        public Action<CreatureInfoPage>        setup_action;
        public Action<CreatureInfoPage, Actor> show_action;
    }
}