using System;
using System.Collections.Generic;
using System.Reflection;
using Cultiway.Core.Skills;
using HarmonyLib;
using UnityEngine;

namespace Cultiway.Content.Compatibility.Worldfall;

/// <summary>携带身体直接查询同一环境模型的三角面，不按站立高度切成通行轮廓。</summary>
internal sealed partial class WorldfallBodyObstacles
{
    /// <summary>固定模型数字只采集一次，换世界清理。</summary>
    private readonly Dictionary<object, BodySurfaceMesh> collisionModels = new();

    /// <summary>实际模型和基础阻挡的固定字段。</summary>
    private FieldInfo collisionParts, collisionPoints, collisionTriangles, collisionBox, collisionRadius;

    /// <summary>本轮已经采集的活动范围，超出时补采集实际记录。</summary>
    private Rect capturedArea;

    /// <summary>绑定实际网格和原基础阻挡，不按物品名称猜形状。</summary>
    /// <param name="assembly">已核对的外部程序集。</param><param name="collider">原基础阻挡类型。</param>
    private void BindMeshes(Assembly assembly, Type collider)
    {
        collisionParts = AccessTools.Field(assembly.GetType("FirstPerson.Core.MeshModel", true), "Parts");
        Type part = assembly.GetType("FirstPerson.Core.MeshPart", true);
        collisionPoints = AccessTools.Field(part, "Pos");
        collisionTriangles = AccessTools.Field(part, "Tri");
        collisionBox = AccessTools.Field(collider, "Box");
        collisionRadius = AccessTools.Field(collider, "Radius");
    }

    /// <summary>每座实际摆放只在变更时变换几何；发布值不持有建筑或反射对象。</summary>
    /// <param name="bounds">真实携带身体扫过的范围。</param><param name="terrain">共同地面。</param>
    private BodySurfaceObstacle[] CaptureMeshes(Bounds bounds, AbilityHeightField terrain)
    {
        var area = new Rect(bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y);
        if (!capturedArea.Contains(area.min) || !capturedArea.Contains(area.max)) Capture(area, terrain);
        var result = new List<BodySurfaceObstacle>();
        foreach (Entry entry in active)
        {
            if (!entry.Owner.isAlive() || entry.Owner.id != entry.Id || entry.Owner.chopped || !entry.Bounds.Overlaps(area)) continue;
            object source = entry.Model ?? entry.Fallback;
            float angle = entry.Heading * Mathf.Rad2Deg + 90f;
            Matrix4x4 matrix = entry.Model == null ? Matrix4x4.identity : Matrix4x4.TRS(new Vector3(entry.X, entry.Y, entry.Z),
                Quaternion.AngleAxis(angle, Vector3.forward), new Vector3(entry.HorizontalScale * (entry.Mirror ? -1f : 1f), entry.HorizontalScale, entry.Scale));
            if (entry.CollisionSource != source || entry.CollisionMatrix != matrix)
            {
                if (entry.Model != null)
                {
                    if (!collisionModels.TryGetValue(entry.Model, out BodySurfaceMesh mesh))
                    {
                        var points = new List<Vector3>(); var triangles = new List<int>();
                        foreach (object part in (Array)collisionParts.GetValue(entry.Model))
                        {
                            int start = points.Count;
                            var raw = (float[])collisionPoints.GetValue(part);
                            for (int i = 0; i < raw.Length; i += 3) points.Add(new Vector3(raw[i], raw[i + 1], raw[i + 2]));
                            foreach (int vertex in (int[])collisionTriangles.GetValue(part)) triangles.Add(start + vertex);
                        }
                        collisionModels.Add(entry.Model, mesh = new BodySurfaceMesh(points.ToArray(), triangles.ToArray()));
                    }
                    entry.CollisionMesh = mesh.Transformed(matrix);
                }
                else if ((bool)collisionBox.GetValue(entry.Fallback)) entry.CollisionMesh = BodySurfaceMesh.Box(entry.Bounds, entry.Z, entry.Top);
                else
                {
                    float radius = (float)collisionRadius.GetValue(entry.Fallback);
                    int count = Math.Max(8, Mathf.CeilToInt(Mathf.PI * Mathf.Sqrt(radius / .00002f)));
                    var outline = new Vector2[count];
                    for (int i = 0; i < count; i++)
                    {
                        float at = i * Mathf.PI * 2f / count;
                        outline[i] = new Vector2(entry.X + Mathf.Cos(at) * radius, entry.Y + Mathf.Sin(at) * radius);
                    }
                    entry.CollisionMesh = BodySurfaceMesh.Prism(outline, entry.Z, entry.Top);
                }
                entry.CollisionSource = source;
                entry.CollisionMatrix = matrix;
            }
            result.Add(new BodySurfaceObstacle(entry.Id, entry.CollisionMesh, entry.Model == null ? "ObstacleContact" : "ModelContact"));
        }
        return result.ToArray();
    }
}
