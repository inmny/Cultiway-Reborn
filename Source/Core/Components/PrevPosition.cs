using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.Components;

/// <summary>
/// 扫掠碰撞用：记录实体上一帧的平面位置，供 <see cref="SkillLibV3.Systems.LogicActorCollisionSystem"/> 做线段-瓦片相交检测。
/// </summary>
public struct PrevPosition : IComponent
{
    /// <summary>上次轨迹更新前的地图位置。</summary>
    public Vector2 Value;

    /// <summary>与平面位置同时记录的相对地表高度。</summary>
    public float Height;
}
