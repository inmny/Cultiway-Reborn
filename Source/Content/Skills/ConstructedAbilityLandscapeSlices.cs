using Cultiway.Core.Skills;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;

namespace Cultiway.Content.Skills;

public sealed partial class ConstructedAbilityService
{
    /// <summary>取景从最初对合的位置采集一次，逐轮只移动实际形体。</summary>
    /// <param name="running">已经确认的本次施放。</param>
    private void AppendLandscapeSlices(RunningCast running)
    {
        int[] nodes = running.Cast.Plan.LandscapeNodes;
        for (int i = 0; i < nodes.Length; i++)
        {
            int node = nodes[i];
            if (!running.Cast.TryLandscapeFrame(node, out var actual, out var source)) continue;
            CompiledForm form = running.Cast.Plan.Nodes[node];
            running.Landscapes ??= new LandscapeSnapshot[nodes.Length];
            LandscapeSnapshot page = running.Landscapes[i] ??= new LandscapeSnapshot(form.LandscapeSlice, source, terrain);
            visuals.Add(new FormVisualInstance(form.Resource, page.Scene, actual, Color.white, terrain, "space_destination"));
            if (!form.AffectsActors) continue;

            bool cutting = running.Cast.Age >= form.Active.x && running.Cast.Age < form.Active.y;
            var tint = cutting ? new Color(.53f, .87f, .97f, .84f) : new Color(.25f, .42f, .58f, .22f);
            visuals.Add(new FormVisualInstance(form.Resource, page.Edge, actual, tint, terrain, "space_gate_inlay"));
        }
    }

    /// <summary>统一归还临时地貌网格；从未取得原地图上的施工、物件或人物控制。</summary>
    /// <param name="running">本次实际保存了取景的施放。</param>
    private static void ReleaseLandscapeSlices(RunningCast running)
    {
        if (running.Landscapes == null) return;
        foreach (LandscapeSnapshot page in running.Landscapes) page?.Dispose();
        running.Landscapes = null;
    }
}
