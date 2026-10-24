using System.Collections.Generic;
using Cultiway.Const;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Core.Visuals.Forms;

/// <summary>原版视角读取共同姿态，保留真实表面和足够精度的内部深度。</summary>
public sealed class FormNativeRenderSystem : BaseSystem
{
    /// <summary>一个可复用部件视图及本帧已经确定的投影资料。</summary>
    internal sealed class PartView
    {
        /// <summary>实际网格绘制器。</summary>
        internal MeshRenderer Renderer;
        /// <summary>缓存的网格入口。</summary>
        internal MeshFilter Filter;
        /// <summary>本部件的复用参数，在深度范围确定后一起提交。</summary>
        internal readonly MaterialPropertyBlock Properties = new();
        /// <summary>尚未映射到相机范围的完整投影边界。</summary>
        internal Bounds Projection;
        /// <summary>实际姿态的前后位置，越小越近。</summary>
        internal float Depth;
        /// <summary>相同深度时保留实际发布顺序。</summary>
        internal int Index;
        /// <summary>本次是否为半透明表面。</summary>
        internal bool Transparent;
    }

    /// <summary>复用到实际显示峰值的部件视图。</summary>
    private readonly List<PartView> views = new();

    /// <summary>保持当前绘制使用的完整资源版本。</summary>
    internal FormResource FrameResource { get; private set; }
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
        float minimum = float.PositiveInfinity, maximum = float.NegativeInfinity;
        float time = World.world == null ? 0f : (float)World.world.getCurWorldTime();
        FrameResource = null;
        foreach (FormVisualInstance instance in FormVisualScene.Instances)
        {
            FrameResource ??= instance.Resource;
            // 原版跳动和抖动已经写入人物精灵平面，绑定部件使用相同平面偏移，不改玩法姿态。
            Vector2 bodyOffset = Vector2.zero;
            if (instance.GroundActorId != 0)
            {
                Actor actor = World.world.units.get(instance.GroundActorId);
                if (actor != null) bodyOffset = actor.move_jump_offset + actor.shake_offset;
            }
            bool anchored = instance.GroundActorId != 0 && instance.Terrain != null && instance.Layer == null &&
                            !instance.Resource.Description.ConformToGround;
            float anchorHeight = anchored ? instance.Terrain.Sample(instance.GroundAnchor.x, instance.GroundAnchor.y) : 0f;
            for (int part = 0; part < instance.PartCount; part++)
            {
                if (index == views.Count) views.Add(CreateView());
                PartView view = views[index];
                view.Index = index++;
                Mesh mesh = instance.MeshFor(part);
                view.Filter.sharedMesh = mesh;
                Material material = instance.Resource.Surface(instance.SurfaceFor(part), true, instance.Tint.HasValue && instance.Tint.Value.a < 1f);
                material.SetFloat("_WorldTime", time);
                view.Renderer.sharedMaterial = material;
                Matrix4x4 transform = instance.PartTransform(part);
                transform.m03 += bodyOffset.x; transform.m13 += bodyOffset.y;
                MaterialPropertyBlock properties = view.Properties;
                properties.Clear(); properties.SetMatrix("_FormMatrix", transform);
                FormTerrainTextures.Bind(instance.Terrain, properties);
                properties.SetVector("_TerrainAnchor", new Vector4(anchorHeight, anchored ? 1 : 0, 0, 0));
                properties.SetMatrix("_FormNormalMatrix", transform.inverse.transpose);
                bool skinned = instance.Layer == null && instance.Skin != null && instance.Resource.Description.Parts[part].Skin;
                properties.SetFloat("_UseSkinning", skinned ? 1f : 0f);
                properties.SetVector("_FormBuild", new Vector4(instance.BuildProgress, FormFormation.TipSpan,
                    instance.Layer == null && instance.Resource.Description.Parts[part].HasBuildData ? 1f : 0f, 0f));
                if (skinned) properties.SetMatrixArray("_FormBones", instance.Skin);
                if (instance.Tint.HasValue) properties.SetColor("_Color", instance.Tint.Value);
                properties.SetColor("_Dye", instance.Dye ?? Color.white);
                Bounds bounds = instance.Layer != null ? ProjectBounds(instance.Layer.Bounds, Matrix4x4.identity) :
                    instance.PoseBounds.HasValue ? ProjectBounds(instance.PoseBounds.Value, instance.Transform) : ProjectBounds(mesh.bounds, transform);
                if (instance.Layer != null || instance.PoseBounds.HasValue)
                    bounds.center += new Vector3(bodyOffset.x, bodyOffset.y, bodyOffset.y);
                if (instance.Terrain != null)
                {
                    // 绑定身体的刚性部件共享脚下承托；实际流散或贴地变形仍按各顶点采样。
                    float middle = anchored ? anchorHeight : (instance.Terrain.Minimum + instance.Terrain.Maximum) * .5f;
                    float range = anchored ? 0f : instance.Terrain.Maximum - instance.Terrain.Minimum;
                    bounds.center += new Vector3(0, -middle, middle);
                    bounds.Expand(new Vector3(0, range, range));
                }
                view.Projection = bounds;
                view.Depth = bounds.center.z;
                minimum = Mathf.Min(minimum, bounds.min.z); maximum = Mathf.Max(maximum, bounds.max.z);
                view.Transparent = material.GetFloat("_ZWrite") < .5f;
                view.Renderer.sortingOrder = 0;
                view.Renderer.gameObject.SetActive(true);
            }
        }

        ViewCount = index; MinimumDepth = minimum; MaximumDepth = maximum;
        if (index > 0) FormNativeScenePass.Attach(World.world.camera, this);
        while (index < views.Count)
        {
            PartView hidden = views[index++]; hidden.Renderer.gameObject.SetActive(false);
            hidden.Filter.sharedMesh = null; hidden.Renderer.sharedMaterial = null;
        }
        FormResources.UpdateRetired();
        FormTerrainTextures.Collect();
        FormLayerMeshes.Collect();
    }

    /// <summary>本帧形体的共同前后边界。</summary>
    internal float MinimumDepth { get; private set; }
    /// <summary>本帧形体的共同后方边界。</summary>
    internal float MaximumDepth { get; private set; }

    /// <summary>相机收集精灵后，把双方一起映射到同一个有精度的深度范围。</summary>
    /// <param name="camera">实际相机。</param>
    /// <param name="minimum">形体和可见精灵的前方边界。</param>
    /// <param name="maximum">双方的后方边界。</param>
    internal void MapDepth(Camera camera, float minimum, float maximum)
    {
        float scale = (camera.farClipPlane - camera.nearClipPlane) * .8f / Mathf.Max(1f, maximum - minimum);
        float bias = camera.transform.position.z + (camera.nearClipPlane + camera.farClipPlane) * .5f - (minimum + maximum) * .5f * scale;
        DepthScale = scale; DepthBias = bias;
        for (int i = 0; i < ViewCount; i++)
        {
            PartView view = views[i];
            view.Properties.SetFloat("_NativeDepthScale", scale); view.Properties.SetFloat("_NativeDepthBias", bias);
            view.Renderer.SetPropertyBlock(view.Properties);
            Bounds bounds = view.Projection;
            Vector3 centre = bounds.center, size = bounds.size;
            centre.z = centre.z * scale + bias; size.z *= scale;
            view.Renderer.localBounds = new Bounds(centre, size);
        }
    }

    /// <summary>为实际需要的部件建立可复用视图，不逐帧获取组件或创建参数块。</summary>
    private static PartView CreateView()
    {
        var root = new GameObject("Cultiway.FormPart", typeof(MeshFilter), typeof(MeshRenderer));
        MeshRenderer renderer = root.GetComponent<MeshRenderer>();
        renderer.forceRenderingOff = true;
        renderer.sortingLayerName = RenderSortingLayerNames.EffectsTop_5;
        return new PartView { Renderer = renderer, Filter = root.GetComponent<MeshFilter>() };
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
            if (corner == 0) result = new Bounds(point, Vector3.zero); else result.Encapsulate(point);
        }
        return result;
    }

    /// <summary>世界切换时释放视图和发布状态，共享模型不重复加载。</summary>
    public void ClearWorldState()
    {
        FormNativeScenePass.Clear(this); FrameResource = null; ViewCount = 0;
        FormVisualScene.Clear();
        FormTerrainTextures.Collect(true);
        FormLayerMeshes.Collect(true);
        foreach (PartView view in views) Object.Destroy(view.Renderer.gameObject);
        views.Clear();
    }
}
