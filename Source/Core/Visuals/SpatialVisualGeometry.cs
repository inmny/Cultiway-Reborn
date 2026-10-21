using UnityEngine;

namespace Cultiway.Core.Visuals;

/// <summary>可供多个招式组合的固定几何；每帧只改变姿态，不重新上传模型。</summary>
internal static partial class SpatialVisualGeometry
{
    /// <summary>外缘半径为一的宽气浪，曲面与雾材质共同软化边缘。</summary>
    internal static readonly SpatialVisualMesh Ring = BuildRing();
    /// <summary>用于局部接触和轻量碎屑的短菱体。</summary>
    internal static readonly SpatialVisualMesh Spark = BuildSpark();

    /// <summary>沿一段路径添加三棱细杆，侧面在不同观察方向都具有厚度。</summary>
    /// <param name="builder">目标模型。</param>
    /// <param name="start">起点。</param>
    /// <param name="end">终点。</param>
    /// <param name="width">完整粗细。</param>
    /// <param name="color">表面颜色。</param>
    internal static void AddRod(SpatialVisualMeshBuilder builder, Vector3 start, Vector3 end,
        float width, Color32 color)
    {
        Vector3 direction = (end - start).normalized;
        Vector3 right = Vector3.Cross(direction,
            Mathf.Abs(direction.z) < 0.9f ? Vector3.forward : Vector3.up).normalized * width * 0.5f;
        Vector3 up = Vector3.Cross(direction, right).normalized * width * 0.5f;
        for (int i = 0; i < 3; i++)
        {
            float a = i * Mathf.PI * 2f / 3f;
            float b = (i + 1) * Mathf.PI * 2f / 3f;
            Vector3 first = right * Mathf.Cos(a) + up * Mathf.Sin(a);
            Vector3 second = right * Mathf.Cos(b) + up * Mathf.Sin(b);
            builder.AddFace(new[] { start + first, start + second, end + second, end + first }, color);
        }
    }

    /// <summary>建立有圆截面的宽气浪，避免范围越大描线越明显。</summary>
    private static SpatialVisualMesh BuildRing()
    {
        SpatialVisualMeshBuilder builder = new();
        AddMistBand(builder, 0f, Mathf.PI * 2f);
        return builder.Build("spatial_pressure_wave", SpatialSurface.Mist);
    }

    /// <summary>宽带保留体积，外缘不超出单位半径；开口处沿长度收尖。</summary>
    /// <param name="builder">目标模型。</param><param name="start">起始角度。</param><param name="angle">展开角度。</param>
    private static void AddMistBand(SpatialVisualMeshBuilder builder, float start, float angle)
    {
        const int segments = 48, sides = 8;
        bool closed = angle >= Mathf.PI * 1.99f;
        for (int i = 0; i < segments; i++)
        for (int j = 0; j < sides; j++)
            builder.AddFace(new[] { Point(i,j), Point(i+1,j), Point(i+1,j+1), Point(i,j+1) }, Color.white);
        Vector3 Point(int segment, int side)
        {
            float t = segment / (float)segments, a = start + angle * t, b = side * Mathf.PI * 2f / sides;
            float taper = closed ? 1f : Mathf.Pow(Mathf.Sin(t * Mathf.PI), .6f);
            float r = .81f + Mathf.Cos(b) * .19f * taper;
            return new Vector3(Mathf.Cos(a)*r, Mathf.Sin(a)*r, Mathf.Sin(b)*.13f*taper);
        }
    }

    /// <summary>生成两端尖细、中央略宽的四面菱体。</summary>
    private static SpatialVisualMesh BuildSpark()
    {
        SpatialVisualMeshBuilder builder = new();
        Vector3[] rim = { Vector3.left * 0.25f, Vector3.down * 0.25f,
            Vector3.right * 0.25f, Vector3.up * 0.25f };
        for (int i = 0; i < 4; i++)
        {
            builder.AddFace(new[] { rim[i], rim[(i + 1) % 4], Vector3.forward * .35f }, Color.white);
            builder.AddFace(new[] { rim[(i + 1) % 4], rim[i], Vector3.back * .35f }, Color.white);
        }
        return builder.Build("spatial_spark");
    }
}
