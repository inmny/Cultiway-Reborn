using System;
using System.Collections.Generic;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>地表传播使用普通部位、共同作用和共同显示；路线不产生另一施放。</summary>
public sealed partial class AbilityCast
{
    /// <summary>只有实际声明传播才分配每部位运行及预测引用。</summary>
    private readonly AbilityPropagation[] propagations, savedPropagations;
    /// <summary>主线程在完整边界发布的只读地面。</summary>
    private PropagationGround propagationGround;
    /// <summary>本次预测开始的地面输入。</summary>
    private PropagationGround savedPropagationGround;
    /// <summary>每个已发布区域的稳定显示身份，不按显示帧建立新的缓存。</summary>
    private readonly Dictionary<(int Node, int X, int Y), object> propagationVisuals = new();

    /// <summary>更新本轮连通与高度，工作线程只读取这一份完整输入。</summary>
    /// <param name="value">真实地面快照。</param>
    internal void UpdatePropagationGround(PropagationGround value) => propagationGround = value;

    /// <summary>从真实事件位置或已有地面主体启动普通有限传播。</summary>
    /// <param name="action">真实操作身份。</param><param name="settings">已检查参数。</param>
    private bool StartPropagation(CompiledEventOperation action, CompiledPropagation settings)
    {
        int node = action.Node;
        if (propagations[node] != null) return false;
        ref NodeState state = ref nodes[node];
        Vector3 point = action.When == null ? state.Transform.GetColumn(3) : operations[action.Index].EventPoint;
        ReleaseNode(node);
        var wave = new AbilityPropagation(settings, propagationGround, point,
            state.Transform.MultiplyVector(Vector3.forward), MaximumScale(state.Transform), state.Force, Age);
        propagations[node] = wave;
        EndSurfaceChildren(node);
        state.Revision++;
        if (wave.Route.Length == 0) { EndNode(node); return false; }
        return true;
    }

    /// <summary>先确认这一步的地面和实际花费，再在自然结束之前通知新到达边界。</summary>
    /// <param name="node">当前传播来源。</param><param name="wave">运行中的真实路线。</param>
    private void AdvancePropagation(int node, AbilityPropagation wave)
    {
        int first = wave.End;
        float spent = wave.Advance(Age - wave.StartedAt, propagationGround);
        ConsumeForce(node, spent, wave, first);
    }

    /// <summary>只查看本次新到达的格子；没有订阅或已经越过最后阈值时不扫描路线。</summary>
    /// <param name="node">实际来源部位。</param><param name="wave">已经按路径顺序确认到达的地波。</param>
    /// <param name="first">本次新到达的第一个路线位置。</param>
    private void SignalGroundRoute(int node, AbilityPropagation wave, int first)
    {
        float[] thresholds = Plan.Nodes[node].GroundRouteThresholds;
        if (thresholds.Length == 0 || first == wave.End) return;
        ref NodeState state = ref nodes[node];
        int threshold = NextSizeIndex(thresholds, state.GroundRouteDistance, true);
        int end = wave.End;
        for (int i = first; i < end && threshold < thresholds.Length && state.Exists; i++)
        {
            var cell = wave.Route[i];
            if (cell.Distance < thresholds[threshold] || !wave.Reachable(i)) continue;
            float before = state.GroundRouteDistance;
            state.GroundRouteDistance = cell.Distance;
            var point = new Vector3(cell.X + .5f, cell.Y + .5f, propagationGround.Height(cell.X, cell.Y));
            SignalEvent(node, ConstructionChangeTrigger.GroundRouteDistance, point, before, cell.Distance);
            threshold = NextSizeIndex(thresholds, cell.Distance, true);
        }
    }

    /// <summary>命中所用的路程衰减，普通形体保持一。</summary>
    /// <param name="node">实际部位。</param><param name="shape">真实作用面编号。</param>
    internal float ContactAttenuation(int node, int shape) => node >= 0 && propagations?[node] is AbilityPropagation wave
        ? wave.Strength(shape - Plan.Nodes[node].Resource.Contacts.Length) : 1f;

    /// <summary>只提供当前或本步刚回落的真实波面柱，编号不随活动列表变化。</summary>
    /// <param name="node">普通部位。</param><param name="wave">同一数值过程。</param>
    /// <param name="output">共同接触缓冲。</param><param name="includeEnd">是否保留本步末段。</param>
    private void AppendPropagationContacts(int node, AbilityPropagation wave, List<AbilityCapsule> output, bool includeEnd)
    {
        NodeState state = nodes[node];
        float step = includeEnd ? state.LastStep : 0f;
        int first = wave.Lower(wave.Age - wave.Settings.Seconds - step);
        for (int i = first; i < wave.End; i++)
        {
            if (!wave.Reachable(i)) continue;
            var cell = wave.Route[i];
            float remaining = cell.At + wave.Settings.Seconds - wave.Age;
            if (remaining < 0f && !includeEnd) continue;
            float height = wave.Height(i), floor = propagationGround.Height(cell.X, cell.Y);
            var center = new Vector3(cell.X + .5f, cell.Y + .5f, floor + height * .5f);
            float until = remaining >= 0f ? 1f : step > 0f ? Mathf.Clamp01(1f + remaining / step) : 0f;
            output.Add(new AbilityCapsule(node, center, center, 0f, state.Matter.Id,
                Plan.Nodes[node].Resource.Contacts.Length + cell.X + cell.Y * wave.Width,
                state.Revision, state.Matter, new Vector3(.5f, .5f, height * .5f), until));
        }
    }

    /// <summary>按实际活动波面分区发布，未到达和已经回落的地图不建立显示网格。</summary>
    /// <param name="node">实际部位。</param><param name="wave">同一已完成数值过程。</param>
    /// <param name="output">共同显示集合。</param>
    private void AppendPropagationVisuals(int node, AbilityPropagation wave, List<FormVisualInstance> output)
    {
        const int size = 16;
        var regions = new Dictionary<(int X, int Y), float[]>();
        for (int i = wave.First; i < wave.End; i++)
        {
            float height = wave.Height(i);
            if (height <= .000001f) continue;
            var cell = wave.Route[i];
            var key = (cell.X / size, cell.Y / size);
            if (!regions.TryGetValue(key, out var values)) regions.Add(key, values = new float[size * size]);
            values[cell.X % size + cell.Y % size * size] = height;
        }
        var appearance = MatterAppearance(node);
        foreach (var pair in regions)
        {
            var key = (node, pair.Key.X, pair.Key.Y);
            if (!propagationVisuals.TryGetValue(key, out object owner)) propagationVisuals.Add(key, owner = new object());
            var origin = new Vector2(pair.Key.X * size, pair.Key.Y * size);
            var bed = new float[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++) bed[x + y * size] = propagationGround.Height((int)origin.x + x, (int)origin.y + y);
            var frame = new FormLayerFrame(owner, origin, size, pair.Value, bed);
            output.Add(new FormVisualInstance(Plan.Nodes[node].Resource, Matrix4x4.identity,
                surface: appearance.Surface, matterSurface: appearance.MatterSurface, matter: nodes[node].Matter,
                dye: Plan.Nodes[node].Dye, terrain: terrain, layer: frame));
        }
    }
}
