using System.Collections.Generic;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Core.Visuals.Forms;

/// <summary>原版视角读取共同姿态，保留真实表面和足够精度的内部深度。</summary>
public sealed class FormNativeRenderSystem : BaseSystem
{
    /// <summary>一个可复用部件视图及本帧已经确定的投影资料。</summary>
    internal sealed class PartView
    {
        /// <summary>尚未映射到相机范围的完整投影边界。</summary>
        internal Bounds Projection;

        /// <summary>本次是否为半透明表面。</summary>
        internal bool Transparent;

        /// <summary>本次实际部件及其所属版本，用于相机准备阶段选择共享显示几何。</summary>
        internal FormResource Resource;

        /// <summary>所属真实部件索引。</summary>
        internal int Part;

        /// <summary>动态曲面保留共同格网，不以静态远景模型替代。</summary>
        internal bool Dynamic;

        /// <summary>需要时才为当前视口建立的动态地表网格。</summary>
        internal FormLayerFrame Layer;

        /// <summary>薄霜在着色器中沿地图平面重投影，不能按普通边界裁剪。</summary>
        internal bool FlatNative;

        /// <summary>本相机当前使用的显示等级。</summary>
        internal int Detail;

        /// <summary>与原有视图使用同一姿态的批量提交资料。</summary>
        internal FormDrawItem Draw;
    }

    /// <summary>复用到实际显示峰值的部件视图。</summary>
    private readonly List<PartView> views = new();

    /// <summary>整帧的共享表面与默认颜色。</summary>
    private readonly FormFrameSurfaces surfaces = new(native: true);

    /// <summary>保持原版形体所在的默认相机层；独立绘制验证可使用专用层。</summary>
    internal int LayerMask { get; set; } = 1;

    /// <summary>保持当前绘制使用的完整资源版本。</summary>
    internal FormResource FrameResource => frameResource ??
                                           (FormNativeSpriteDeformation.HasPoses ? FormResources.Load("limb") : null);

    /// <summary>当前生成形体使用的资源，纯身体动作直接复用已发布的共同显示材料。</summary>
    private FormResource frameResource;

    /// <summary>本帧已经发布的实际部件数。</summary>
    internal int ViewCount { get; private set; }

    /// <summary>本帧共同深度倍率。</summary>
    internal float DepthScale { get; private set; }

    /// <summary>本帧共同深度偏移。</summary>
    internal float DepthBias { get; private set; }

    /// <summary>取得已经准备的部件，不复制几何。</summary>
    /// <param name="index">有效部件索引。</param>
    internal PartView ViewAt(int index) => views[index];

    /// <summary>先补齐身体的原版显示偏移，再准备姿态和边界，最后由实际相机共同绘制。</summary>
    protected override void OnUpdateGroup()
    {
        int index = 0;
        float time = World.world == null ? 0f : (float)World.world.getCurWorldTime();
        frameResource = null;
        surfaces.Begin(time);
        foreach (FormVisualInstance instance in FormVisualScene.Instances)
        {
            frameResource ??= instance.Resource;
            // 原版跳动和抖动已经写入人物精灵平面，绑定部件使用相同平面偏移，不改玩法姿态。
            Vector2 bodyOffset = Vector2.zero;
            if (instance.GroundActorId != 0)
            {
                Actor actor = World.world.units.get(instance.GroundActorId);
                if (actor != null) bodyOffset = actor.move_jump_offset + actor.shake_offset;
            }

            bool anchored = instance.GroundActorId != 0 && instance.Terrain != null && !instance.DynamicGeometry &&
                            !instance.Resource.Description.ConformToGround;
            float anchorHeight =
                anchored ? instance.Terrain.Sample(instance.GroundAnchor.x, instance.GroundAnchor.y) : 0f;
            Texture2D terrainTexture = FormTerrainTextures.Get(instance.Terrain);
            Vector4 terrainSize = instance.Terrain == null ? default :
                new Vector4(instance.Terrain.Size.x, instance.Terrain.Size.y, instance.Terrain.Outside, 0);
            for (int part = 0; part < instance.PartCount; part++)
            {
                if (index == views.Count) views.Add(new PartView());
                PartView view = views[index++];
                view.Resource = instance.Resource;
                view.Part = part;
                view.Dynamic = instance.DynamicGeometry;
                view.Layer = instance.Layer;
                Mesh mesh = instance.Geometry ?? instance.Resource.Meshes[part];
                var surface = surfaces.Get(instance.Resource, instance.SurfaceFor(part), instance.IsTranslucent);
                FormPartDescription description = instance.Resource.Description.Parts[part];
                Matrix4x4 transform = instance.PartTransform(part);
                transform.m03 += bodyOffset.x;
                transform.m13 += bodyOffset.y;
                bool skinned = !view.Dynamic && instance.Skin != null && description.Skin;
                view.Draw = new FormDrawItem
                {
                    Resource = instance.Resource,
                    Mesh = view.Layer == null ? mesh : null,
                    Material = surface.Material,
                    Transform = transform,
                    Skin = skinned ? instance.Skin : null,
                    Tint = instance.Tint ?? surface.Color,
                    Dye = instance.Dye ?? Color.white,
                    Build = new Vector4(instance.BuildProgress, FormFormation.TipSpan,
                        !view.Dynamic && description.HasBuildData ? 1f : 0f, instance.Feedback.Strength),
                    Contact = instance.Feedback.Region(new Vector3(bodyOffset.x, bodyOffset.y, 0f)),
                    Anchor = new Vector4(anchorHeight, anchored ? 1 : 0, 0, 0),
                    ClosedVolume = !view.Dynamic && description.HasSurfaceData,
                    Terrain = terrainTexture,
                    TerrainSize = terrainSize
                };
                Bounds bounds = instance.Layer != null ? ProjectBounds(instance.Layer.Bounds, Matrix4x4.identity) :
                    instance.PoseBounds.HasValue ? ProjectBounds(instance.PoseBounds.Value, instance.Transform) :
                    ProjectBounds(mesh.bounds, transform);
                if (instance.Layer != null || instance.PoseBounds.HasValue)
                    bounds.center += new Vector3(bodyOffset.x, bodyOffset.y, bodyOffset.y);
                if (instance.Terrain != null)
                {
                    // 绑定身体的刚性部件共享脚下承托；实际流散或贴地变形仍按各顶点采样。
                    float middle =
                        anchored ? anchorHeight : (instance.Terrain.Minimum + instance.Terrain.Maximum) * .5f;
                    float range = anchored ? 0f : instance.Terrain.Maximum - instance.Terrain.Minimum;
                    bounds.center += new Vector3(0, -middle, middle);
                    bounds.Expand(new Vector3(0, range, range));
                }

                view.Projection = bounds;
                view.Transparent = surface.Transparent;
                view.FlatNative = surface.FlatNative;
            }
        }

        int previousCount = ViewCount;
        ViewCount = index;
        if (index > 0 || FormNativeSpriteDeformation.HasPoses) FormNativeScenePass.Attach(World.world.camera, this);
        while (index < previousCount)
        {
            PartView hidden = views[index++];
            hidden.Resource = null;
            hidden.Layer = null;
            hidden.Draw = default;
        }

        FormResources.UpdateRetired();
        FormTerrainTextures.Collect();
        FormLayerMeshes.Collect();
    }

    /// <summary>相机收集精灵后，把双方一起映射到同一个有精度的深度范围。</summary>
    /// <param name="camera">实际相机。</param>
    /// <param name="minimum">形体和可见精灵的前方边界。</param>
    /// <param name="maximum">双方的后方边界。</param>
    internal void MapDepth(Camera camera, float minimum, float maximum)
    {
        float scale = (camera.farClipPlane - camera.nearClipPlane) * .8f / Mathf.Max(1f, maximum - minimum);
        float bias = camera.transform.position.z + (camera.nearClipPlane + camera.farClipPlane) * .5f -
                     (minimum + maximum) * .5f * scale;
        DepthScale = scale;
        DepthBias = bias;
    }

    /// <summary>相机只为可能进入画面的部件选择远近几何及法线矩阵。</summary>
    /// <param name="view">本次可见部件。</param>
    /// <param name="camera">实际原版相机。</param>
    internal static FormDrawItem Prepare(PartView view, Camera camera)
    {
        view.Detail = FormDetail.Native(view.Projection, camera);
        FormDrawItem item = view.Draw;
        item.Mesh = view.Layer != null ? FormLayerMeshes.Get(view.Layer) :
            view.Dynamic ? item.Mesh : view.Resource.DetailMesh(view.Part, view.Detail);
        item.Normal = item.Transform.inverse.transpose;
        view.Draw = item;
        return item;
    }

    /// <summary>计算实际投影平面和未压缩的前后边界，供裁剪与共同深度映射使用。</summary>
    /// <param name="local">局部几何边界。</param>
    /// <param name="transform">实际部位变换。</param>
    private static Bounds ProjectBounds(Bounds local, Matrix4x4 transform)
    {
        Bounds result = default;
        for (int corner = 0; corner < 8; corner++)
        {
            Vector3 point = local.center + Vector3.Scale(local.extents,
                new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
            point = transform.MultiplyPoint3x4(point);
            point = new Vector3(point.x, point.y + point.z, point.y - point.z);
            if (corner == 0) result = new Bounds(point, Vector3.zero);
            else result.Encapsulate(point);
        }

        return result;
    }

    /// <summary>世界切换时释放视图和发布状态，共享模型不重复加载。</summary>
    public void ClearWorldState()
    {
        FormNativeScenePass.Clear(this);
        frameResource = null;
        ViewCount = 0;
        FormVisualScene.Clear();
        FormTerrainTextures.Collect(true);
        FormLayerMeshes.Collect(true);
        surfaces.Begin(0f);
        views.Clear();
    }
}
