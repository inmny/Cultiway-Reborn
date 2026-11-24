using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.Components;

/// <summary>
/// 记录实体上一帧的位置，供共用扫掠检查变动路径。
/// </summary>
public struct PrevPosition : IComponent
{
    /// <summary>上次轨迹更新前的地图位置。</summary>
    public Vector2 Value;

    /// <summary>与平面位置同时记录的相对地表高度。</summary>
    public float Height;
}
