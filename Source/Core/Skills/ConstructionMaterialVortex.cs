using System.IO;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>在部位中心打开一个可牵引已受蚀散件的深口。</summary>
public sealed class ConstructionMaterialVortex
{
    /// <summary>外唇的实际横向范围，采用世界格。</summary>
    public float Radius = 4f;

    /// <summary>喉口能通过的最大半宽，采用世界格。</summary>
    public float Throat = .64f;

    /// <summary>外唇到喉口的实际高度差。</summary>
    public float Depth = 1.26f;

    /// <summary>散件向内推进的每秒格数。</summary>
    public float Speed = 2.4f;

    /// <summary>本次部位可以接纳的实际体积。原支付者和控制者均须允许。</summary>
    public float MaximumVolume = 12f;
}

/// <summary>限制实际选取、收束时段及每次部位的工作量。</summary>
internal sealed class CompiledMaterialVortex
{
    /// <summary>共同材料接触使用的凸轮廓。</summary>
    private static readonly Vector2[] rim = Outline();

    /// <summary>同一画面及实际推进共用的范围、速度和容量。</summary>
    internal readonly float Radius, Throat, Depth, Speed, MaximumVolume;

    /// <summary>从外唇向上下实际接纳的高度界限。</summary>
    internal float HalfHeight => Depth + Radius * .45f;

    /// <summary>只接受有限范围、正深度及可收束的喉口。</summary>
    /// <param name="data">可编辑资料。</param><param name="path">错误位置。</param>
    internal CompiledMaterialVortex(ConstructionMaterialVortex data, string path)
    {
        if (!(data.Radius >= 1 && data.Radius <= 24 && data.Throat >= .12f && data.Throat <= data.Radius * .55f &&
              data.Depth >= .15f && data.Depth <= data.Radius && data.Speed > 0 && data.Speed <= 24 &&
              data.MaximumVolume > 0 && data.MaximumVolume <= 640))
            throw new InvalidDataException(path + "：裂口需要有限半径、较小的喉口、实际深度以及正数的推进速度与容量");
        Radius = data.Radius; Throat = data.Throat; Depth = data.Depth;
        Speed = data.Speed; MaximumVolume = data.MaximumVolume;
    }

    /// <summary>把当前可见范围与内外收束分开。有效时段达到完整口径。</summary>
    /// <param name="age">本部位已推进的秒数。</param>
    /// <param name="presence">存在时段。</param><param name="active">正式牵引时段。</param>
    internal Vector2 Extents(float age, Vector2 presence, Vector2 active)
    {
        float opening = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(presence.x, active.x, age));
        float closing = Mathf.InverseLerp(active.y, presence.y, age);
        return new Vector2(opening * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((closing - .32f) / .68f))),
            opening * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(closing / .62f))));
    }

    /// <summary>以当前张开的裂口建立真正的接触范围，供普通世界侵蚀等作用选择。</summary>
    /// <param name="node">查询使用的实际部位。</param><param name="centre">实际外唇中心。</param>
    /// <param name="opening">当前的展开比例。</param>
    internal AbilityCapsule Selection(int node, Vector3 centre, float opening)
    {
        Vector3 point = centre - Vector3.forward * (Depth * opening * .5f);
        var matrix = Matrix4x4.TRS(point, Quaternion.Euler(-90f, 0f, 0f),
            new Vector3(Radius, HalfHeight, Radius) * opening);
        return new AbilityCapsule(node, point, point, 0f, null, prism: new AbilityPrism(rim, matrix));
    }

    /// <summary>生成封闭且逆时针的凸轮廓，重复查询不创建几何。</summary>
    private static Vector2[] Outline()
    {
        var points = new Vector2[16];
        for (int i = 0; i < points.Length; i++)
        {
            float angle = i * (Mathf.PI * 2f / points.Length);
            points[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }
        return points;
    }
}
