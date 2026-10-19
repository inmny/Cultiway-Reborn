using System;
using System.Collections.Generic;
using Cultiway.Core.Persistence;

namespace Cultiway.Content;

/// <summary>持久化数据：元神单位 id → 原肉身种族资产 id。</summary>
public class YuanshenOriginBannerData
{
    /// <summary>键为原版单位 id，值为肉身毁灭前的种族资产 id。</summary>
    public Dictionary<long, string> Origins = new();
}

/// <summary>
/// 元神原肉身种族的记忆库。
/// 化神修士肉身毁灭转为无身元神时，在此登记原种族资产 id（以原版单位 id 为键），
/// 供元神王国的城市名牌解析"跟随肉身种族"的横幅背景。
/// 数据随原版世界文件一起保存；
/// 消亡单位的残留条目体积极小，不做主动清理。
/// </summary>
public static class YuanshenOriginBannerStore
{
    /// <summary>只属于当前世界的原肉身资料。</summary>
    private static YuanshenOriginBannerData data = new();

    /// <summary>随世界保存，切换世界时清空，分享世界时一起携带。</summary>
    public static void Initialize()
    {
        ModClass.I.WorldSaves.World.Register(new SaveSection<MapBox, YuanshenOriginBannerData>(
            "yuanshen-origins", "元神原肉身种族", 1, _ => data, (_, record) => data = record) { BeforeActors = true });
        ModClass.I.WorldSaves.WorldCleared += () => data = new YuanshenOriginBannerData();
    }

    /// <summary>登记原种族，下一次保存世界时一起写入。</summary>
    /// <param name="actorId">原版人物编号。</param>
    /// <param name="originAssetId">原肉身的种族编号。</param>
    public static void Record(long actorId, string originAssetId)
    {
        if (!string.IsNullOrEmpty(originAssetId)) data.Origins[actorId] = originAssetId;
    }

    /// <summary>取得当前世界内人物的原肉身种族。</summary>
    /// <param name="actorId">原版人物编号。</param>
    /// <param name="originAssetId">找到的原肉身种族编号。</param>
    public static bool TryGet(long actorId, out string originAssetId)
        => data.Origins.TryGetValue(actorId, out originAssetId) && !string.IsNullOrEmpty(originAssetId);
}
