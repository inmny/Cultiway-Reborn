using System;
using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.UI.Prefab;
using NeoModLoader.api;
using UnityEngine;

namespace Cultiway.UI;

public class WindowNewCreatureInfo : AbstractWideWindow<WindowNewCreatureInfo>
{
    private Actor _actor;

    private readonly List<Tuple<string, StatValue>> _stat_values = new();

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
    }

    public override void OnNormalEnable()
    {
        _actor = Config.selectedUnit;
        if (_actor == null) return;
        foreach ((var id, StatValue stat) in _stat_values)
        {
            var value = _actor.stats[id];
            stat.Setup(value);
        }
    }
}