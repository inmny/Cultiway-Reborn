using System.Collections.Generic;
using System.Linq;
using Cultiway.Core.SkillLibV3.Effects;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;

namespace Cultiway.Content.Skills;

public sealed partial class ConstructedAbilityService
{
    /// <summary>真实改地留下的短暂收势。大范围施放也只保留最近的画面。</summary>
    private static readonly List<TerrainChange> terrainChanges = new(256);

    /// <summary>每个地块只保留最近一次真实变化，保护地块不会绘制。</summary>
    private static bool AddTerrainChange(RunningCast running, int node, WorldTile tile, SkillEffectOutcomeFlags flags, double time)
    {
        string form = flags switch
        {
            _ when (flags & SkillEffectOutcomeFlags.TerrainRaised) != 0 => "terrain_raised_slab",
            _ when (flags & SkillEffectOutcomeFlags.TerrainLowered) != 0 => "terrain_lowered_steps",
            _ when (flags & SkillEffectOutcomeFlags.WaterFilled) != 0 => "terrain_water_spread",
            _ when (flags & SkillEffectOutcomeFlags.WaterDrained) != 0 => "terrain_water_withdrawal",
            _ => null
        };
        if (form == null) return false;

        for (int i = terrainChanges.Count - 1; i >= 0; i--)
            if (terrainChanges[i].Tile == tile)
            {
                terrainChanges.RemoveAt(i);
                break;
            }
        if (terrainChanges.Count == 256) terrainChanges.RemoveAt(0);
        var delta = new Vector2(tile.pos.x - running.LastTarget.x, tile.pos.y - running.LastTarget.y);
        float distance = Mathf.Clamp01(delta.magnitude / Mathf.Max(.1f, running.Cast.Plan.Nodes[node].Scale));
        terrainChanges.Add(new TerrainChange(tile, running.Id, form, distance, time));
        return true;
    }

    /// <summary>水铺开或回卷，岩层顶起或沉入。两种视角读取同一组完成姿态。</summary>
    private void AppendTerrainChanges(double now)
    {
        bool water = false;
        for (int i = terrainChanges.Count - 1; i >= 0; i--)
        {
            TerrainChange change = terrainChanges[i];
            float phase = (float)((now - change.Time) / .78f);
            if (phase >= 1f)
            {
                terrainChanges.RemoveAt(i);
                continue;
            }

            if (change.Liquid)
            {
                water = true;
                continue;
            }
            float seed = ((change.Tile.pos.x * 374761393 ^ change.Tile.pos.y * 668265263) & 1023) / 1023f;
            // 结算仍是同一时点。顶起和沉落的余势沿真实落点向外传，错开岩片角度和高度。
            float delay = .19f * change.Distance + .07f * seed;
            float enter = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, .30f, phase - delay));
            float settle = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.28f, 1f, phase));
            bool rising = change.Form == "terrain_raised_slab";
            float radius = .60f + .20f * seed;
            float height = rising ? Mathf.Lerp(.25f, 1.18f, enter) * (1f - .69f * settle) : .71f + seed * .39f;
            float lift = rising ? .30f * enter - .29f * settle : (.20f + .12f * seed) * (1f - enter * settle) - .31f * settle;

            Vector3 point = new(change.Tile.pos.x + .5f, change.Tile.pos.y + .5f, 0f);
            point.z = (terrain?.Sample(point.x, point.y) ?? 0f) + lift + .04f;
            var rotation = Quaternion.Euler(0f, (9f - 18f * seed) * enter, 360f * seed);
            var transform = Matrix4x4.TRS(point, rotation, new Vector3(radius, radius, height));
            float opacity = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.55f, 1f, phase));
            float shade = .82f + .18f * seed;
            visuals.Add(new FormVisualInstance(FormResources.Load(change.Form), transform,
                dye: new Color(shade, shade, shade, opacity), terrain: terrain));
        }

        if (water) AppendChangedWater(now);
    }

    /// <summary>用共同水面合拢相邻成功地块，保留未改变的空隙。每个小区复用同一个网格。</summary>
    private void AppendChangedWater(double now)
    {
        const int size = 16;
        var patches = terrainChanges.Where(change => change.Liquid).GroupBy(change =>
            (change.Cast, X: change.Tile.pos.x / size * size, Y: change.Tile.pos.y / size * size));
        foreach (var patch in patches)
        {
            TerrainChange first = patch.First();
            bool filling = first.Form == "terrain_water_spread";
            float phase = Mathf.Clamp01((float)((now - first.Time) / .78f));
            float front = filling ? phase * 2.3f - .10f : 1.08f - phase * 1.7f;
            float settling = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.48f, 1f, phase));
            var depth = new float[size * size];
            var ground = new float[depth.Length];
            foreach (TerrainChange cell in patch)
            {
                int x = cell.Tile.pos.x, y = cell.Tile.pos.y;
                int index = x - patch.Key.X + (y - patch.Key.Y) * size;
                float distance = (cell.Distance - front) / .24f;
                float swell = Mathf.Exp(-distance * distance);
                float sheet = filling
                    ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-.20f, .15f, front - cell.Distance))
                    : Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-.18f, .20f, front - cell.Distance));
                depth[index] = .015f + settling * (.10f * sheet + .105f * swell);
                ground[index] = (terrain?.Sample(x + .5f, y + .5f) ?? 0f) + .03f;
            }

            var layer = new FormLayerFrame(first, new Vector2(patch.Key.X, patch.Key.Y), size, depth, ground);
            visuals.Add(new FormVisualInstance(FormResources.Load("world_domain"), Matrix4x4.identity,
                surface: "water", dye: new Color(.9f, .97f, 1f, settling), terrain: terrain, layer: layer));
        }
    }

    private sealed class TerrainChange
    {
        internal readonly WorldTile Tile;
        internal readonly long Cast;
        internal readonly string Form;
        internal readonly float Distance;
        internal readonly double Time;
        internal bool Liquid => Form is "terrain_water_spread" or "terrain_water_withdrawal";

        internal TerrainChange(WorldTile tile, long cast, string form, float distance, double time)
        {
            Tile = tile;
            Cast = cast;
            Form = form;
            Distance = distance;
            Time = time;
        }
    }
}
