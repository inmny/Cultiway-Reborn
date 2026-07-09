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
    /// 仅火系/水系在掠过时有效果，其他元素命中时才影响地面。
    /// </summary>
    public static void OnFlyOver(Vector3 pos, SkillVfxElementStyle style)
    {
        var tile = World.world.GetTile(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y));
        if (tile == null) return;

        switch (style)
        {
            case SkillVfxElementStyle.Fire:
                tile.setBurned();
                break;
            case SkillVfxElementStyle.Water:
                tile.freeze(1);
                break;
        }
    }

    /// <summary>
    /// 法术命中时对地面的完整影响。由 <see cref="SkillHitResolver"/> 和词条副作用调用。
    /// </summary>
    /// <param name="pos">命中世界坐标。</param>
    /// <param name="style">元素视觉风格。</param>
    /// <param name="radius">影响半径（0 表示单体命中）。</param>
    /// <param name="isArea">是否范围命中。</param>
    public static void OnImpact(Vector3 pos, SkillVfxElementStyle style, float radius, bool isArea)
    {
        var tile = World.world.GetTile(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y));
        if (tile == null) return;

        var rad = isArea ? Mathf.Max(1, Mathf.RoundToInt(radius)) : 0;

        switch (style)
        {
            case SkillVfxElementStyle.Fire:
                ApplyFireImpact(tile, rad, isArea);
                break;
            case SkillVfxElementStyle.Water:
                ApplyWaterImpact(tile, rad, isArea);
                break;
            case SkillVfxElementStyle.Earth:
                ApplyEarthImpact(tile, rad, isArea);
                break;
            case SkillVfxElementStyle.Metal:
                ApplyMetalImpact(tile, rad, isArea);
                break;
            case SkillVfxElementStyle.Wood:
                ApplyWoodImpact(tile, rad, isArea);
                break;
            case SkillVfxElementStyle.Wind:
                ApplyWindImpact(tile, rad, isArea);
                break;
            case SkillVfxElementStyle.Lightning:
                ApplyLightningImpact(tile, rad, isArea);
                break;
            case SkillVfxElementStyle.Neg:
                ApplyNegImpact(tile, rad, isArea);
                break;
            case SkillVfxElementStyle.Pos:
                ApplyPosImpact(tile, rad, isArea);
                break;
            case SkillVfxElementStyle.Entropy:
                ApplyEntropyImpact(tile, rad, isArea);
                break;
        }
    }

    // === 元素专属命中影响 ===

    private static void ApplyFireImpact(WorldTile tile, int rad, bool isArea)
    {
        if (isArea && rad > 0)
        {
            MapAction.damageWorld(tile, rad, WorldboxGame.Terraforms.HitGround);
        }
        else
        {
            tile.startFire(true);
            tile.setBurned();
        }
    }

    private static void ApplyWaterImpact(WorldTile tile, int rad, bool isArea)
    {
        if (isArea && rad > 0)
        {
            ForEachTileInRadius(tile, rad, t => t.freeze(3));
        }
        else
        {
            tile.freeze(3);
        }
    }

    private static void ApplyEarthImpact(WorldTile tile, int rad, bool isArea)
    {
        if (isArea && rad > 0)
        {
            var earthquake = AssetManager.terraform.get("earthquake");
            if (earthquake != null) MapAction.damageWorld(tile, rad, earthquake);
        }
        else
        {
            MapAction.decreaseTile(tile, true);
        }
    }

    private static void ApplyMetalImpact(WorldTile tile, int rad, bool isArea)
    {
        if (isArea && rad > 0)
        {
            ForEachTileInRadius(tile, rad, t => MapAction.decreaseTile(t, true));
        }
        else
        {
            tile.setTopTileType(null);
        }
    }

    private static void ApplyWoodImpact(WorldTile tile, int rad, bool isArea)
    {
        if (isArea && rad > 0)
        {
            ForEachTileInRadius(tile, rad, t => MapAction.increaseTile(t, false));
        }
        else
        {
            MapAction.increaseTile(tile, false);
        }
    }

    private static void ApplyWindImpact(WorldTile tile, int rad, bool isArea)
    {
        var forceRad = isArea && rad > 0 ? rad : 2;
        World.world.applyForceOnTile(tile, forceRad, 2f);
    }

    private static void ApplyLightningImpact(WorldTile tile, int rad, bool isArea)
    {
        var lightning = AssetManager.terraform.get("lightning_power");
        if (lightning != null && rad > 0)
        {
            MapAction.damageWorld(tile, Mathf.Max(1, rad), lightning);
        }
        else
        {
            tile.setBurned();
        }
    }

    private static void ApplyNegImpact(WorldTile tile, int rad, bool isArea)
    {
        if (isArea && rad > 0)
        {
            ForEachTileInRadius(tile, rad, t =>
            {
                t.setBurnedStage(15);
                if (t.top_type == null)
                {
                    var wasteland = t.Height > 1 ? TopTileLibrary.wasteland_high : TopTileLibrary.wasteland_low;
                    t.setTopTileType(wasteland);
                }
            });
        }
        else
        {
            tile.setBurnedStage(15);
        }
    }

    private static void ApplyPosImpact(WorldTile tile, int rad, bool isArea)
    {
        if (isArea && rad > 0)
        {
            ForEachTileInRadius(tile, rad, t => MapAction.increaseTile(t, false));
        }
        else
        {
            MapAction.increaseTile(tile, false);
        }
    }

    private static void ApplyEntropyImpact(WorldTile tile, int rad, bool isArea)
    {
        if (isArea && rad > 0)
        {
            ForEachTileInRadius(tile, rad, t => MapAction.decreaseTile(t, true));
        }
        else
        {
            MapAction.decreaseTile(tile, true);
        }
    }

    /// <summary>
    /// 对以 center 为中心、radius 半径内的所有 tile 执行 action。
    /// </summary>
    private static void ForEachTileInRadius(WorldTile center, int radius, System.Action<WorldTile> action)
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
