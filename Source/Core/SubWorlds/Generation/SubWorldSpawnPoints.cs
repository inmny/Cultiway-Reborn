using System;
using System.Collections.Generic;

namespace Cultiway.Core.SubWorlds.Generation;

/// <summary>生成器声明的命名 row-major 出生点。</summary>
internal readonly struct SubWorldSpawnPoint
{
    internal SubWorldSpawnPoint(string name, int tileIndex)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("SpawnPoint 名称为空", nameof(name));
        Name = name;
        TileIndex = tileIndex;
    }

    internal string Name { get; }
    internal int TileIndex { get; }
}

/// <summary>保存一个 Runtime 的强类型命名出生点索引。</summary>
internal sealed class SubWorldSpawnPointCollection
{
    private readonly Dictionary<string, int> points = new(StringComparer.Ordinal);

    internal SubWorldSpawnPointCollection(SubWorldSpawnPoint[] source, int tileCount)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (tileCount <= 0) throw new ArgumentOutOfRangeException(nameof(tileCount));
        for (int i = 0; i < source.Length; i++)
        {
            SubWorldSpawnPoint point = source[i];
            if ((uint)point.TileIndex >= (uint)tileCount)
                throw new InvalidOperationException(
                    $"SubWorld SpawnPoint 超出地图: name={point.Name}, tile={point.TileIndex}, count={tileCount}");
            if (!points.TryAdd(point.Name, point.TileIndex))
                throw new InvalidOperationException($"SubWorld SpawnPoint 名称重复: {point.Name}");
        }
    }

    internal bool TryGet(string name, out int tileIndex)
    {
        return points.TryGetValue(name, out tileIndex);
    }

    internal int GetRequired(string name)
    {
        if (!TryGet(name, out int tileIndex))
            throw new KeyNotFoundException($"SubWorld SpawnPoint 不存在: {name}");
        return tileIndex;
    }
}

/// <summary>首批通用命名出生点。</summary>
internal static class SubWorldSpawnPointNames
{
    internal const string Entry = "Entry";
    internal const string Exit = "Exit";
}
