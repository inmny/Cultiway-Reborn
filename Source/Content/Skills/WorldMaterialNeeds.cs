using System.Collections.Generic;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>从世界材料自身维护日常回收机会，查询与正式选择共用实际材料形状。</summary>
public sealed partial class WorldMaterialService
{
    /// <summary>随现有空间索引一起更新的临时材料归属，避免每个人扫描全部世界记录。</summary>
    private static readonly Dictionary<long, List<WorldMaterialRecord>> temporaryByProvider = new();

    /// <summary>学习世界回收后，只为确实存在临时材料的人物唤醒需求。</summary>
    /// <param name="provider">实际支付者。</param>
    internal static bool HasTemporaryNeed(long provider)
    {
        instance.RebuildIndex();
        return temporaryByProvider.ContainsKey(provider);
    }

    /// <summary>取得一个真实临时材料机会；有材料但暂时无实际形状时仍保留下一次查询。</summary>
    /// <param name="provider">原支付者。</param><param name="offset">此人遍历材料的轮换位置。</param>
    /// <param name="id">选中的稳定材料编号。</param><param name="point">真实作用形状中心。</param>
    /// <param name="resource">原支付资源。</param>
    internal static bool TryTemporaryNeed(long provider, ref int offset, out long id, out Vector3 point,
        out string resource)
    {
        instance.RebuildIndex();
        id = 0;
        point = default;
        resource = null;
        if (!temporaryByProvider.TryGetValue(provider, out var owned) || owned.Count == 0) return false;
        WorldMaterialRecord row = owned[offset % owned.Count];
        offset = (offset + 1) % owned.Count;
        instance.shapes.Clear();
        row.Material.Contacts(instance.shapes);
        foreach (AbilityCapsule shape in instance.shapes)
        {
            if (row.Material.Flow?.Budget != null && row.Material.Flow.Budget[shape.Shape] <= 0) continue;
            id = row.Id;
            point = (shape.A + shape.B) * .5f;
            resource = row.Resource;
            break;
        }

        return true;
    }

    /// <summary>只读预估一份材料在真实选择形状内的剩余原支付份额，不取走或预先入账。</summary>
    /// <param name="id">实际材料编号。</param><param name="selection">共同构造算出的选择形状。</param>
    internal static double EstimateRecovery(long id, IReadOnlyList<AbilityCapsule> selection)
    {
        if (!rows.TryGetValue(id, out var row) || row.Finished || row.Recovering || row.Expires <= 0) return 0;
        bool selected = row.WorldGrid
            ? row.Material.SelectShared(selection, instance.picked, instance.waterColumns[WaterKey(row)])
            : row.Material.Select(selection, instance.picked);
        if (!selected) return 0;
        if (row.Material.Flow == null) return row.Material.Resource;
        double budget = 0, volume = 0;
        foreach (int cell in instance.picked)
        {
            if (row.Material.Flow.Budget != null) budget += row.Material.Flow.Budget[cell];
            volume += row.Material.Flow.Water[cell].x * row.Material.Flow.Cell * row.Material.Flow.Cell;
        }

        return row.Material.Flow.Budget != null
            ? budget
            : row.Material.Volume > 0
                ? row.Material.Resource * volume / row.Material.Volume
                : 0;
    }
}
