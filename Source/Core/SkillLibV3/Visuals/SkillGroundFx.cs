using UnityEngine;

namespace Cultiway.Core.SkillLibV3.Visuals;

/// <summary>
/// 法术对地面的真实影响：按元素风格修改地形（火烧焦、水凝冰、土砸坑、金碎石等）。
/// 飞行掠过时轻量影响（焦痕线/凝冰线），命中时完整影响。
/// </summary>
public static class SkillGroundFx
{
    /// <summary>
    /// 法术飞行掠过时对地面的轻量影响。由 <see cref="Systems.LogicSkillGroundFxRecordSystem"/> 按距离节流调用。
    /// </summary>
    public static void OnFlyOver(Vector3 pos, SkillVfxElementAsset element)
    {
        var tile = GetTile(pos);
        if (tile == null) return;

        element.ApplyGroundFlyOver(tile);
    }

    /// <summary>
    /// 法术命中时对地面的完整影响。由 <see cref="SkillHitResolver"/> 和词条副作用调用。
    /// </summary>
    /// <param name="pos">命中世界坐标。</param>
    /// <param name="element">元素视觉资产。</param>
    /// <param name="radius">影响半径（0 表示单体命中）。</param>
    /// <param name="isArea">是否范围命中。</param>
    /// <param name="sourceObj">效果来源，用于需要攻击者境界参与结算的地形反馈。</param>
    public static void OnImpact(Vector3 pos, SkillVfxElementAsset element, float radius, bool isArea,
        BaseSimObject sourceObj)
    {
        var tile = GetTile(pos);
        if (tile == null) return;

        var rad = isArea ? Mathf.Max(1, Mathf.RoundToInt(radius)) : 0;
        element.ApplyGroundImpact(tile, rad, isArea, sourceObj);
    }

    public static WorldTile GetTile(Vector3 pos)
    {
        return World.world.GetTile(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y));
    }

    public static void ForEachTileInRadius(WorldTile center, int radius, System.Action<WorldTile> action)
    {
        if (radius <= 0 || center == null) return;
        var cx = center.pos.x;
        var cy = center.pos.y;
        for (var dx = -radius; dx <= radius; dx++)
        for (var dy = -radius; dy <= radius; dy++)
        {
            if (dx * dx + dy * dy > radius * radius) continue;
            var t = World.world.GetTile(cx + dx, cy + dy);
            if (t != null) action(t);
        }
    }
}
