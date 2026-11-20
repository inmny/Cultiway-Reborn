using Cultiway.Core.Skills;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;

namespace Cultiway.Content.Skills;

public sealed partial class ConstructedAbilityService
{
    /// <summary>同一部位在确认轮保存真实牵引与显示资源。</summary>
    private sealed class VortexInstance
    {
        /// <summary>仍属世界的真实材料及临时牵引关系。</summary>
        internal WorldVortexRun Motion;
        /// <summary>只为当前施放创建的三个厚面。</summary>
        internal MaterialVortexView View;
    }

    /// <summary>在共同世界材料和交锋确认后推进部位，阶段结束立即解除关系。</summary>
    /// <param name="running">已经接受的施放。</param><param name="clock">已经确认的世界时间。</param>
    private static void ConfirmMaterialVortices(RunningCast running, double clock)
    {
        int[] nodes = running.Cast.Plan.MaterialVortexNodes;
        if (nodes.Length == 0 || running.Practice) return;
        long generation = worldGeneration;
        for (int i = 0; i < nodes.Length; i++)
        {
            int node = nodes[i];
            VortexInstance vortex = running.Vortices?[i];
            var owner = running.Owner(node);
            if (!owner.IsLive || !running.Cast.TryMaterialVortex(node, out var centre, out _, out bool pulling) || !pulling)
            {
                vortex?.Motion?.Release();
                continue;
            }
            running.Vortices ??= new VortexInstance[nodes.Length];
            vortex ??= running.Vortices[i] = new VortexInstance();
            vortex.Motion ??= new WorldVortexRun();
            WorldMaterialService.DriveVortex(vortex.Motion, running.Cast.Plan.Nodes[node].MaterialVortex,
                owner.Id, centre, clock, running.Id, running.Cast.Plan.Id);
            if (generation != worldGeneration) return;
        }
    }

    /// <summary>以同一部位中心及开合时段绘制外唇、暗腔和转向内部的厚面。</summary>
    /// <param name="running">已确认的施放。</param>
    private void AppendMaterialVortices(RunningCast running)
    {
        int[] nodes = running.Cast.Plan.MaterialVortexNodes;
        for (int i = 0; i < nodes.Length; i++)
        {
            int node = nodes[i];
            if (!running.Cast.TryMaterialVortex(node, out var centre, out var phase, out _) || phase.x < .001f) continue;
            var form = running.Cast.Plan.Nodes[node];
            CompiledMaterialVortex rule = form.MaterialVortex;
            running.Vortices ??= new VortexInstance[nodes.Length];
            var vortex = running.Vortices[i] ??= new VortexInstance();
            MaterialVortexView view = vortex.View ??= new MaterialVortexView(rule);
            float age = running.Cast.Age;
            var scale = new Vector3(rule.Radius, rule.Radius, rule.Depth);
            var outer = Matrix4x4.TRS(centre, Quaternion.Euler(0f, 0f, age * -17f), scale * phase.x);
            visuals.Add(new FormVisualInstance(form.Resource, view.Chamber, outer,
                new Color(.19f, .105f, .29f, 1f), terrain, "rift_core"));
            visuals.Add(new FormVisualInstance(form.Resource, view.Rim, outer,
                new Color(.57f, .30f, .74f, 1f), terrain, "rift_lip"));
            if (phase.y < .001f) continue;
            Vector3 throat = centre - Vector3.forward * (rule.Depth * phase.x);
            var inner = Matrix4x4.TRS(throat, Quaternion.Euler(0f, 0f, age * 48f), scale * phase.y);
            visuals.Add(new FormVisualInstance(form.Resource, view.Throat, inner,
                new Color(.40f, .26f, .65f, 1f), terrain, "rift_lip"));
        }
    }

    /// <summary>取消或换世界时归还实际材料控制，回收三个临时显示面。</summary>
    /// <param name="running">不再继续的施放。</param>
    private static void ReleaseMaterialVortices(RunningCast running)
    {
        if (running.Vortices == null) return;
        foreach (var vortex in running.Vortices)
        {
            vortex?.Motion?.Release();
            vortex?.View?.Dispose();
        }
        running.Vortices = null;
    }
}
