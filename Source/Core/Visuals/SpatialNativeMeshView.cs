using System.Collections.Generic;
using Cultiway.Const;
using UnityEngine;

namespace Cultiway.Core.Visuals;

/// <summary>将共用模型投影到原版视角；只绘制，不推进招式。</summary>
internal sealed class SpatialNativeMeshView : MonoBehaviour
{
    /// <summary>该池化视图独占的动态网格。</summary>
    private Mesh mesh;
    /// <summary>保留硬边颜色的投影顶点。</summary>
    private readonly List<Vector3> vertices = new();
    /// <summary>逐顶点颜色。</summary>
    private readonly List<Color32> colors = new();
    /// <summary>投影后的三角形索引。</summary>
    private readonly List<int> triangles = new();
    /// <summary>发光能量独立混合，避免冲击波遮成实心色块。</summary>
    private readonly List<int> energyTriangles = new();
    /// <summary>有内部纹理和体积明暗的主体独立绘制。</summary>
    private readonly List<int> surfaceTriangles = new();
    /// <summary>主体局部坐标与表面种类，供像素着色读取。</summary>
    private readonly List<Vector4> surfaceCoordinates = new();
    /// <summary>表面朝向、光照与世界尺寸，不依赖相机贴图动画。</summary>
    private readonly List<Vector4> surfaceLighting = new();
    /// <summary>当前批次的部件深度，复用存储避免按三角形排序。</summary>
    private readonly List<(int instance, float depth)> order = new();
    /// <summary>半透明水体内部也按前后绘制，避免浪底盖住浪唇和泡沫。</summary>
    private readonly List<(int triangle, float depth)> waterFaces = new();
    /// <summary>提供稳定明暗的世界光方向。</summary>
    private static readonly Vector3 Light = new Vector3(-0.5f, -0.4f, 0.8f).normalized;

    /// <summary>为一个实际需要显示的主体建立二维视图。</summary>
    /// <param name="parent">当前世界的显示根节点。</param>
    internal static SpatialNativeMeshView Create(Transform parent)
    {
        GameObject root = new("spatial_mesh", typeof(MeshFilter), typeof(MeshRenderer));
        root.transform.SetParent(parent, false);
        SpatialNativeMeshView view = root.AddComponent<SpatialNativeMeshView>();
        view.mesh = new Mesh { name = "Cultiway_SpatialProjection", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        view.mesh.MarkDynamic();
        root.GetComponent<MeshFilter>().sharedMesh = view.mesh;
        MeshRenderer renderer = root.GetComponent<MeshRenderer>();
        renderer.sharedMaterials = new[]{WorldVisualResources.TransparentSpriteMaterial,WorldVisualResources.SurfaceMaterial,WorldVisualResources.AdditiveEnergyMaterial};
        renderer.sortingLayerName = RenderSortingLayerNames.EffectsTop_5;
        return view;
    }

    /// <summary>把当前可见的空间部件合为一次绘制，主体与粒子共用深度顺序。</summary>
    /// <param name="instances">已经完成原版镜头裁剪的本帧快照。</param>
    internal void DrawBatch(IReadOnlyList<SpatialVisualMeshInstance> instances)
    {
        vertices.Clear(); colors.Clear(); triangles.Clear(); energyTriangles.Clear(); surfaceTriangles.Clear();
        surfaceCoordinates.Clear();surfaceLighting.Clear();order.Clear();
        WorldVisualResources.SurfaceMaterial.SetFloat("_WorldTime",(float)World.world.getCurWorldTime());
        // 按完整部件排序，模型内部共享顶点；能量环、流带与薄层不逐三角形分配和排序。
        for (int n = 0; n < instances.Count; n++)
        {
            Vector3 center=instances[n].TransformPoint(instances[n].Model.Bounds.center);
            order.Add((n,center.z-center.y));
        }
        order.Sort((left,right)=>left.depth.CompareTo(right.depth));
        foreach(var draw in order)
        {
            SpatialVisualMeshInstance instance=instances[draw.instance];
            SpatialVisualMesh model=instance.Model;
            int offset=vertices.Count;
            Matrix4x4 transform=Matrix4x4.TRS(instance.Position,instance.Rotation,instance.Scale);
            Quaternion inverse=Quaternion.Inverse(instance.Rotation);
            Vector3 localLight=inverse*Light,localView=inverse*new Vector3(0,-.7071068f,.7071068f);
            bool softVolume=instance.Emissive && (model.Name=="energy_orb" || model.Name=="energy_flame");
            Vector3 minimum=model.Bounds.min,size=model.Bounds.size;
            Vector3 inverseSize=new(1f/Mathf.Max(.001f,size.x),1f/Mathf.Max(.001f,size.y),1f/Mathf.Max(.001f,size.z));
            float worldSize=Vector3.Scale(size,instance.Scale).magnitude;
            for(int i=0;i<model.Vertices.Length;i++)
            {
                Vector3 point=transform.MultiplyPoint3x4(model.Vertices[i]);
                vertices.Add(new Vector3(point.x,point.y+point.z,0f));
                float shade=instance.Emissive?1f:.62f+Mathf.Max(0f,Vector3.Dot(model.Normals[i],localLight))*.38f;
                Color color=(Color)model.VertexColors[i]*instance.Tint;
                if(instance.Surface==SpatialSurface.None)
                {
                    if(softVolume)color.a*=Mathf.SmoothStep(.06f,.7f,Mathf.Abs(Vector3.Dot(model.Normals[i],localView)));
                    color.r*=shade;color.g*=shade;color.b*=shade;
                }
                colors.Add(color);
                if(instance.Surface!=SpatialSurface.None)
                {
                    Vector3 p=Vector3.Scale(model.Vertices[i]-minimum,inverseSize);
                    surfaceCoordinates.Add(new Vector4(p.x,p.y,p.z,(float)instance.Surface));
                    surfaceLighting.Add(new Vector4(Vector3.Dot(model.Normals[i],localView),Mathf.Max(0f,Vector3.Dot(model.Normals[i],localLight)),worldSize,0f));
                }
                else{surfaceCoordinates.Add(Vector4.zero);surfaceLighting.Add(Vector4.zero);}
            }
            var indices=instance.Surface!=SpatialSurface.None?surfaceTriangles:instance.Emissive&&instance.Additive?energyTriangles:triangles;
            if (instance.Surface == SpatialSurface.Water)
            {
                waterFaces.Clear();
                Vector3 depthAxis = Vector3.Scale(localView, instance.Scale);
                for (int i = 0; i < model.Triangles.Length; i += 3)
                {
                    Vector3 center = model.Vertices[model.Triangles[i]] + model.Vertices[model.Triangles[i + 1]] + model.Vertices[model.Triangles[i + 2]];
                    waterFaces.Add((i, Vector3.Dot(center, depthAxis)));
                }
                waterFaces.Sort(static (a,b) => a.depth == b.depth ? a.triangle.CompareTo(b.triangle) : a.depth.CompareTo(b.depth));
                foreach (var face in waterFaces)
                    for (int corner = 0; corner < 3; corner++) indices.Add(offset + model.Triangles[face.triangle + corner]);
            }
            else for(int i=0;i<model.Triangles.Length;i++)indices.Add(offset+model.Triangles[i]);
        }
        mesh.Clear(); mesh.SetVertices(vertices); mesh.SetColors(colors);
        mesh.SetUVs(0,surfaceCoordinates);mesh.SetUVs(1,surfaceLighting);mesh.subMeshCount=3;
        mesh.SetTriangles(triangles,0,false);mesh.SetTriangles(surfaceTriangles,1,false);mesh.SetTriangles(energyTriangles,2,false);mesh.RecalculateBounds();
        gameObject.SetActive(instances.Count > 0);
    }

    /// <summary>随着世界视图销毁其动态网格。</summary>
    private void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
    }
}
