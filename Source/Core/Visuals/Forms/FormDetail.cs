using UnityEngine;

namespace Cultiway.Core.Visuals.Forms;

/// <summary>按实际屏幕占用选择显示几何，不减少逻辑更新、作用区或挂点。</summary>
public static class FormDetail
{
    /// <summary>最高显示细节需要的像素跨度。</summary>
    public const float NearPixels = 160f;
    /// <summary>中景显示细节需要的像素跨度。</summary>
    public const float MiddlePixels = 48f;

    /// <summary>大主体即使距离很远也保留轮廓；小主体仍保留远景几何。</summary>
    /// <param name="pixels">本次视点下的最大屏幕跨度。</param>
    public static int Level(float pixels) => pixels >= NearPixels ? 0 : pixels >= MiddlePixels ? 1 : 2;

    /// <summary>原版固定投影按同一像素比例换算，使用已经投影的真实保守边界。</summary>
    /// <param name="projection">原版平面中的实际边界。</param><param name="camera">当前绘制相机。</param>
    public static int Native(Bounds projection, Camera camera) =>
        Level(Mathf.Max(projection.size.x, projection.size.y) * camera.pixelHeight / (2f * camera.orthographicSize));

    /// <summary>从当前真实投影计算包围盒跨度；跨越视眼时保留完整几何。</summary>
    /// <param name="bounds">当前姿态的保守模型边界。</param><param name="transform">当前完整世界变换。</param>
    /// <param name="viewProjection">本帧实际投影。</param><param name="pixels">实际输出像素尺寸。</param>
    public static int Projected(Bounds bounds, Matrix4x4 transform, Matrix4x4 viewProjection, Vector2Int pixels)
    {
        TryProjected(bounds, transform, viewProjection, pixels, out int level);
        return level;
    }

    /// <summary>由同一保守屏幕边界判断是否完全离开画面，并选取实际细节。</summary>
    /// <param name="bounds">当前部件的保守局部边界。</param>
    /// <param name="transform">当前实际位姿。</param>
    /// <param name="viewProjection">本帧确定的三维投影。</param>
    /// <param name="pixels">实际输出尺寸。</param>
    /// <param name="level">近、中、远显示等级。</param>
    internal static bool TryProjected(Bounds bounds, Matrix4x4 transform, Matrix4x4 viewProjection,
        Vector2Int pixels, out int level)
    {
        Vector2 minimum = new(float.PositiveInfinity, float.PositiveInfinity), maximum = -minimum;
        Matrix4x4 matrix = viewProjection * transform;
        for (int corner = 0; corner < 8; corner++)
        {
            Vector3 point = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                (corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
            Vector4 clip = matrix * new Vector4(point.x, point.y, point.z, 1f);
            if (!(clip.w > .00001f))
            {
                level = 0;
                return true;
            }
            Vector2 screen = new(clip.x / clip.w, clip.y / clip.w);
            minimum = Vector2.Min(minimum, screen); maximum = Vector2.Max(maximum, screen);
        }
        Vector2 span = (maximum - minimum) * .5f;
        level = Level(Mathf.Max(span.x * pixels.x, span.y * pixels.y));
        // 跨视线边缘和数值边界时仍留给图形接口裁剪。
        return minimum.x <= 1.00001f && minimum.y <= 1.00001f && maximum.x >= -1.00001f && maximum.y >= -1.00001f;
    }
}
