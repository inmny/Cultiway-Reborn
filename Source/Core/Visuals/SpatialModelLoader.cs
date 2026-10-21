using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace Cultiway.Core.Visuals;

/// <summary>读取制作工具导出的共同模型，原版和三维视角使用同一几何资料。</summary>
internal static class SpatialModelLoader
{
    /// <summary>模型只在首次引用时读取，表面材料由招式组合指定。</summary>
    /// <param name="name">随模组发布的模型文件名。</param><param name="surface">默认表面。</param>
    internal static SpatialVisualMesh Load(string name,SpatialSurface surface)
    {
        string path=Path.Combine(ModClass.I.GetDeclaration().FolderPath,"Content","CombatModels",name+".json");
        ModelData data=JsonConvert.DeserializeObject<ModelData>(File.ReadAllText(path));
        Vector3[] positions=Vectors(data.positions),normals=Vectors(data.normals);
        Color32[] colors=new Color32[data.colors.Length];
        for(int i=0;i<colors.Length;i++)
        {
            uint rgba=data.colors[i];
            colors[i]=new Color32((byte)rgba,(byte)(rgba>>8),(byte)(rgba>>16),(byte)(rgba>>24));
        }
        return new SpatialVisualMesh("crafted_"+name,positions,normals,data.triangles,colors,surface);
    }

    /// <summary>展开制作工具写入的三维浮点数组。</summary>
    /// <param name="values">按横向、纵深、高度排列的资料。</param>
    private static Vector3[] Vectors(float[] values)
    {
        Vector3[] result=new Vector3[values.Length/3];
        for(int i=0;i<result.Length;i++)result[i]=new Vector3(values[i*3],values[i*3+1],values[i*3+2]);
        return result;
    }

    /// <summary>离线模型导出的资料，不属于世界存档。</summary>
    private sealed class ModelData
    {
        /// <summary>顶点位置。</summary>
        public float[] positions;
        /// <summary>平滑或硬边法向。</summary>
        public float[] normals;
        /// <summary>三角形连接。</summary>
        public int[] triangles;
        /// <summary>每个三角形的基础颜色。</summary>
        public uint[] colors;
    }
}
