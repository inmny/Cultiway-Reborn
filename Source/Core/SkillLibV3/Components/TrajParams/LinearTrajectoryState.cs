using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3.Components.TrajParams;

/// <summary>
/// 光束和屏障首次解析后的固定几何，以及拉伸视觉所需的原始缩放。
/// </summary>
public struct LinearTrajectoryState : IComponent
{
    public bool Initialized;
    public Vector3 Start;
    public Vector3 End;
    public Vector3 BaseScale;
}
