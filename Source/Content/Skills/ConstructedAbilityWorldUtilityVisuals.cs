using System.Collections.Generic;
using System.Linq;
using Cultiway.Core.SkillLibV3.Effects;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;

namespace Cultiway.Content.Skills;

public sealed partial class ConstructedAbilityService
{
    /// <summary>生长、净土与催熟只在已经确认的地块上留下短暂画面。</summary>
    private static readonly List<WorldUtilityVisual> worldUtilityVisuals = new(128);

    /// <summary>按实际结果选用画面，失败的尝试和未受影响的邻格都不添加形体。</summary>
    private static bool AddWorldUtilityVisual(RunningCast running, int node, WorldTile tile, SkillEffectOutcomeFlags flags, double now)
    {
        if (node < 0) return false;
        string form = running.Cast.Plan.Id switch
        {
            "Cultiway.NatureGrowthField" when (flags & SkillEffectOutcomeFlags.FloraCreated) != 0 => "utility_young_growth",
            "Cultiway.CleanLandField" when (flags & LandPollution) != 0 => "world_domain",
            "Cultiway.Fertilize" when (flags & SkillEffectOutcomeFlags.CropFertilized) != 0 => "utility_ripe_grain",
            "Cultiway.SummonRainCloud" when (flags & SkillEffectOutcomeFlags.RainCloudSummoned) != 0 => "utility_rain_condensate",
            _ => null
        };
        if (form == null) return false;

        for (int i = worldUtilityVisuals.Count - 1; i >= 0; i--)
            if (worldUtilityVisuals[i].Tile == tile)
            {
                worldUtilityVisuals.RemoveAt(i);
                break;
            }
        if (worldUtilityVisuals.Count == 128) worldUtilityVisuals.RemoveAt(0);
        var delta = new Vector2(tile.pos.x - running.LastTarget.x, tile.pos.y - running.LastTarget.y);
        float distance = Mathf.Clamp01(delta.magnitude / Mathf.Max(.1f, running.Cast.Plan.Nodes[node].Scale));
        worldUtilityVisuals.Add(new WorldUtilityVisual(tile, form, running.Id, distance, now));
        return true;
    }

    /// <summary>净土可移除的真实状态，用于区分其他构造效果。</summary>
    private const SkillEffectOutcomeFlags LandPollution = SkillEffectOutcomeFlags.FireRemoved | SkillEffectOutcomeFlags.BurnRemoved |
        SkillEffectOutcomeFlags.FrozenRemoved | SkillEffectOutcomeFlags.HeatRemoved | SkillEffectOutcomeFlags.WastelandRemoved;

    /// <summary>芽叶向上长成，麦穗收束，水珠在真实成云处聚合。</summary>
    private void AppendWorldUtilityVisuals(double now)
    {
        bool wash = false;
        for (int i = worldUtilityVisuals.Count - 1; i >= 0; i--)
        {
            WorldUtilityVisual change = worldUtilityVisuals[i];
            bool grain = change.Form == "utility_ripe_grain";
            bool rain = change.Form == "utility_rain_condensate";
            float phase = (float)((now - change.Time) / (change.Wash ? .64f : grain ? .88f : 1.25f));
            if (phase >= 1f)
            {
                worldUtilityVisuals.RemoveAt(i);
                continue;
            }
            if (change.Wash)
            {
                wash = true;
                continue;
            }

            float seed = ((change.Tile.pos.x * 374761393 ^ change.Tile.pos.y * 668265263) & 1023) / 1023f;
            float reveal = Mathf.SmoothStep(0f, 1f, phase * 2.55f);
            float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.52f, 1f, phase));
            Vector3 position = new(change.Tile.pos.x + .5f, change.Tile.pos.y + .5f, 0f);
            position.z = (terrain?.Sample(position.x, position.y) ?? 0f) + .045f;
            if (rain) position.z += .4f + 1.35f * Mathf.SmoothStep(0f, 1f, phase);
            float spread = rain ? 1f - .42f * reveal : Mathf.Lerp(.26f, .95f, reveal);
            float height = Mathf.Lerp(.20f, 1.13f + .2f * seed, reveal);
            var rotation = Quaternion.AngleAxis(seed * 360f + (grain ? 28f * reveal : 0f), Vector3.forward);
            Matrix4x4 pose = Matrix4x4.TRS(position, rotation, new Vector3(spread, spread, height));
            visuals.Add(new FormVisualInstance(FormResources.Load(change.Form), pose,
                dye: new Color(1f, 1f, 1f, fade), terrain: terrain));
        }
        if (wash) AppendGroundWash(now);
    }

    /// <summary>同一轮成功清理的邻格共用薄面，未清理的格子始终留空。</summary>
    private void AppendGroundWash(double now)
    {
        const int size = 16;
        var patches = worldUtilityVisuals.Where(mark => mark.Wash).GroupBy(mark =>
            (mark.Cast, mark.Time, X: mark.Tile.pos.x / size * size, Y: mark.Tile.pos.y / size * size));
        foreach (var patch in patches)
        {
            float phase = Mathf.Clamp01((float)((now - patch.Key.Time) / .64f));
            float fading = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.14f, 1f, phase));
            var thickness = new float[size * size];
            var ground = new float[thickness.Length];
            foreach (WorldUtilityVisual mark in patch)
            {
                int x = mark.Tile.pos.x, y = mark.Tile.pos.y;
                int index = x - patch.Key.X + (y - patch.Key.Y) * size;
                float wave = (mark.Distance - phase * 1.8f) / .32f;
                thickness[index] = .015f + fading * (.044f + .092f * Mathf.Exp(-wave * wave));
                ground[index] = (terrain?.Sample(x + .5f, y + .5f) ?? 0f) + .03f;
            }
            var layer = new FormLayerFrame(patch.First(), new Vector2(patch.Key.X, patch.Key.Y), size, thickness, ground);
            visuals.Add(new FormVisualInstance(FormResources.Load("world_domain"), Matrix4x4.identity,
                surface: "purification_pearl", dye: new Color(.70f, .91f, 1f, .67f * fading), terrain: terrain, layer: layer));
        }
    }

    private sealed class WorldUtilityVisual
    {
        internal readonly WorldTile Tile;
        internal readonly string Form;
        internal readonly long Cast;
        internal readonly float Distance;
        internal readonly double Time;
        internal bool Wash => Form == "world_domain";

        internal WorldUtilityVisual(WorldTile tile, string form, long cast, float distance, double time)
        {
            Tile = tile;
            Form = form;
            Cast = cast;
            Distance = distance;
            Time = time;
        }
    }
}
