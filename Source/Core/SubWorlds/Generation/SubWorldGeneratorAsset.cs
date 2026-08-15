using System;
using Cultiway.Core.SubWorlds.Model;

namespace Cultiway.Core.SubWorlds.Generation;

/// <summary>
/// 根据模板、种子和创建参数生成小世界初始场景的资产基类。
/// </summary>
public abstract class SubWorldGeneratorAsset : Asset
{
    /// <summary>验证生成器具备可注册的资产 ID。</summary>
    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(id)) throw new InvalidOperationException("SubWorld Generator 缺少 ID");
    }

    /// <summary>
    /// 创建一个尚未绑定 Runtime 的完整初始场景。
    /// </summary>
    /// <param name="template">决定地图尺寸和相关配置引用的模板。</param>
    /// <param name="seed">本次场景生成使用的创建种子。</param>
    /// <param name="anchor">该实例在主世界中的锚点。</param>
    /// <param name="parameters">本次创建的附加参数。</param>
    /// <returns>地图数据和初始实体放置结果。</returns>
    internal abstract SubWorldGeneratedScene Generate(
        SubWorldTemplateAsset template,
        int seed,
        SubWorldAnchor anchor,
        SubWorldCreationParameters parameters);
}

/// <summary>
/// 保存生成器构造完成、等待 Runtime 接管的初始场景。
/// </summary>
internal sealed class SubWorldGeneratedScene
{
    /// <summary>
    /// 创建生成结果。
    /// </summary>
    /// <param name="mapData">生成完成的地图数据。</param>
    /// <param name="initialPawnTileIndex">测试 Pawn 的初始格子索引。</param>
    internal SubWorldGeneratedScene(SubWorldMapData mapData, int initialPawnTileIndex)
    {
        MapData = mapData;
        InitialPawnTileIndex = initialPawnTileIndex;
    }

    /// <summary>生成完成的地图数据；所有权将在创建时交给 Runtime。</summary>
    internal SubWorldMapData MapData { get; }

    /// <summary>测试 Pawn 的初始 row-major 格子索引。</summary>
    internal int InitialPawnTileIndex { get; }
}
