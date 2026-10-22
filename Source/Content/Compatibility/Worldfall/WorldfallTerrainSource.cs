using System;
using System.Reflection;
using Cultiway.Content.Skills;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Compatibility.Worldfall;

/// <summary>在主线程读取外部实际地形，发布不依赖镜头和可变游戏对象的数值快照。</summary>
internal sealed class WorldfallTerrainSource
{
    /// <summary>外部根、采集器和高度缓存的已核对字段。</summary>
    private readonly PropertyInfo root;
    /// <summary>外部根拥有的采集器。</summary>
    private readonly FieldInfo samplerField, cacheField, heightsVersion, flagsField, outsideField;
    /// <summary>初始化、区域刷新及实际高度入口。</summary>
    private readonly MethodInfo ensure, refresh, sample;
    /// <summary>当前外部采集器及已绑定委托，不在每个格点反射。</summary>
    private object sampler;
    /// <summary>只在主线程调用的环境入口。</summary>
    private Func<bool> ensureWorld;
    /// <summary>区域刷新不执行外部光照重建。</summary>
    private Func<int, int, int, int, bool, int> refreshTiles;
    /// <summary>外部实际高度函数。</summary>
    private Func<float, float, float> heightAt;
    /// <summary>当前完整世界标识及已经采集的版本。</summary>
    private object map;
    /// <summary>外部高度版本。</summary>
    private int version = -1;
    /// <summary>同一版本的只读快照。</summary>
    private AbilityHeightField snapshot;
    /// <summary>保留影响采样的原格标记，用于识别没有改变格高的岸线变化。</summary>
    private byte[] flags;

    /// <summary>仅在可选模组边界解析已经使用的实际入口。</summary>
    /// <param name="assembly">已加载的外部程序集。</param>
    internal WorldfallTerrainSource(Assembly assembly)
    {
        const BindingFlags all = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        Type main = assembly.GetType("FirstPerson.WorldBoxMod", true), reader = assembly.GetType("FirstPerson.WorldSampler", true);
        Type terrain = assembly.GetType("FirstPerson.Core.TerrainCache", true);
        root = main.GetProperty("Instance", all); samplerField = main.GetField("_sampler", all);
        cacheField = reader.GetField("Cache", all); heightsVersion = reader.GetField("_heightsVersion", all);
        flagsField = terrain.GetField("Flags", all); outsideField = terrain.GetField("OutsideHeight", all);
        ensure = reader.GetMethod("EnsureWorld", all); refresh = reader.GetMethod("RefreshTiles", all);
        sample = terrain.GetMethod("SampleHeight", all, null, new[] { typeof(float), typeof(float) }, null);
        if (root == null || samplerField == null || cacheField == null || heightsVersion == null || flagsField == null ||
            outsideField == null || ensure == null || refresh == null || sample == null)
            throw new MissingMemberException("Worldfall 地表快照入口与已核对版本不符");
    }

    /// <summary>显示接入成功后启用采集，并在切换世界时释放旧地图数组。</summary>
    internal void Install()
    {
        ConstructedAbilityService.GroundProvider = Capture;
        Cultiway.Patch.PatchMapBox.RegisterActionOnClearWorld(Clear);
    }

    /// <summary>外部采集器仍可复用，但旧世界的数组和地图不得继续保留。</summary>
    private void Clear()
    {
        map = null; snapshot = null; flags = null; version = -1;
    }

    /// <summary>先刷新活动区域，再只在高度或岸线变化后建立新数组。</summary>
    /// <param name="area">实际来源、目标和活动形体所在区域。</param>
    private AbilityHeightField Capture(Rect area)
    {
        object main = root.GetValue(null); if (main == null || World.world?.tiles_map == null) return null;
        object current = samplerField.GetValue(main);
        if (!ReferenceEquals(current, sampler))
        {
            sampler = current; snapshot = null;
            ensureWorld = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), current, ensure);
            refreshTiles = (Func<int, int, int, int, bool, int>)Delegate.CreateDelegate(typeof(Func<int, int, int, int, bool, int>), current, refresh);
            heightAt = (Func<float, float, float>)Delegate.CreateDelegate(typeof(Func<float, float, float>), cacheField.GetValue(current), sample);
        }
        if (!ensureWorld()) return null;
        int width = MapBox.width, height = MapBox.height;
        int x0 = Mathf.Clamp(Mathf.FloorToInt(area.xMin), 0, width), x1 = Mathf.Clamp(Mathf.CeilToInt(area.xMax), 0, width);
        int y0 = Mathf.Clamp(Mathf.FloorToInt(area.yMin), 0, height), y1 = Mathf.Clamp(Mathf.CeilToInt(area.yMax), 0, height);
        refreshTiles(x0, y0, x1, y1, true);
        object cache = cacheField.GetValue(current); byte[] actual = (byte[])flagsField.GetValue(cache);
        bool changed = snapshot == null || !ReferenceEquals(map, World.world.tiles_map) || version != (int)heightsVersion.GetValue(current);
        if (!changed)
            for (int y = y0; y < y1 && !changed; y++) for (int x = x0; x < x1; x++)
                if ((flags[y * width + x] & 91) != (actual[y * width + x] & 91)) { changed = true; break; }
        if (!changed) return snapshot;
        // 常规世界每格两个采样；大地图保持最多四百万格点，避免无界纹理与数组。
        float rate = Mathf.Min(2f, 2048f / Mathf.Max(width, height));
        int columns = Mathf.Max(1, Mathf.CeilToInt(width * rate)), rows = Mathf.Max(1, Mathf.CeilToInt(height * rate));
        var values = new float[columns * rows];
        for (int y = 0; y < rows; y++) for (int x = 0; x < columns; x++)
            values[y * columns + x] = heightAt((x + .5f) * width / columns, (y + .5f) * height / rows);
        snapshot = new AbilityHeightField(columns, rows, new Vector2(width, height), values, (float)outsideField.GetValue(cache));
        flags = (byte[])actual.Clone(); map = World.world.tiles_map; version = (int)heightsVersion.GetValue(current);
        return snapshot;
    }
}
