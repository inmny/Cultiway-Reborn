using System.IO;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>在实际开口保存一件原版无主兵器，作用结束时向口外抛出。</summary>
public sealed class ConstructionWeaponChamber
{
    /// <summary>器口内缘的实际世界半径。</summary>
    public float Radius = 2f;

    /// <summary>器口后方器腹的实际世界深度。</summary>
    public float Depth = 2.5f;

    /// <summary>同一原物离口时的每秒格数。</summary>
    public float ReleaseSpeed = 16f;
}

internal sealed class CompiledWeaponChamber
{
    internal readonly float Radius, Depth, ReleaseSpeed;

    internal CompiledWeaponChamber(ConstructionWeaponChamber source, string path)
    {
        if (!(source.Radius >= .5f && source.Radius <= 8f && source.Depth >= .5f && source.Depth <= 10f &&
              source.ReleaseSpeed > 0f && source.ReleaseSpeed <= 32f))
            throw new InvalidDataException(path + "：器口半径须为0.5至8格，深度须为0.5至10格，离口速度须大于0且不超过32");
        Radius = source.Radius; Depth = source.Depth; ReleaseSpeed = source.ReleaseSpeed;
    }
}

/// <summary>可见内缘、接入区域和释放共用同一世界姿态。</summary>
internal readonly struct WeaponChamberFrame
{
    internal const float LipDepth = .24f;
    internal const float OpeningSeconds = .24f;
    internal readonly Vector3 Mouth, Forward;
    internal readonly Quaternion Rotation;
    internal readonly float Radius, Depth, ReleaseSpeed;
    internal readonly float ExitProgress;
    internal readonly bool Accepting, Releasing;

    internal WeaponChamberFrame(in Matrix4x4 pose, CompiledWeaponChamber rule, float age, Vector2 active)
    {
        Mouth = pose.GetColumn(3);
        Forward = Vector3.ProjectOnPlane(pose.GetColumn(2), Vector3.forward).normalized;
        if (Forward.sqrMagnitude < .01f) Forward = Vector3.right;
        Rotation = Quaternion.LookRotation(Forward, Vector3.forward);
        Radius = rule.Radius; Depth = rule.Depth; ReleaseSpeed = rule.ReleaseSpeed;
        Accepting = age >= active.x && age < GatherEnd(active);
        Releasing = age >= active.y;
        ExitProgress = Mathf.InverseLerp(active.y - OpeningSeconds, active.y, age);
    }

    internal static float GatherEnd(Vector2 active) => Mathf.Lerp(active.x, active.y, .48f);

    internal bool Contains(Vector3 world)
    {
        Vector3 offset = world - Mouth;
        float axial = Vector3.Dot(offset, Forward);
        return axial >= 0f && axial <= LipDepth * Depth &&
               (offset - Forward * axial).sqrMagnitude <= Radius * Radius;
    }
}
