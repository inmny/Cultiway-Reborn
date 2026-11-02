using System.Collections.Generic;
using UnityEngine;

namespace Cultiway.Core.Visuals.Forms;

/// <summary>原版图片的连续变形；沿用原贴图与深度材质，只改变本次绘制的顶点。</summary>
public static class FormNativeSpriteDeformation
{
    /// <summary>本帧实际需要变形的图片，完成后移除已不再使用的实例。</summary>
    private static readonly Dictionary<SpriteRenderer, Pose> poses = new();

    /// <summary>回收上一帧已经结束的图片，不在遍历时修改字典。</summary>
    private static readonly List<SpriteRenderer> released = new();

    /// <summary>原版图片池完成更新后，先标记上一批，再发布当前姿态。</summary>
    public static void BeginFrame()
    {
        foreach (Pose pose in poses.Values) pose.Active = false;
    }

    /// <summary>只有完成的显示姿态能够进入这里，不推进人物或技能。</summary>
    /// <param name="renderer">原版当前实际图片。</param><param name="displacement">上身在屏幕世界平面中的偏移。</param>
    /// <param name="shoulderHeight">从脚底到肩点的世界距离。</param>
    /// <param name="root">同轮脚下投影及只用于显示的跳动。</param>
    /// <param name="scale">该姿态对应的原图片尺寸。</param>
    public static void SetPose(SpriteRenderer renderer, Vector2 displacement, float shoulderHeight, Vector3 root,
        float scale)
    {
        if (!poses.TryGetValue(renderer, out Pose pose)) poses.Add(renderer, pose = new Pose());
        pose.Active = true;
        pose.Displacement = displacement;
        pose.ShoulderHeight = shoulderHeight;
        pose.Root = root;
        pose.Scale = scale;
    }

    /// <summary>姿态结束或图片池换人时立即回收，不让旧变形落到下一个人物身上。</summary>
    public static void EndFrame()
    {
        released.Clear();
        foreach (var entry in poses)
            if (!entry.Value.Active)
            {
                entry.Value.Dispose();
                released.Add(entry.Key);
            }

        foreach (SpriteRenderer renderer in released) poses.Remove(renderer);
        released.Clear();
    }

    /// <summary>主相机与检查相机都从同一份图片姿态构造绘制。</summary>
    /// <param name="renderer">实际原版图片。</param><param name="pose">该图片已经发布的变形。</param>
    internal static bool TryGet(SpriteRenderer renderer, out Pose pose) => poses.TryGetValue(renderer, out pose);

    /// <summary>切换世界时释放本次全部临时网格。</summary>
    internal static void Clear()
    {
        foreach (Pose pose in poses.Values) pose.Dispose();
        poses.Clear();
        released.Clear();
    }

    /// <summary>一个当前图片实例复用的小网格和材质参数；没有复制贴图。</summary>
    internal sealed class Pose
    {
        /// <summary>是否仍属于当前这批原版人物。</summary>
        internal bool Active;

        /// <summary>共同姿态投影后的上身偏移。</summary>
        internal Vector2 Displacement;

        /// <summary>共同脚底至肩点距离。</summary>
        internal float ShoulderHeight;

        /// <summary>同轮脚下在原版中的投影，包含原有受击及跳动显示。</summary>
        internal Vector3 Root;

        /// <summary>已确认体型对应的图片缩放，不读取未来人物尺寸。</summary>
        internal float Scale;

        /// <summary>本次绘制采用的共同脚下与尺寸，不修改原版精灵池的缓存。</summary>
        internal Matrix4x4 Matrix;

        /// <summary>按本次真实绘制变换计算的边界，供近处裁剪及深度排序使用。</summary>
        internal Bounds Bounds;

        /// <summary>本实例复用的变形网格。</summary>
        internal readonly Mesh Mesh = new() { name = "Cultiway.NativeBody", hideFlags = HideFlags.DontSave };

        /// <summary>本次原版图片的完整纹理、颜色和翻转资料。</summary>
        internal readonly MaterialPropertyBlock Properties = new();

        /// <summary>最近取得原始几何的图片；换动画帧时重新取样。</summary>
        private Sprite source;

        /// <summary>细分后尚未变形的图片点。</summary>
        private Vector3[] original;

        /// <summary>当前姿态复用的变形结果。</summary>
        private Vector3[] vertices;

        /// <summary>先在图片变化时细分，再按共同偏移变形，最后沿用原图片颜色与纹理。</summary>
        /// <param name="renderer">原版图片本次的实际变换。</param>
        internal void Prepare(SpriteRenderer renderer)
        {
            if (source != renderer.sprite) Rebuild(renderer.sprite);
            Matrix = renderer.localToWorldMatrix;
            float ratio = Scale / Mathf.Abs(renderer.transform.lossyScale.y);
            for (int column = 0; column < 2; column++) Matrix.SetColumn(column, Matrix.GetColumn(column) * ratio);
            Matrix.SetColumn(3, new Vector4(Root.x, Root.y, Root.z, 1f));
            Vector3 flip = new(renderer.flipX ? -1f : 1f, renderer.flipY ? -1f : 1f, 1f);
            float foot = renderer.flipY ? -source.bounds.max.y : source.bounds.min.y;
            float scale = Scale;
            Matrix4x4 inverse = Matrix.inverse;
            for (int i = 0; i < vertices.Length; i++)
            {
                float height = (original[i].y * flip.y - foot) * scale;
                float weight = Mathf.SmoothStep(0, 1, height / ShoulderHeight);
                Vector3 delta = inverse.MultiplyVector((Vector3)Displacement * weight);
                vertices[i] = original[i] + Vector3.Scale(delta, flip);
            }

            Mesh.vertices = vertices;
            Mesh.RecalculateBounds();
            Bounds local = Mesh.bounds;
            Vector3 x = Matrix.MultiplyVector(Vector3.right * local.extents.x),
                y = Matrix.MultiplyVector(Vector3.up * local.extents.y);
            Bounds = new Bounds(Matrix.MultiplyPoint3x4(local.center), new Vector3(
                Mathf.Abs(x.x) + Mathf.Abs(y.x), Mathf.Abs(x.y) + Mathf.Abs(y.y),
                Mathf.Abs(x.z) + Mathf.Abs(y.z)) * 2f);
            Properties.Clear();
            renderer.GetPropertyBlock(Properties);
            Properties.SetTexture("_MainTex", source.texture);
            Properties.SetColor("_RendererColor", renderer.color);
            Properties.SetVector("_Flip", new Vector4(flip.x, flip.y, 1, 1));
            Texture2D alpha = source.associatedAlphaSplitTexture;
            Properties.SetFloat("_EnableExternalAlpha", alpha == null ? 0 : 1);
            if (alpha != null) Properties.SetTexture("_AlphaTex", alpha);
        }

        /// <summary>每个原三角形细分为十六块，保留原轮廓和逐点贴图坐标，腰部可弯而脚底不滑动。</summary>
        /// <param name="sprite">本次真正使用的动画帧。</param>
        private void Rebuild(Sprite sprite)
        {
            source = sprite;
            Vector2[] points = sprite.vertices, uv = sprite.uv;
            ushort[] triangles = sprite.triangles;
            var positions = new List<Vector3>();
            var coordinates = new List<Vector2>();
            var indices = new List<int>();
            for (int face = 0; face < triangles.Length; face += 3)
            {
                int a = triangles[face], b = triangles[face + 1], c = triangles[face + 2];
                int start = positions.Count;
                for (int row = 0; row <= 4; row++)
                for (int column = 0; column <= 4 - row; column++)
                {
                    float v = row * .25f, u = column * .25f, w = 1 - u - v;
                    positions.Add(points[a] * w + points[b] * u + points[c] * v);
                    coordinates.Add(uv[a] * w + uv[b] * u + uv[c] * v);
                }

                for (int row = 0; row < 4; row++)
                for (int column = 0; column < 4 - row; column++)
                {
                    int here = start + row * 5 - row * (row - 1) / 2 + column;
                    int next = here + 5 - row;
                    indices.Add(here);
                    indices.Add(here + 1);
                    indices.Add(next);
                    if (column < 3 - row)
                    {
                        indices.Add(here + 1);
                        indices.Add(next + 1);
                        indices.Add(next);
                    }
                }
            }

            original = positions.ToArray();
            vertices = new Vector3[original.Length];
            Mesh.Clear();
            Mesh.vertices = original;
            Mesh.uv = coordinates.ToArray();
            Mesh.SetTriangles(indices, 0);
            var colours = new Color32[original.Length];
            for (int i = 0; i < colours.Length; i++) colours[i] = Color.white;
            Mesh.colors32 = colours;
        }

        /// <summary>只释放自己的临时网格，原图片和贴图仍由原版持有。</summary>
        internal void Dispose() => Object.Destroy(Mesh);
    }
}