using UnityEngine;

namespace Cultiway.Core.Skills;

public sealed partial class AbilityCast
{
    /// <summary>从真实部位读出同一中心与时段，跨帧画面不再自行选择位置。</summary>
    /// <param name="node">真正承载深口的部位。</param>
    /// <param name="centre">部位的当前世界中心。</param>
    /// <param name="phase">外沿和内圈的展开量。</param>
    /// <param name="pulling">实际牵引时段是否有效。</param>
    internal bool TryMaterialVortex(int node, out Vector3 centre, out Vector2 phase, out bool pulling)
    {
        ref NodeState state = ref nodes[node];
        CompiledForm form = Plan.Nodes[node];
        centre = state.Transform.GetColumn(3);
        phase = default;
        pulling = false;
        if (Finished || !state.Exists || !state.Started || state.Leaving || state.Recovering || !state.Visible) return false;
        float age = Age - state.StartedAt;
        phase = form.MaterialVortex.Extents(age, form.Presence, form.Active);
        pulling = age >= form.Active.x && age < form.Active.y;
        return true;
    }
}
