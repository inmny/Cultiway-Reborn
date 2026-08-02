using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.Components.AnimOverwrite;

/// <summary>覆盖技能动画生命周期选中的运行时帧，用于装备贴图等动态视觉来源。</summary>
public struct AnimRuntimeFrames : IComponent
{
    /// <summary>本次执行体实际显示的帧；为空时沿用技能资产声明的动画。</summary>
    public Sprite[] Value;
}
