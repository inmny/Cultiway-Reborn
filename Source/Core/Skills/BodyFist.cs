using Cultiway.Core.Visuals.Forms;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>握紧原手的各节指骨，由同一副指根网格确定拳面的作用形状。</summary>
internal sealed class BodyFist
{
    /// <summary>各节手指的固定闭合姿态，按原手部件顺序保存。</summary>
    internal readonly Matrix4x4[] Fingers;

    /// <summary>闭合后四个指根的形状，在腕部坐标下供射程与实际命中共用。</summary>
    internal readonly FormContactBinding[] Knuckles = new FormContactBinding[4];

    /// <summary>按原手的各个关节枢轴收拢手指，再从闭合后的指根取得表面。</summary>
    /// <param name="hand">已有完整掌面及指节网格。</param>
    internal BodyFist(FormResource hand)
    {
        Fingers = new Matrix4x4[hand.Meshes.Length];
        Fingers[0] = Matrix4x4.identity;
        for (int finger = 0; finger < 5; finger++)
        {
            int first = 1 + finger * 2, second = first + 1;
            bool thumb = finger == 4;
            Fingers[first] = Bend(first, thumb ? -45f : -84f, thumb ? -55f : 0f);
            Fingers[second] = Fingers[first] * Bend(second, thumb ? -42f : -94f, 0f);
            if (thumb) continue;

            Bounds bone = hand.Meshes[first].bounds;
            float radius = Mathf.Min(bone.extents.x, bone.extents.z);
            Vector3 start = bone.center, end = bone.center;
            start.y = bone.min.y + radius;
            end.y = bone.max.y - radius;
            Knuckles[finger] = new FormContactBinding(new FormContactDescription
            {
                Id = "knuckles/" + (finger + 1), Part = first,
                A = Coordinates(Fingers[first].MultiplyPoint3x4(start)),
                B = Coordinates(Fingers[first].MultiplyPoint3x4(end)), Radius = radius
            }, hand.Rig, null);
        }

        /// <summary>按对应的实际关节枢轴折叠一个指节。</summary>
        /// <param name="index">形体部件编号。</param><param name="curl">弯曲角。</param><param name="side">向掌内收的角度。</param>
        Matrix4x4 Bend(int index, float curl, float side)
        {
            float[] pivot = hand.Description.Parts[index].Pivot;
            Vector3 joint = new(pivot[0], pivot[1], pivot[2]);
            return Matrix4x4.Translate(joint) * Matrix4x4.Rotate(Quaternion.Euler(curl, 0f, side)) *
                   Matrix4x4.Translate(-joint);
        }
    }

    /// <summary>用共同作用形体的坐标格式登记已经闭合的指根。</summary>
    /// <param name="point">腕部局部坐标。</param>
    private static float[] Coordinates(Vector3 point) => new[] { point.x, point.y, point.z };
}
