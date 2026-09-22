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
/// 数据经 <see cref="ModSaveManager"/> 持久化为独立 JSON 文档；
/// 消亡单位的残留条目体积极小，不做主动清理。
/// </summary>
public static class YuanshenOriginBannerStore
{
    private const string DocumentId = "yuanshen_origin_banners";
    private const string StorageKey = "Saves/world/yuanshen_origin_banners";
    private const int CurrentVersion = 1;

    private static SaveDocument<YuanshenOriginBannerData> _document;

    /// <summary>注册持久化文档。必须在 ModClass 构造 Persistence 之后调用。</summary>
    public static void Initialize(ModSaveManager saveManager)
    {
        if (_document != null) return;
        _document = saveManager.Register(new SaveDocumentDefinition<YuanshenOriginBannerData>(
            DocumentId,
            StorageKey,
            CurrentVersion,
            () => new YuanshenOriginBannerData(),
            Array.Empty<ISaveMigration>()));
    }

    /// <summary>登记一个元神的原肉身种族资产 id；转化事件级频率，直接落盘。</summary>
    public static void Record(long actorId, string originAssetId)
    {
        if (_document == null || string.IsNullOrEmpty(originAssetId)) return;
        _document.Data.Origins[actorId] = originAssetId;
        _document.Save();
    }

    /// <summary>读取元神登记的原肉身种族资产 id；没有记忆（存量或自然诞生）返回假。</summary>
    public static bool TryGet(long actorId, out string originAssetId)
    {
        if (_document != null &&
            _document.Data.Origins.TryGetValue(actorId, out var stored) &&
            !string.IsNullOrEmpty(stored))
        {
            originAssetId = stored;
            return true;
        }
        originAssetId = null;
        return false;
    }
}
