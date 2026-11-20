using System.Collections.Generic;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;

namespace Cultiway.Core.Skills;

public sealed partial class AbilityCast
{
    /// <summary>种核先成形，裂口只在实际吸收位置出现，释放时与光束共用真实方向。</summary>
    /// <param name="node">负责吸收的明壳。</param>
    /// <param name="output">本步已确认的画面。</param>
    private void AppendSolarReservoir(int node, List<FormVisualInstance> output)
    {
        CompiledForm form = Plan.Nodes[node];
        if (form.Resource.Description.Id != "solar_aegis") return;
        ref NodeState shell = ref nodes[node];
        int index = form.AbsorbedForceOutlet;
        ref NodeState outlet = ref nodes[index];
        FormResource resource = form.Resource;
        var generation = resource.Generation;
        float age = NodeAge(node), showShell = form.Presence.x;
        if (shell.Exists && age >= 0f && age < showShell + .42f)
        {
            float gather = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / Mathf.Max(.01f, showShell)));
            float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((age - showShell) / .42f));
            Vector3 centre = (Vector3)shell.Transform.GetColumn(3) + Vector3.forward * (.68f * form.Scale * gather);
            float span = (.30f + gather * .15f) * form.Scale;
            var pose = Matrix4x4.TRS(centre, Quaternion.Euler(age * 30f, age * -43f, age * 65f),
                Vector3.one * span);
            output.Add(new FormVisualInstance(resource, generation.SolarGrains, pose,
                new Color(1f, .67f, .22f, fade), terrain, "solar_gold"));
            output.Add(new FormVisualInstance(resource, generation.SolarGrains,
                pose * Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(37f, 56f, 11f), Vector3.one * .56f),
                new Color(1f, .80f, .32f, fade * gather), terrain, "solar_kernel"));
        }

        if (!outlet.Exists || !outlet.EventPlaced) return;
        CompiledForm discharge = Plan.Nodes[index];
        float phase = Age - outlet.BirthAt;
        float delay = discharge.Activation.Delay;
        float open = Mathf.SmoothStep(0f, 1f, delay > 0f ? Mathf.Clamp01(1f + phase / delay) : 1f);
        float fadeLips = Mathf.Clamp01(1f - phase / .48f);
        if (fadeLips <= 0f) return;
        float intensity = Mathf.Clamp01(outlet.InitialForce / Mathf.Max(.0001f, shell.InitialForce));
        float width = discharge.Scale * (.13f + open * .16f);
        var rupture = Matrix4x4.TRS(outlet.EventPosition, outlet.EventRotation,
            new Vector3(width * (1f + Mathf.Max(phase, 0f) * 2f), width * 1.3f, width));
        output.Add(new FormVisualInstance(resource, generation.SolarLips, rupture,
            new Color(1f, .56f + intensity * .34f, .23f + intensity * .18f, fadeLips * (.42f + open * .58f)),
            terrain, "solar_gold"));
        if (outlet.Started)
            AppendSpentSolarShell(resource, shell.Transform, outlet.EventRotation, phase, output);
    }

    /// <summary>停止拦截的原壳从裂口两侧翻开，厚甲片在同一位置碎退。</summary>
    /// <param name="resource">明壳的实际面片资源。</param>
    /// <param name="shell">最后一帧实际壳姿态。</param>
    /// <param name="vent">首个裂口的外向坐标。</param>
    /// <param name="phase">释放后经过的世界秒数。</param>
    /// <param name="output">本步提交的显示。</param>
    private void AppendSpentSolarShell(FormResource resource, Matrix4x4 shell, Quaternion vent, float phase,
        List<FormVisualInstance> output)
    {
        const int plates = 8;
        float retreat = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(phase / .48f));
        Vector3 centre = shell.GetColumn(3);
        Vector3 outward = vent * Vector3.forward;
        Vector3 side = vent * Vector3.right;
        float radius = shell.lossyScale.x;
        for (int panel = 0; panel < plates; panel++)
        {
            float angle = (panel + .5f) * Mathf.PI * 2f / plates;
            Vector3 normal = shell.MultiplyVector(new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f)).normalized;
            float front = Mathf.Max(0f, Vector3.Dot(normal, outward));
            float flank = Vector3.Dot(normal, side) < 0f ? -1f : 1f;
            Vector3 pivot = centre + normal * (radius * 1.49f);
            Vector3 drift = (normal * .53f + side * (flank * front * .47f) + Vector3.forward * .46f) *
                            (radius * retreat);
            Quaternion tilt = Quaternion.AngleAxis((32f + front * 28f) * retreat,
                Vector3.Cross(Vector3.forward, normal));
            var pose = Matrix4x4.TRS(pivot + drift, tilt, Vector3.one) * Matrix4x4.Translate(-pivot) * shell;
            output.Add(new FormVisualInstance(resource, resource.Meshes[panel], pose,
                new Color(1f, .62f, .18f, (1f - retreat) * .65f), terrain, "aegis_solar"));
        }
    }
}
