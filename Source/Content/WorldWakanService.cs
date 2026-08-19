using System;
using System.Threading;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>当前世界的洁净灵气和浊气统计结果。</summary>
public readonly struct WorldWakanStatistics
{
    public WorldWakanStatistics(float sum, float average, float maximum, float minimum)
    {
        Sum = sum;
        Average = average;
        Maximum = maximum;
        Minimum = minimum;
    }

    public float Sum { get; }
    public float Average { get; }
    public float Maximum { get; }
    public float Minimum { get; }
}

/// <summary>
/// 统一管理当前世界的洁净灵气和浊气。
/// 运行中的数值只由这里修改，地图绘制读取单独公布的显示副本。
/// </summary>
public static class WorldWakanService
{
    public static float DefaultCleanBackground { get; private set; }
    public static float MaximumValue { get; private set; }

    private static float[] cleanValues;
    private static float[] dirtyValues;
    private static float[] displayCleanValues = Array.Empty<float>();
    private static float[] displayDirtyValues = Array.Empty<float>();
    private static int width;
    private static int height;
    private static int displayRevision;
    private static bool initialized;
    private static bool displayPending;

    public static bool IsInitialized => initialized;
    public static int Width => width;
    public static int Height => height;
    public static int TileCount => cleanValues?.Length ?? 0;
    public static int DisplayRevision => Volatile.Read(ref displayRevision);

    internal static void ConfigureBalance(float cleanBackground, float maximumValue)
    {
        if (cleanBackground < 0f || maximumValue <= cleanBackground)
            throw new ArgumentOutOfRangeException(nameof(maximumValue), "天地灵气上限必须高于非负背景值");
        DefaultCleanBackground = cleanBackground;
        MaximumValue = maximumValue;
    }

    /// <summary>为新世界建立两张地块资源图，并清除旧世界内容。</summary>
    public static void InitializeWorld(int mapWidth, int mapHeight)
    {
        InitializeWorld(mapWidth, mapHeight, DefaultCleanBackground);
    }

    public static void InitializeWorld(int mapWidth, int mapHeight, float cleanBackground)
    {
        if (mapWidth <= 0 || mapHeight <= 0)
        {
            ClearWorld();
            return;
        }

        width = mapWidth;
        height = mapHeight;
        int tileCount = checked(mapWidth * mapHeight);
        cleanValues = new float[tileCount];
        dirtyValues = new float[tileCount];
        float background = Mathf.Clamp(cleanBackground, 0f, MaximumValue);
        for (int i = 0; i < tileCount; i++) cleanValues[i] = background;

        displayCleanValues = (float[])cleanValues.Clone();
        displayDirtyValues = (float[])dirtyValues.Clone();
        initialized = true;
        displayPending = false;
        Interlocked.Increment(ref displayRevision);
    }

    /// <summary>清空当前世界的两张资源图。</summary>
    public static void ClearWorld()
    {
        cleanValues = null;
        dirtyValues = null;
        displayCleanValues = Array.Empty<float>();
        displayDirtyValues = Array.Empty<float>();
        width = 0;
        height = 0;
        initialized = false;
        displayPending = false;
        Interlocked.Increment(ref displayRevision);
    }

    public static int GetTileId(int x, int y)
    {
        return x >= 0 && x < width && y >= 0 && y < height ? y * width + x : -1;
    }

    public static float GetClean(int tileId)
    {
        return IsValidTileId(tileId) ? Mathf.Max(0f, cleanValues[tileId]) : 0f;
    }

    public static float GetDirty(int tileId)
    {
        return IsValidTileId(tileId) ? Mathf.Max(0f, dirtyValues[tileId]) : 0f;
    }

    public static float GetClean(int x, int y)
    {
        return GetClean(GetTileId(x, y));
    }

    public static float GetDirty(int x, int y)
    {
        return GetDirty(GetTileId(x, y));
    }

    public static float GetClean(WorldTile tile)
    {
        return tile?.data == null ? 0f : GetClean(tile.data.tile_id);
    }

    public static float GetDirty(WorldTile tile)
    {
        return tile?.data == null ? 0f : GetDirty(tile.data.tile_id);
    }

    public static float GetDisplayClean(int tileId)
    {
        float[] values = Volatile.Read(ref displayCleanValues);
        return (uint)tileId < (uint)values.Length ? Mathf.Max(0f, values[tileId]) : 0f;
    }

    public static float GetDisplayDirty(int tileId)
    {
        float[] values = Volatile.Read(ref displayDirtyValues);
        return (uint)tileId < (uint)values.Length ? Mathf.Max(0f, values[tileId]) : 0f;
    }

    /// <summary>直接设置一格洁净灵气，供世界初始化和灵脉初始分布使用。</summary>
    public static void SetClean(int tileId, float value)
    {
        if (!IsValidTileId(tileId)) return;
        float next = ClampValue(value);
        if (Mathf.Approximately(cleanValues[tileId], next)) return;
        cleanValues[tileId] = next;
        displayPending = true;
    }

    /// <summary>直接设置一格浊气，供世界初始化和清理逻辑使用。</summary>
    public static void SetDirty(int tileId, float value)
    {
        if (!IsValidTileId(tileId)) return;
        float next = ClampValue(value);
        if (Mathf.Approximately(dirtyValues[tileId], next)) return;
        dirtyValues[tileId] = next;
        displayPending = true;
    }

    public static float AddClean(int tileId, float amount)
    {
        if (!IsValidTileId(tileId) || amount <= 0f) return 0f;
        float current = Mathf.Max(0f, cleanValues[tileId]);
        float actual = Mathf.Min(amount, MaximumValue - current);
        if (actual <= 0f) return 0f;
        cleanValues[tileId] = current + actual;
        displayPending = true;
        return actual;
    }

    public static float AddDirty(int tileId, float amount)
    {
        if (!IsValidTileId(tileId) || amount <= 0f) return 0f;
        float current = Mathf.Max(0f, dirtyValues[tileId]);
        float actual = Mathf.Min(amount, MaximumValue - current);
        if (actual <= 0f) return 0f;
        dirtyValues[tileId] = current + actual;
        displayPending = true;
        return actual;
    }

    public static float WithdrawClean(int tileId, float amount)
    {
        if (!IsValidTileId(tileId) || amount <= 0f) return 0f;
        float current = Mathf.Max(0f, cleanValues[tileId]);
        float actual = Mathf.Min(current, amount);
        if (actual <= 0f) return 0f;
        cleanValues[tileId] = current - actual;
        displayPending = true;
        return actual;
    }

    public static float WithdrawDirty(int tileId, float amount)
    {
        if (!IsValidTileId(tileId) || amount <= 0f) return 0f;
        float current = Mathf.Max(0f, dirtyValues[tileId]);
        float actual = Mathf.Min(current, amount);
        if (actual <= 0f) return 0f;
        dirtyValues[tileId] = current - actual;
        displayPending = true;
        return actual;
    }

    public static float ScaleClean(int tileId, float multiplier)
    {
        if (!IsValidTileId(tileId) || multiplier < 0f) return 0f;
        float before = GetClean(tileId);
        float after = ClampValue(before * multiplier);
        if (!Mathf.Approximately(before, after))
        {
            cleanValues[tileId] = after;
            displayPending = true;
        }

        return after;
    }

    public static float TransferClean(int sourceTileId, int targetTileId, float amount)
    {
        if (!IsValidTileId(sourceTileId) || !IsValidTileId(targetTileId) ||
            sourceTileId == targetTileId || amount <= 0f)
        {
            return 0f;
        }

        float source = GetClean(sourceTileId);
        float target = GetClean(targetTileId);
        float actual = Mathf.Min(amount, source, MaximumValue - target);
        if (actual <= 0f) return 0f;
        cleanValues[sourceTileId] = source - actual;
        cleanValues[targetTileId] = target + actual;
        displayPending = true;
        return actual;
    }

    public static float TransferDirty(int sourceTileId, int targetTileId, float amount)
    {
        if (!IsValidTileId(sourceTileId) || !IsValidTileId(targetTileId) ||
            sourceTileId == targetTileId || amount <= 0f)
        {
            return 0f;
        }

        float source = GetDirty(sourceTileId);
        float target = GetDirty(targetTileId);
        float actual = Mathf.Min(amount, source, MaximumValue - target);
        if (actual <= 0f) return 0f;
        dirtyValues[sourceTileId] = source - actual;
        dirtyValues[targetTileId] = target + actual;
        displayPending = true;
        return actual;
    }

    /// <summary>返回当前运行中的洁净灵气统计，不读取地图显示副本。</summary>
    public static WorldWakanStatistics GetCleanStatistics()
    {
        return BuildStatistics(cleanValues);
    }

    /// <summary>返回当前运行中的浊气统计，不读取地图显示副本。</summary>
    public static WorldWakanStatistics GetDirtyStatistics()
    {
        return BuildStatistics(dirtyValues);
    }

    /// <summary>
    /// 公布一次地图显示快照。快照只在这里替换，地图绘制线程不会读取运行中的数组。
    /// </summary>
    public static void PublishDisplayValues(bool force = false)
    {
        if (!initialized || (!force && !displayPending)) return;
        Volatile.Write(ref displayCleanValues, (float[])cleanValues.Clone());
        Volatile.Write(ref displayDirtyValues, (float[])dirtyValues.Clone());
        displayPending = false;
        Interlocked.Increment(ref displayRevision);
        ModClass.I?.CustomMapModeManager?.SetRasterMapDirty();
    }

    private static WorldWakanStatistics BuildStatistics(float[] values)
    {
        if (values == null || values.Length == 0) return default;
        float sum = 0f;
        float maximum = 0f;
        float minimum = float.MaxValue;
        for (int i = 0; i < values.Length; i++)
        {
            float value = Mathf.Max(0f, values[i]);
            sum += value;
            maximum = Mathf.Max(maximum, value);
            minimum = Mathf.Min(minimum, value);
        }

        return new WorldWakanStatistics(sum, sum / values.Length, maximum, minimum);
    }

    private static bool IsValidTileId(int tileId)
    {
        return initialized && (uint)tileId < (uint)cleanValues.Length;
    }

    private static float ClampValue(float value)
    {
        return Mathf.Clamp(float.IsNaN(value) || float.IsInfinity(value) ? 0f : value, 0f, MaximumValue);
    }
}
