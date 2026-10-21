using System.Collections.Generic;
using Cultiway.Const;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Core.Visuals.Forms;

/// <summary>原版视角读取共同姿态；通过矩阵绘制完整模型，不逐帧复制全部顶点。</summary>
public sealed class FormNativeRenderSystem : BaseSystem
{
    /// <summary>复用到实际显示峰值的部件视图。</summary>
    private readonly List<MeshRenderer> views = new();
    /// <summary>共享的临时属性，提交时由 Unity 复制到对应视图。</summary>
    private readonly MaterialPropertyBlock properties = new();

    /// <summary>读取本轮发布状态，更新可见部件的资源、矩阵和投影边界。</summary>
    protected override void OnUpdateGroup()
    {
        int index = 0;
        float time = World.world == null ? 0f : (float)World.world.getCurWorldTime();
        foreach (FormVisualInstance instance in FormVisualScene.Instances)
        {
            for (int part = 0; part < instance.Resource.Meshes.Length; part++)
            {
                if (index == views.Count) views.Add(CreateView());
                MeshRenderer view = views[index++];
                Mesh mesh = instance.Resource.Meshes[part];
                view.GetComponent<MeshFilter>().sharedMesh = mesh;
                Material material = FormResources.Surface(instance.Surface ?? instance.Resource.Description.Parts[part].Surface, true);
                material.SetFloat("_WorldTime", time); view.sharedMaterial = material;
                Matrix4x4 transform = instance.PartTransform(part);
                properties.Clear(); properties.SetMatrix("_FormMatrix", transform);
                properties.SetMatrix("_FormNormalMatrix", transform.inverse.transpose);
                view.SetPropertyBlock(properties);
                Bounds bounds = ProjectBounds(mesh.bounds, transform);
                view.localBounds = bounds;
                Vector3 centre = transform.MultiplyPoint3x4(mesh.bounds.center);
                view.sortingOrder = Mathf.Clamp(Mathf.RoundToInt((centre.z - centre.y) * 8f), -32000, 32000);
                view.gameObject.SetActive(true);
            }
        }
        while (index < views.Count) views[index++].gameObject.SetActive(false);
    }

    /// <summary>为一个实际需要的部件建立可复用视图。</summary>
    private static MeshRenderer CreateView()
    {
        var root = new GameObject("Cultiway.FormPart", typeof(MeshFilter), typeof(MeshRenderer));
        MeshRenderer renderer = root.GetComponent<MeshRenderer>();
        renderer.sortingLayerName = RenderSortingLayerNames.EffectsTop_5;
        return renderer;
    }

    /// <summary>用八个边界角计算投影范围，使高度和变形后的物件正确参与镜头裁剪。</summary>
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
            point = new Vector3(point.x, point.y + point.z, (point.y - point.z) * .0001f - .5f);
            if (corner == 0) result = new Bounds(point, Vector3.zero); else result.Encapsulate(point);
        }
        return result;
    }

    /// <summary>世界切换时释放视图和发布状态，共享模型不重复加载。</summary>
    public void ClearWorldState()
    {
        FormVisualScene.Clear();
        foreach (MeshRenderer view in views) Object.Destroy(view.gameObject);
        views.Clear();
    }
}
