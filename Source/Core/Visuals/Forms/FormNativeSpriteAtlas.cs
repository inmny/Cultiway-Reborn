using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cultiway.Core.Visuals.Forms;

/// <summary>专用原版着色器先生成原画面，再以批量平面参与共同深度，保留风动和变色。</summary>
internal sealed class FormNativeSpriteAtlas
{
    /// <summary>一个专用精灵的原投影区域和图集区域。</summary>
    private struct Stamp
    {
        /// <summary>保留原版着色器及实际参数。</summary>
        internal SpriteRenderer Renderer;
        /// <summary>主画面中的完整像素区域。</summary>
        internal Rect Screen;
        /// <summary>本帧图集中的区域。</summary>
        internal Rect Tile;
        /// <summary>原版精灵的实际地面位置。</summary>
        internal float Ground;
    }

    /// <summary>一段连续的透明图集物件。</summary>
    internal readonly struct Run
    {
        /// <summary>下一段共同排序项。</summary>
        internal readonly int End;
        /// <summary>该段的固定索引区域。</summary>
        internal readonly int Submesh;
        /// <summary>记录本帧已确定的批次。</summary>
        /// <param name="end">本批次之后的共同排序项。</param>
        /// <param name="submesh">本批次索引区域。</param>
        internal Run(int end, int submesh) { End = end; Submesh = submesh; }
    }

    /// <summary>复用到显示峰值的资料。</summary>
    private readonly List<Stamp> stamps = new();
    /// <summary>合并的四角平面，不复制原版或形体网格。</summary>
    private readonly List<Vector3> vertices = new();
    /// <summary>各平面在本帧图集中的坐标。</summary>
    private readonly List<Vector2> uv = new();
    /// <summary>每顶点携带所属精灵的地面坐标。</summary>
    private readonly List<Color> ground = new();
    /// <summary>实体与透明各批次的固定索引。</summary>
    private readonly List<int> indices = new();
    /// <summary>所有批次的范围。</summary>
    private readonly List<SubMeshDescriptor> ranges = new();
    /// <summary>透明排序中批次开始处对应的索引区域。</summary>
    internal readonly Dictionary<int, Run> Runs = new();
    /// <summary>当前相机拥有的原画面图集。</summary>
    private RenderTexture atlas;
    /// <summary>复用的批量平面。</summary>
    private Mesh mesh;
    /// <summary>图集纹理只作用于本相机。</summary>
    private readonly MaterialPropertyBlock properties = new();

    /// <summary>开始收集本帧，不每帧重建原生对象。</summary>
    internal void Begin() => stamps.Clear();

    /// <summary>记录原版着色器的实际对象和覆盖区域。</summary>
    /// <param name="renderer">原版精灵。</param>
    /// <param name="camera">实际观察相机。</param>
    internal int Add(SpriteRenderer renderer, Camera camera)
    {
        Bounds bounds = renderer.bounds;
        bounds.Expand(new Vector3(bounds.size.x * .2f, bounds.size.y * .2f, 0));
        Vector3 a = camera.WorldToScreenPoint(bounds.min), b = camera.WorldToScreenPoint(bounds.max);
        Rect viewport = camera.pixelRect;
        float x = Mathf.Max(viewport.xMin, Mathf.Floor(a.x) - 2), y = Mathf.Max(viewport.yMin, Mathf.Floor(a.y) - 2);
        float right = Mathf.Min(viewport.xMax, Mathf.Ceil(b.x) + 2), top = Mathf.Min(viewport.yMax, Mathf.Ceil(b.y) + 2);
        int index = stamps.Count;
        Vector3 position = renderer.transform.position;
        stamps.Add(new Stamp { Renderer = renderer, Screen = Rect.MinMaxRect(x, y, right, top), Ground = position.y - position.z });
        return index;
    }

    /// <summary>按当前像素需求打包，超出平台纹理面积时整体降低采样密度而不丢对象。</summary>
    /// <param name="camera">实际相机。</param>
    /// <param name="order">已经完成共同排序的透明项。</param>
    internal void Prepare(Camera camera, List<FormNativeScenePass.Entry> order)
    {
        Runs.Clear(); if (stamps.Count == 0) return;
        int limit = Mathf.Min(4096, SystemInfo.maxTextureSize);
        int width = Mathf.Min(limit, Mathf.NextPowerOfTwo(Mathf.Max(1024, camera.pixelWidth)));
        float density = 1f; int height;
        while (true)
        {
            int x = 0, y = 0, row = 0;
            for (int i = 0; i < stamps.Count; i++)
            {
                Stamp stamp = stamps[i];
                int w = Mathf.Clamp(Mathf.CeilToInt(stamp.Screen.width * density), 1, width);
                int h = Mathf.Max(1, Mathf.CeilToInt(stamp.Screen.height * density));
                if (x + w > width) { y += row; x = 0; row = 0; }
                stamp.Tile = new Rect(x, y, w, h); stamps[i] = stamp;
                x += w; row = Mathf.Max(row, h);
            }
            height = Mathf.NextPowerOfTwo(Mathf.Max(1, y + row));
            if (height <= limit) break;
            density *= .5f;
        }
        if (atlas == null || atlas.width != width || atlas.height < height)
        {
            if (atlas != null) { atlas.Release(); UnityEngine.Object.Destroy(atlas); }
            atlas = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
                { name = "Cultiway.NativeSpriteAtlas", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            atlas.Create();
        }
        vertices.Clear(); uv.Clear(); ground.Clear(); indices.Clear(); ranges.Clear();
        foreach (Stamp stamp in stamps)
        {
            Rect area = stamp.Screen, tile = stamp.Tile;
            for (int corner = 0; corner < 4; corner++)
            {
                bool right = corner == 1 || corner == 2, top = corner >= 2;
                Vector3 point = camera.ScreenToWorldPoint(new Vector3(right ? area.xMax : area.xMin, top ? area.yMax : area.yMin, 0));
                point.z = 0; vertices.Add(point);
                uv.Add(new Vector2((right ? tile.xMax : tile.xMin) / atlas.width, (top ? tile.yMax : tile.yMin) / atlas.height));
                ground.Add(new Color(stamp.Ground, 0, 0, 1));
            }
        }
        for (int i = 0; i < stamps.Count; i++) AddIndices(i);
        ranges.Add(new SubMeshDescriptor(0, indices.Count, MeshTopology.Triangles));
        for (int i = 0; i < order.Count;)
        {
            if (!order[i].Stamp.HasValue) { i++; continue; }
            int first = i, begin = indices.Count;
            while (i < order.Count && order[i].Stamp.HasValue) { AddIndices(order[i].Stamp.Value); i++; }
            Runs.Add(first, new Run(i, ranges.Count));
            ranges.Add(new SubMeshDescriptor(begin, indices.Count - begin, MeshTopology.Triangles));
        }
        mesh ??= new Mesh { name = "Cultiway.NativeSpriteQuads" };
        mesh.Clear(); mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetColors(ground);
        mesh.SetIndexBufferParams(indices.Count, IndexFormat.UInt32);
        mesh.SetIndexBufferData(indices, 0, 0, indices.Count);
        mesh.subMeshCount = ranges.Count;
        for (int i = 0; i < ranges.Count; i++) mesh.SetSubMesh(i, ranges[i]);
        mesh.RecalculateBounds();
        properties.Clear(); properties.SetTexture("_MainTex", atlas);
    }

    /// <summary>把一个平面的六个索引追加到当前批次。</summary>
    /// <param name="stamp">图集物件编号。</param>
    private void AddIndices(int stamp)
    {
        int offset = stamp * 4;
        indices.Add(offset); indices.Add(offset + 2); indices.Add(offset + 1);
        indices.Add(offset); indices.Add(offset + 3); indices.Add(offset + 2);
    }

    /// <summary>用原材质绘制图集，原变形、贴图、颜色和时间参数继续生效。</summary>
    /// <param name="commands">当前相机命令。</param>
    /// <param name="camera">实际观察相机。</param>
    internal void Capture(CommandBuffer commands, Camera camera)
    {
        if (stamps.Count == 0) return;
        commands.SetRenderTarget(atlas); commands.ClearRenderTarget(false, true, Color.clear);
        Rect viewport = camera.pixelRect;
        foreach (Stamp stamp in stamps)
        {
            Rect area = stamp.Screen;
            Matrix4x4 crop = Matrix4x4.identity;
            crop.m00 = viewport.width / area.width; crop.m11 = viewport.height / area.height;
            crop.m03 = (viewport.width - 2 * (area.xMin - viewport.xMin) - area.width) / area.width;
            crop.m13 = (viewport.height - 2 * (area.yMin - viewport.yMin) - area.height) / area.height;
            commands.SetViewport(stamp.Tile);
            commands.SetViewProjectionMatrices(camera.worldToCameraMatrix, GL.GetGPUProjectionMatrix(crop * camera.projectionMatrix, true));
            commands.SetGlobalVector("_ScreenParams", new Vector4(stamp.Tile.width, stamp.Tile.height, 1 + 1 / stamp.Tile.width, 1 + 1 / stamp.Tile.height));
            commands.DrawRenderer(stamp.Renderer, stamp.Renderer.sharedMaterial, 0, 0);
        }
        commands.SetRenderTarget(BuiltinRenderTextureType.CameraTarget); commands.SetViewport(viewport);
        commands.SetViewProjectionMatrices(camera.worldToCameraMatrix, GL.GetGPUProjectionMatrix(camera.projectionMatrix, camera.targetTexture != null));
        commands.SetGlobalVector("_ScreenParams", new Vector4(camera.pixelWidth, camera.pixelHeight, 1 + 1f / camera.pixelWidth, 1 + 1f / camera.pixelHeight));
    }

    /// <summary>以已固定的索引区域绘制，不在命令录制过程中改动几何。</summary>
    /// <param name="commands">当前命令。</param>
    /// <param name="material">同版本的共同精灵表面。</param>
    /// <param name="submesh">本帧固定批次。</param>
    /// <param name="pass">实体深度、实体颜色或透明颜色。</param>
    internal void Draw(CommandBuffer commands, Material material, int submesh, int pass)
    {
        if (stamps.Count > 0) commands.DrawMesh(mesh, Matrix4x4.identity, material, submesh, pass, properties);
    }

    /// <summary>世界或相机结束时释放图集和批量网格。</summary>
    internal void Clear()
    {
        stamps.Clear(); Runs.Clear();
        if (atlas != null) { atlas.Release(); UnityEngine.Object.Destroy(atlas); atlas = null; }
        if (mesh != null) { UnityEngine.Object.Destroy(mesh); mesh = null; }
        properties.Clear();
    }
}
