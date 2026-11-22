using System;
using Cultiway.Content.CreatureCompositions.Libraries;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>从已经采集的实际身体网格找吻端或躯干表面。</summary>
internal static class BodyCastOutlets
{
    internal static bool TryGet(Actor actor, CreatureOrganBodyOutlet outlet, out Vector3 point, out Vector3 forward)
    {
        point = forward = default;
        BodySurfacePose pose = ConstructedAbilityService.BodyCastingSurface(actor);
        if (pose == null || outlet == CreatureOrganBodyOutlet.None) return false;
        Vector3 facing = new(Mathf.Cos(pose.Heading), Mathf.Sin(pose.Heading), 0f);
        forward = facing;
        Vector3 side = Vector3.Cross(facing, Vector3.forward);
        float low = float.PositiveInfinity, high = float.NegativeInfinity;
        ReadSurface(true, 0f, out _);
        if (!(high > low)) return false;
        ReadSurface(false, Mathf.Lerp(low, high, outlet == CreatureOrganBodyOutlet.Mouth ? .32f : .55f), out point);
        return true;

        void ReadSurface(bool measureHeight, float center, out Vector3 bestPoint)
        {
            bestPoint = default;
            float best = float.NegativeInfinity;
            for (int i = 0; i < pose.Parts.Length; i++)
            {
                BodySurfacePart part = pose.Parts[i];
                if (!Matches(part, outlet)) continue;
                Matrix4x4 matrix = pose.Matrices[part.TransformIndex < 0 ? i : part.TransformIndex];
                Vector3[] points = pose.Vertices(i);
                foreach (int index in part.Triangles)
                {
                    Vector3 actual = matrix.MultiplyPoint3x4(points[index]);
                    if (measureHeight)
                    {
                        low = Mathf.Min(low, actual.z);
                        high = Mathf.Max(high, actual.z);
                        continue;
                    }

                    Vector3 offset = actual - pose.Foot;
                    float score = Vector3.Dot(offset, facing) -
                                  Mathf.Abs(actual.z - center) * 1.15f -
                                  Mathf.Abs(Vector3.Dot(offset, side)) * .30f;
                    if (score <= best) continue;
                    best = score;
                    bestPoint = actual;
                }
            }
        }
    }

    private static bool Matches(BodySurfacePart part, CreatureOrganBodyOutlet outlet)
    {
        if (part.Attachment != null || part.Id.StartsWith("organ:", StringComparison.Ordinal)) return false;
        return outlet == CreatureOrganBodyOutlet.Mouth
            ? (part.Id is "head" or "part:head" or "part:jaw") || part.Matches("casting.muzzle")
            : part.Matches("casting.torso") ||
              !part.Matches("casting.muzzle") && (part.Id is "torso" or "part:body" or "part:chest");
    }
}
