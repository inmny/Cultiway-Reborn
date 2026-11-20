using System.Collections.Generic;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;

namespace Cultiway.Core.Skills;

public sealed partial class AbilityCast
{
    /// <summary>读取仍在场的实际器口，已关闭或退出的部位不再接入原物。</summary>
    internal bool TryWeaponChamber(int node, out WeaponChamberFrame frame)
    {
        frame = default;
        ref NodeState state = ref nodes[node];
        var form = Plan.Nodes[node];
        float age = NodeAge(node);
        if (Finished || !state.Exists || !state.Started || !state.Visible || state.Leaving ||
            state.Recovering || age < form.Presence.x || age >= form.Presence.y) return false;
        frame = new WeaponChamberFrame(state.Transform, form.WeaponChamber, age, form.Active);
        return true;
    }

    /// <summary>器腹与厚口沿同一数值开口成形；蓄积时内侧夹页合拢。</summary>
    private void AppendWeaponChamber(int node, List<FormVisualInstance> output)
    {
        if (!TryWeaponChamber(node, out var frame)) return;
        var form = Plan.Nodes[node];
        float age = NodeAge(node), shut = WeaponChamberFrame.GatherEnd(form.Active);
        float phase = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(form.Presence.x, form.Active.x, age)) *
                      Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(form.Presence.y, form.Active.y, age));
        if (phase < .003f) return;
        var generation = form.Resource.Generation;
        var scale = new Vector3(frame.Radius, frame.Radius, frame.Depth) * phase;
        var pose = Matrix4x4.TRS(frame.Mouth, frame.Rotation, scale);
        output.Add(new FormVisualInstance(form.Resource, generation.WeaponChamberShell, pose,
            new Color(.93f, .73f, .48f, 1f), terrain, "furnace_bronze"));
        output.Add(new FormVisualInstance(form.Resource, generation.WeaponChamberLip, pose,
            new Color(.94f, .87f, .66f, 1f), terrain, "time_stamp_brass"));
        float closure = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(shut, shut + .14f, age)) *
                        Mathf.SmoothStep(0f, 1f, 1f - frame.ExitProgress);
        if (closure > .01f)
        {
            var hinge = Matrix4x4.Translate(Vector3.up) *
                        Matrix4x4.Rotate(Quaternion.Euler(84f * (1f - closure), 0f, 0f)) *
                        Matrix4x4.Translate(Vector3.down);
            var opposite = Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, 180f));
            var bronze = new Color(.68f, .79f, .72f, 1f);
            output.Add(new FormVisualInstance(form.Resource, generation.WeaponChamberJaw, pose * hinge,
                bronze, terrain, "metal"));
            output.Add(new FormVisualInstance(form.Resource, generation.WeaponChamberJaw, pose * opposite * hinge,
                bronze, terrain, "metal"));
        }
    }
}
