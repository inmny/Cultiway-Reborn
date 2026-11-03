using System.Collections.Generic;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Core.Visuals.Forms;

/// <summary>原版图片的连续变形；沿用原贴图与深度材质，只改变本次绘制的顶点。</summary>
public static class FormNativeSpriteDeformation
{
    /// <summary>本帧实际需要变形的图片，完成后移除已不再使用的实例。</summary>
    private static readonly Dictionary<SpriteRenderer, Pose> poses = new();

    /// <summary>回收上一帧已经结束的图片，不在遍历时修改字典。</summary>
    private static readonly List<SpriteRenderer> released = new();

    /// <summary>独立身体动作也需要显示共同姿态，不依赖另一个生成形体恰好可见。</summary>
    internal static bool HasPoses => poses.Count != 0;

    /// <summary>只让当前相机真正看得见的身体启动原版绘制，三维隐藏后不接管其过渡画面。</summary>
    /// <param name="mask">当前相机的可见图层。</param>
    internal static bool HasVisiblePoses(int mask)
    {
        foreach (SpriteRenderer renderer in poses.Keys)
            if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy &&
                !renderer.forceRenderingOff && (mask & (1 << renderer.gameObject.layer)) != 0)
                return true;
        return false;
    }

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
    /// <param name="body">腿部动作的共同已完成姿态，空值沿用原上身变形。</param>
    public static void SetPose(SpriteRenderer renderer, Vector2 displacement, float shoulderHeight, Vector3 root,
        float scale, BodyPose? body = null)
    {
        if (!poses.TryGetValue(renderer, out Pose pose)) poses.Add(renderer, pose = new Pose());
        pose.Active = true;
        pose.Displacement = displacement;
        pose.ShoulderHeight = shoulderHeight;
        pose.Root = root;
        pose.Scale = scale;
        pose.Body = body;
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

        /// <summary>当前腿部动作，两个足点和膝点都来自已完成的同一姿态。</summary>
        internal BodyPose? Body;

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

        /// <summary>三维安装引起的逐顶点地面偏移，以及相同几何的深度边界。</summary>
        internal bool HasVertexGround;

        /// <summary>变形后的最近和最远实际深度。</summary>
        internal float MinimumDepth, MaximumDepth;

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
            if (Body?.Organ is BodyOrganPose organ)
            {
                // 整个受控身体沿用同轮锁定方向，不能让控制器临时转身改变图片而不改变接触。
                float angle = organ.BodyAngle * Mathf.Deg2Rad;
                float sx = organ.PixelScale.x * source.pixelsPerUnit, sy = organ.PixelScale.y * source.pixelsPerUnit;
                Matrix.SetColumn(0, new Vector4(Mathf.Cos(angle) * sx, Mathf.Sin(angle) * sx, 0f, 0f));
                Matrix.SetColumn(1, new Vector4(-Mathf.Sin(angle) * sy, Mathf.Cos(angle) * sy, 0f, 0f));
                Matrix.SetColumn(2, new Vector4(0f, 0f, 1f, 0f));
            }

            Matrix.SetColumn(3, new Vector4(Root.x, Root.y, Root.z, 1f));
            Vector3 flip = new(renderer.flipX ? -1f : 1f, renderer.flipY ? -1f : 1f, 1f);
            float foot = renderer.flipY ? -source.bounds.max.y : source.bounds.min.y;
            float scale = Scale;
            Matrix4x4 inverse = Matrix.inverse;
            HasVertexGround = Body?.Organ?.Model.HasValue == true;
            MinimumDepth = float.PositiveInfinity;
            MaximumDepth = float.NegativeInfinity;
            for (int i = 0; i < vertices.Length; i++)
            {
                float depthShift = 0f;
                float height = (original[i].y * flip.y - foot) * scale;
                float weight = Mathf.SmoothStep(0, 1, height / ShoulderHeight);
                Vector2 movement = Body?.Head.HasValue == true ? HeadDisplacement(original[i], flip, height, weight) :
                    Body?.LeftLeg.HasValue == true ? LegDisplacement(original[i], flip, height) : Displacement * weight;
                if (Body?.Organ is BodyOrganPose attached && attached.Model.HasValue)
                {
                    Vector3 shift = attached.Pivot - attached.NativePivot;
                    float weightAtMount = original[i].y * flip.y * source.pixelsPerUnit * attached.PixelScale.y /
                                          attached.NativeHeight;
                    movement += new Vector2(shift.x, shift.y + shift.z) * weightAtMount;
                    depthShift = shift.y * weightAtMount;
                }

                Vector3 delta = inverse.MultiplyVector(new Vector3(movement.x, movement.y, depthShift));
                vertices[i] = original[i] + Vector3.Scale(delta, flip);
                Vector3 world = Matrix.MultiplyPoint3x4(Vector3.Scale(vertices[i], flip));
                float ground = Root.y - Root.z + (HasVertexGround ? world.z - Root.z : 0f);
                float depth = 2f * ground - world.y;
                MinimumDepth = Mathf.Min(MinimumDepth, depth);
                MaximumDepth = Mathf.Max(MaximumDepth, depth);
            }

            Mesh.vertices = vertices;
            Mesh.RecalculateBounds();
            Bounds local = Mesh.bounds;
            Vector3 x = Matrix.MultiplyVector(Vector3.right * local.extents.x),
                y = Matrix.MultiplyVector(Vector3.up * local.extents.y),
                z = Matrix.MultiplyVector(Vector3.forward * local.extents.z);
            Bounds = new Bounds(Matrix.MultiplyPoint3x4(local.center), new Vector3(
                Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y),
                Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z)) * 2f);
            Properties.Clear();
            renderer.GetPropertyBlock(Properties);
            Properties.SetFloat("_NativeVertexGround", HasVertexGround ? 1f : 0f);
            Properties.SetTexture("_MainTex", source.texture);
            Properties.SetColor("_RendererColor", renderer.color);
            Properties.SetVector("_Flip", new Vector4(flip.x, flip.y, 1, 1));
            Texture2D alpha = source.associatedAlphaSplitTexture;
            Properties.SetFloat("_EnableExternalAlpha", alpha == null ? 0 : 1);
            if (alpha != null) Properties.SetTexture("_AlphaTex", alpha);
        }

        /// <summary>原头图围绕实际头关节变化，下方逐步衔接同轮躯干，不另造头部模型。</summary>
        /// <param name="point">原图片顶点。</param><param name="flip">当前图片翻转。</param>
        /// <param name="height">顶点离脚高度。</param><param name="torsoWeight">原有躯干权重。</param>
        private Vector2 HeadDisplacement(Vector3 point, Vector3 flip, float height, float torsoWeight)
        {
            BodyPose body = Body.Value;
            BodyHeadPose head = body.Head.Value;
            float x = point.x * flip.x * Scale;
            Vector3 relative = new(x - head.NativeCenter.x * body.Height, 0,
                height - head.NativeCenter.z * body.Height);
            Vector3 target = (head.A + head.B) * .5f + head.Delta * relative * head.NativeScale;
            Vector2 moved = new(target.x - body.Foot.x - x, target.y - body.Foot.y + target.z - body.Foot.z - height);
            float start = head.NativePivot.z * body.Height;
            float weight = Mathf.SmoothStep(0, 1,
                Mathf.InverseLerp(start - body.Height * .04f, start + body.Height * .04f, height));
            return Vector2.Lerp(Displacement * torsoWeight, moved, weight);
        }

        /// <summary>原贴图下身沿实际髋膝足连续变形，上身与同轮躯干衔接，不追加另一双腿。</summary>
        /// <param name="point">原图片顶点。</param><param name="flip">本帧翻转。</param><param name="height">顶点离脚底高度。</param>
        private Vector2 LegDisplacement(Vector3 point, Vector3 flip, float height)
        {
            BodyPose body = Body.Value;
            float hipHeight = body.Height * CompiledBodyAction.HipHeight;
            float x = (point.x * flip.x - source.bounds.center.x) * Scale;
            bool left = x < 0;
            BodyLegPose leg = (left ? body.LeftLeg : body.RightLeg).Value;
            Vector2 Project(Vector3 value) => new(value.x - body.Foot.x, value.y - body.Foot.y + value.z - body.Foot.z);
            float referenceX = (left ? -1f : 1f) * source.bounds.size.x * Scale * .2f;
            Vector2 hip = Project(leg.Hip), knee = Project(leg.Knee), foot = Project(leg.Foot);
            Vector2 desired;
            if (height < hipHeight * .5f) desired = Vector2.Lerp(foot, knee, Mathf.Clamp01(height / (hipHeight * .5f)));
            else desired = Vector2.Lerp(knee, hip, Mathf.Clamp01((height - hipHeight * .5f) / (hipHeight * .5f)));
            Vector2 delta = desired - new Vector2(referenceX, Mathf.Min(height, hipHeight));
            if (height > hipHeight)
                delta = Vector2.Lerp(delta, Displacement, Mathf.InverseLerp(hipHeight, ShoulderHeight, height));
            return delta;
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