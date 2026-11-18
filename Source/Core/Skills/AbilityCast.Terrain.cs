using UnityEngine;
using Cultiway.Core.Visuals.Forms;

namespace Cultiway.Core.Skills;

/// <summary>形体和作用区使用绝对地图高度，显示只负责各视角的投影。</summary>
public sealed partial class AbilityCast
{
    /// <summary>本轮主线程已经冻结的地表与预测前态。</summary>
    private AbilityHeightField terrain, savedTerrain;
    /// <summary>只有可贴地曲面才保存未挠曲的基姿态及预测前态。</summary>
    private readonly FormPose[] groundBase, savedGroundBase;

    /// <summary>在逻辑轮次开始前替换地表，计算期间保持不变。</summary>
    /// <param name="value">新采集的只读地表；空值为原版零平面。</param>
    internal void UpdateTerrain(AbilityHeightField value) => terrain = value;

    /// <summary>相同数值地表，原版零平面不额外分配数组。</summary>
    /// <param name="point">实际地图位置。</param>
    private float TerrainHeight(Vector3 point) => terrain?.Sample(point.x, point.y) ?? 0f;

    /// <summary>先还原上次基姿态，防止将地表偏移逐帧累加；冻结及停止保留当前形状。</summary>
    /// <param name="node">本次采样的部位。</param>
    private void RestoreGroundBase(int node)
    {
        ref NodeState state = ref nodes[node];
        if (state.GroundDeformed && !state.ShapeHeld && !state.MotionStopped && state.Flying &&
            state.Flight.Ground == ConstructionGroundResponse.Follow)
        { groundBase[node].CopyTo(poses[node]); state.GroundDeformed = false; }
    }

    /// <summary>沿地形推进时以真实底部承托，柔性资源另用共同控制点挠曲。</summary>
    /// <param name="node">已经移动到本轮水平位置的部位。</param>
    private void FollowGround(int node)
    {
        ref NodeState state = ref nodes[node]; CompiledForm form = Plan.Nodes[node];
        bool deform = form.Resource.Description.ConformToGround && !state.ShapeHeld && !state.MotionStopped;
        float lift = deform ? TerrainHeight(state.Position) - MinimumHeight(node, state.Transform, poses[node], false) :
            -MinimumHeight(node, state.Transform, poses[node]);
        if (float.IsNegativeInfinity(lift)) return;
        state.Position.z += lift; state.Transform.m23 += lift;
        if (deform)
        {
            poses[node].CopyTo(groundBase[node]);
            form.Resource.Rig.Conform(poses[node], state.Transform, terrain);
            state.GroundDeformed = true;
        }
        if (state.LastStep > .00001f)
            state.Velocity.z = (state.Position.z - state.PreviousTransform.MultiplyPoint3x4(state.Pivot).z) / state.LastStep;
        else state.Velocity.z = 0;
        state.Acceleration.z = 0;
    }
}
