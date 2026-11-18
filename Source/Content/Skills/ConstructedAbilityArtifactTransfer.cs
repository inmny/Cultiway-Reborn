using System;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Persistence;
using Cultiway.Core.Skills;
using Cultiway.Core.Visuals;
using UnityEngine;

namespace Cultiway.Content.Skills;

public sealed partial class ConstructedAbilityService
{
    /// <summary>只在共同确认时移动原法宝；预测、预览与回退不能写入原物件。</summary>
    /// <param name="running">持有借用的实际施放。</param><param name="clock">本次确认时点。</param>
    private static void ConfirmArtifactTransfers(RunningCast running, double clock)
    {
        AbilityCast cast = running.Cast;
        ConstructedArtifactCarrier carrier = running.Carrier;
        foreach (int index in cast.Plan.ArtifactTransferSteps)
        {
            if (!cast.AwaitingStepWorld(index)) continue;
            if (carrier?.IsValid != true || !ArtifactAppearanceRenderer.TryGetSpatialPose(carrier.Artifact, out var visual) ||
                !cast.TryMapArtifact(index, CarrierFrame(visual).GetColumn(3), out Vector3 point, out Quaternion turn) ||
                !simulation.CanPlaceMappedArtifact(visual, point, turn * visual.Rotation))
            {
                cast.RejectStepWorld(index);
                continue;
            }

            Matrix4x4 pose = carrier.PlaceMapped(point, turn * visual.Rotation);
            running.CarrierPose = pose;
            cast.RefreshTransferredCarrier(pose);
            if (!carrier.Artifact.HasComponent<WorldSaveIdentity>())
                carrier.Artifact.AddComponent(new WorldSaveIdentity { Id = Guid.NewGuid().ToString("N") });
            string id = carrier.Artifact.GetComponent<WorldSaveIdentity>().Id;
            cast.ConfirmStepWorld(index, new ConstructionWorldReceipt(ConstructionWorldResultKind.ArtifactTransfer,
                cast.Plan.Steps[index].Node, clock, items: new[] { id }, destination: pose.GetColumn(3)));
        }
    }

    /// <summary>按原模型朝向和尺寸核对出口的落点及包络，只看出口，不扫过门间。</summary>
    /// <param name="visual">原法宝显示与作用共用的模型。</param>
    /// <param name="point">对应出口中心。</param><param name="rotation">对应出口朝向。</param>
    private bool CanPlaceMappedArtifact(in SpatialVisualMeshInstance visual, Vector3 point, Quaternion rotation)
    {
        if (!HasTransferGround(point)) return false;
        Bounds bounds = visual.Model.Bounds;
        Vector3 low = bounds.min, high = bounds.max;
        Bounds destination = new(point + rotation * Vector3.Scale(low, visual.Scale), Vector3.zero);
        for (int corner = 0; corner < 8; corner++)
        {
            Vector3 local = new((corner & 1) == 0 ? low.x : high.x, (corner & 2) == 0 ? low.y : high.y,
                (corner & 4) == 0 ? low.z : high.z);
            Vector3 surface = point + rotation * Vector3.Scale(local, visual.Scale);
            if (!HasTransferGround(surface) || surface.z + .001f < (terrain?.Sample(surface.x, surface.y) ?? 0f)) return false;
            destination.Encapsulate(surface);
        }

        for (int x = Mathf.FloorToInt(destination.min.x); x <= Mathf.FloorToInt(destination.max.x); x++)
        for (int y = Mathf.FloorToInt(destination.min.y); y <= Mathf.FloorToInt(destination.max.y); y++)
            if (!HasTransferGround(new Vector2(x + .5f, y + .5f))) return false;

        Vector3 bottom = new(destination.center.x, destination.center.y, destination.min.z);
        return BodyLocomotion.DestinationAllowed?.Invoke(bottom, Mathf.Max(.02f, destination.size.z)) != false;
    }
}
