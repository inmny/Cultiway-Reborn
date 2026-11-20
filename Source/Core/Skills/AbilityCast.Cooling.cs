using System.Collections.Generic;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>各股熔流仍是独立部位，只有实际铺开的交汇处才交换热量和推力。</summary>
public sealed partial class AbilityCast
{
    /// <summary>只登记本次施放中明确能凝结的流散部位。</summary>
    private readonly int[] coolingNodes;

    /// <summary>先让各部位独立流动和散热，再让同时存在的冷热层产生局部破口。</summary>
    /// <param name="step">已经推进的共同世界秒。</param>
    private void AdvanceCoolingContacts(float step)
    {
        foreach (int older in coolingNodes)
        {
            if (!nodes[older].Exists || !flows[older].Started || nodes[older].ShapeHeld || flows[older].CrustVolume <= 0d) continue;
            float began = nodes[older].StartedAt + nodes[older].FlowAt;
            foreach (int incoming in coolingNodes)
                if (incoming != older && nodes[incoming].Exists && flows[incoming].Started && !nodes[incoming].ShapeHeld &&
                    nodes[incoming].StartedAt + nodes[incoming].FlowAt > began && flows[older].BreakWith(flows[incoming], step))
                {
                    FollowFlow(older);
                    FollowFlow(incoming);
                }
        }
    }

    /// <summary>发布已经冷凝的真实厚度，并在仍有余热的实际液面上留下有限余焰。</summary>
    /// <param name="node">实际熔流部位。</param><param name="output">两种视角共同读取的快照。</param>
    private void AppendCoolingVisuals(int node, List<FormVisualInstance> output)
    {
        AbilityFlow flow = flows[node];
        if (flow.CrustVolume > 0d)
            output.Add(new FormVisualInstance(Plan.Nodes[node].Resource, Matrix4x4.identity, surface: "iron_crust",
                matter: AbilityMatters.Resolve("iron", "solid", "Flow/Cooling"), terrain: terrain,
                layer: new FormLayerFrame(flow, owner: flow.CrustOwner, crust: true)));
        int closest = -1;
        float peak = 0f;
        for (int i = 0; i < flow.Water.Length; i++)
        {
            float depth = flow.Water[i].x;
            float value = flow.Glow(i) * Mathf.Clamp01(depth * 4f);
            if (value <= peak) continue;
            peak = value;
            closest = i;
        }

        if (closest < 0 || peak < .045f) return;
        int n = flow.Settings.Cells;
        float strength = Mathf.InverseLerp(.045f, .6f, peak);
        float width = flow.Cell * Mathf.Lerp(.3f, 1.2f, strength);
        Vector3 point = new(flow.Origin.x + (closest % n + .5f) * flow.Cell,
            flow.Origin.y + (closest / n + .5f) * flow.Cell, flow.FlowBed(closest) + flow.Water[closest].x);
        output.Add(new FormVisualInstance(FormResources.Load("fire_body"),
            Matrix4x4.TRS(point, Quaternion.identity, new Vector3(width, width, width * 1.45f)),
            tint: new Color(1f, Mathf.Lerp(.20f, .45f, strength), .055f, strength * .66f), terrain: terrain));
    }
}
