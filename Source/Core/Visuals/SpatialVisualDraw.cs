using System.Collections.Generic;
using UnityEngine;

namespace Cultiway.Core.Visuals;

/// <summary>将可组合的实体、连接和波纹追加到同一批快照，不产生玩法作用。</summary>
internal static class SpatialVisualDraw
{
    /// <summary>追加一个已有固定模型。</summary>
    /// <param name="output">当前帧快照。</param><param name="model">固定几何。</param><param name="position">地图位置与高度。</param>
    /// <param name="rotation">真实朝向。</param><param name="scale">三个方向的尺寸。</param><param name="color">颜色与透明度。</param>
    /// <param name="emissive">是否保持能量本色。</param><param name="groundAnchor">身体共用承托点。</param>
    /// <param name="additive">是否叠加亮度；外焰等薄层可只保留颜色。</param>
    /// <param name="surface">明确指定本次组合使用的材料。</param>
    internal static void Mesh(List<SpatialVisualMeshInstance> output, SpatialVisualMesh model, Vector3 position,
        Quaternion rotation, Vector3 scale, Color color, bool emissive, Vector2? groundAnchor = null,bool additive=true,SpatialSurface? surface=null) =>
        output.Add(new SpatialVisualMeshInstance(model, position, rotation, scale, color, true, emissive, groundAnchor,additive:additive,surface:surface));

    /// <summary>连接实际两端，保持轴线和宽度。</summary>
    /// <param name="output">快照。</param><param name="start">起点。</param><param name="end">终点。</param>
    /// <param name="width">世界宽度。</param><param name="color">颜色。</param><param name="emissive">是否发光。</param>
    internal static void Rod(List<SpatialVisualMeshInstance> output, Vector3 start, Vector3 end, float width, Color color, bool emissive)
    {
        Vector3 delta = end - start;
        if (delta.sqrMagnitude < .000001f) return;
        Mesh(output, SpatialVisualGeometry.Rod, start, Quaternion.FromToRotation(Vector3.forward, delta),
            new Vector3(width, width, delta.magnitude), color, emissive);
    }

    /// <summary>在指定平面追加宽而柔和的气浪，显示边界不依赖细线。</summary>
    /// <param name="output">快照。</param><param name="center">中心。</param><param name="radius">世界半径。</param>
    /// <param name="color">颜色。</param><param name="plane">环平面的姿态。</param><param name="groundAnchor">共同承托点。</param>
    internal static void Ring(List<SpatialVisualMeshInstance> output, Vector3 center, float radius, Color color,
        Quaternion plane, Vector2? groundAnchor = null) =>
        Mesh(output, SpatialVisualGeometry.Ring, center, plane, Vector3.one * radius, color, true, groundAnchor, additive:false);
}
