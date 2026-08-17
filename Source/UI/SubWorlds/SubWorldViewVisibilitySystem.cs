using System.Collections.Generic;
using UnityEngine;

namespace Cultiway.UI.SubWorlds;

/// <summary>按主相机视野与 WorldView 的相交关系启停视觉同步。</summary>
internal sealed class SubWorldViewVisibilitySystem
{
    internal void Update(Camera camera, IReadOnlyDictionary<long, SubWorldWorldView> worldViews)
    {
        Rect visibleBounds = GetVisibleBounds(camera);
        foreach (SubWorldWorldView worldView in worldViews.Values)
        {
            bool intersects = visibleBounds.Overlaps(worldView.WorldBounds);
            bool visibilityChanged = worldView.SetVisible(intersects);
            if (intersects || visibilityChanged) worldView.SyncVisibleState();
        }
    }

    private static Rect GetVisibleBounds(Camera camera)
    {
        float z = camera.nearClipPlane;
        Vector3 bottomLeft = camera.ViewportToWorldPoint(new Vector3(0f, 0f, z));
        Vector3 topLeft = camera.ViewportToWorldPoint(new Vector3(0f, 1f, z));
        Vector3 bottomRight = camera.ViewportToWorldPoint(new Vector3(1f, 0f, z));
        Vector3 topRight = camera.ViewportToWorldPoint(new Vector3(1f, 1f, z));
        float xMin = Mathf.Min(bottomLeft.x, topLeft.x, bottomRight.x, topRight.x);
        float yMin = Mathf.Min(bottomLeft.y, topLeft.y, bottomRight.y, topRight.y);
        float xMax = Mathf.Max(bottomLeft.x, topLeft.x, bottomRight.x, topRight.x);
        float yMax = Mathf.Max(bottomLeft.y, topLeft.y, bottomRight.y, topRight.y);
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }
}
