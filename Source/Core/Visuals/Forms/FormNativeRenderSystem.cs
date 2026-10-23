using System.Collections.Generic;
using Cultiway.Const;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Core.Visuals.Forms;

/// <summary>原版视角读取共同姿态，保留真实表面和足够精度的内部深度。</summary>
public sealed class FormNativeRenderSystem : BaseSystem
{
    /// <summary>一个可复用部件视图及本帧已经确定的投影资料。</summary>
    private sealed class PartView
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
    }

    /// <summary>复用到实际显示峰值的部件视图。</summary>
    private readonly List<PartView> views = new();
    /// <summary>本帧透明部件，只排序引用，不复制网格。</summary>
    private readonly List<PartView> transparent = new();

    /// <summary>先准备完整姿态和边界，再映射深度，最后按远近提交透明部件。</summary>
    protected override void OnUpdateGroup()
    {
        int index = 0;
        float minimum = float.PositiveInfinity, maximum = float.NegativeInfinity;
        float time = World.world == null ? 0f : (float)World.world.getCurWorldTime();
        transparent.Clear();
        foreach (FormVisualInstance instance in FormVisualScene.Instances)
        {
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
                MaterialPropertyBlock properties = view.Properties;
                properties.Clear(); properties.SetMatrix("_FormMatrix", transform);
                FormTerrainTextures.Bind(instance.Terrain, properties);
                properties.SetMatrix("_FormNormalMatrix", transform.inverse.transpose);
                bool skinned = instance.Layer == null && instance.Skin != null && instance.Resource.Description.Parts[part].Skin;
                properties.SetFloat("_UseSkinning", skinned ? 1f : 0f);
                if (skinned) properties.SetMatrixArray("_FormBones", instance.Skin);
                if (instance.Tint.HasValue) properties.SetColor("_Color", instance.Tint.Value);
                properties.SetColor("_Dye", instance.Dye ?? Color.white);
                Bounds bounds = instance.Layer != null ? ProjectBounds(instance.Layer.Bounds, Matrix4x4.identity) :
                    instance.PoseBounds.HasValue ? ProjectBounds(instance.PoseBounds.Value, instance.Transform) : ProjectBounds(mesh.bounds, transform);
                if (instance.Terrain != null)
                {
                    float middle = (instance.Terrain.Minimum + instance.Terrain.Maximum) * .5f;
                    float range = instance.Terrain.Maximum - instance.Terrain.Minimum;
                    bounds.center += new Vector3(0, -middle, middle);
                    bounds.Expand(new Vector3(0, range, range));
                }
                view.Projection = bounds;
                view.Depth = bounds.center.z;
                minimum = Mathf.Min(minimum, bounds.min.z); maximum = Mathf.Max(maximum, bounds.max.z);
                if (material.GetFloat("_ZWrite") < .5f) transparent.Add(view);
                view.Renderer.sortingOrder = 0;
                view.Renderer.gameObject.SetActive(true);
            }
        }

        // 原版可能只有16位深度，不能把整个主体压进万分之一单位的薄层。
        // 所有部件使用同一线性映射，映射后的边界也与着色器完全一致。
        if (index > 0)
        {
            Camera camera = World.world.camera;
            float scale = (camera.farClipPlane - camera.nearClipPlane) * .8f / Mathf.Max(1f, maximum - minimum);
            float bias = camera.transform.position.z + (camera.nearClipPlane + camera.farClipPlane) * .5f - (minimum + maximum) * .5f * scale;
            for (int i = 0; i < index; i++)
            {
                PartView view = views[i];
                view.Properties.SetFloat("_NativeDepthScale", scale);
                view.Properties.SetFloat("_NativeDepthBias", bias);
                view.Renderer.SetPropertyBlock(view.Properties);
                Bounds bounds = view.Projection;
                Vector3 centre = bounds.center, size = bounds.size;
                centre.z = centre.z * scale + bias; size.z *= scale;
                view.Renderer.localBounds = new Bounds(centre, size);
            }
        }
        transparent.Sort(CompareTransparent);
        for (int i = 0; i < transparent.Count; i++) transparent[i].Renderer.sortingOrder = Mathf.Min(i + 1, 32767);
        while (index < views.Count)
        {
            PartView hidden = views[index++]; hidden.Renderer.gameObject.SetActive(false);
            hidden.Filter.sharedMesh = null; hidden.Renderer.sharedMaterial = null;
        }
        FormResources.UpdateRetired();
        FormTerrainTextures.Collect();
        FormLayerMeshes.Collect();
    }

    /// <summary>实体先写深度，透明部件之后按真实前后位置由远到近叠加。</summary>
    /// <param name="left">待比较的部件。</param>
    /// <param name="right">另一部件。</param>
    private static int CompareTransparent(PartView left, PartView right)
    {
        int depth = right.Depth.CompareTo(left.Depth);
        return depth != 0 ? depth : left.Index.CompareTo(right.Index);
    }

    /// <summary>为实际需要的部件建立可复用视图，不逐帧获取组件或创建参数块。</summary>
    private static PartView CreateView()
    {
        var root = new GameObject("Cultiway.FormPart", typeof(MeshFilter), typeof(MeshRenderer));
        MeshRenderer renderer = root.GetComponent<MeshRenderer>();
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
        FormVisualScene.Clear();
        FormTerrainTextures.Collect(true);
        FormLayerMeshes.Collect(true);
        foreach (PartView view in views) Object.Destroy(view.Renderer.gameObject);
        views.Clear(); transparent.Clear();
    }
}
