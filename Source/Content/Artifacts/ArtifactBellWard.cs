using System.Collections.Generic;
using Cultiway.Content.Combat;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core.Components;
using Cultiway.Core.Visuals;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Artifacts;

/// <summary>金钟护界的实际活动、轮廓与姿态，画面和拦截使用同一钟面。</summary>
internal static class ArtifactBellWard
{
    private static readonly HashSet<Entity> Raised = new();
    internal static IEnumerable<Entity> Active => Raised;
    internal static void Add(Entity item) => Raised.Add(item);
    internal static void Remove(Entity item) => Raised.Remove(item);
    internal static void Clear() => Raised.Clear();

    internal static bool IsRaised(Entity item) => Raised.Contains(item);

    /// <summary>正式存档保留金钟原活动，恢复后从同一运行资料登记钟面。</summary>
    internal static void Restore(IEnumerable<Entity> items)
    {
        foreach (Entity item in items)
        {
            if (!item.TryGetComponent(out ArtifactAbilitySet set) ||
                !item.TryGetComponent(out ArtifactAbilityRuntime runtime) || set.abilities == null ||
                runtime.abilities?.Length != set.abilities.Length) continue;
            for (int i = 0; i < set.abilities.Length; i++)
                if (set.abilities[i].ability_id == ArtifactAbilities.GoldenBellBarrier.id &&
                    runtime.abilities[i].activity_kind != ArtifactAbilityActivityKind.None)
                {
                    Add(item);
                    break;
                }
        }
    }

    internal static bool TryRead(Entity item, out ArtifactAbilityExecutionContext context,
        out ArtifactAbilityInstance ability, out ArtifactAbilityRuntimeEntry entry, out int index)
    {
        context = default;
        ability = default;
        entry = default;
        index = -1;
        if (!item.IsAvailable() || !item.TryGetComponent(out ArtifactAbilityRuntime runtime) ||
            !runtime.attached || !item.TryGetComponent(out ArtifactAbilitySet set) ||
            set.abilities == null || runtime.abilities?.Length != set.abilities.Length ||
            !ArtifactAbilityLifecycle.MeetsState(runtime.control_state, ArtifactControlState.Operating)) return false;
        for (int i = 0; i < set.abilities.Length; i++)
        {
            if (set.abilities[i].ability_id != ArtifactAbilities.GoldenBellBarrier.id ||
                runtime.abilities[i].activity_kind == ArtifactAbilityActivityKind.None) continue;
            ability = set.abilities[i];
            entry = runtime.abilities[i];
            index = i;
            context = ArtifactAbilityLifecycle.CreateActivityContext(runtime.controller, item, runtime.control_state, entry);
            return true;
        }
        return false;
    }

    internal static bool TryShape(Entity item, out Shape shape)
    {
        shape = default;
        if (!ArtifactAppearanceRenderer.TryGetBellWardMesh(item, out SpatialVisualMesh mesh) ||
            !item.TryGetComponent(out Rotation facing)) return false;
        var appearance = item.GetComponent<ArtifactAppearance>();
        var template = ArtifactAppearanceCatalogLoader.Current.Templates[appearance.template_key];
        Quaternion rotation = Quaternion.AngleAxis(facing.z, Vector3.forward) *
                              Quaternion.Euler(ArtifactAppearanceMath.Vec3(template.SpatialRotation, Vector3.zero));
        shape = Shape.Create(mesh, rotation);
        return shape.Bounds.size.z > .0001f;
    }

    internal static float Opening(double age) => Mathf.Clamp01((float)(age / .16d));
    internal static float OpeningScale(float opening) => .86f + .14f * (1f - Mathf.Pow(1f - opening, 3f));

    internal readonly struct Shape(SpatialVisualMesh model, Quaternion orientation, Bounds bounds)
    {
        internal readonly SpatialVisualMesh Model = model;
        internal readonly Quaternion Orientation = orientation;
        private readonly Vector3 foot = new(bounds.center.x, bounds.center.y, bounds.min.z);
        internal readonly Bounds Bounds = bounds;

        internal static Shape Create(SpatialVisualMesh mesh, Quaternion rotation)
        {
            Bounds extent = new(rotation * mesh.Vertices[0], Vector3.zero);
            foreach (Vector3 vertex in mesh.Vertices) extent.Encapsulate(rotation * vertex);
            return new Shape(mesh, rotation, extent);
        }

        internal float Scale(float height, double age) =>
            height * 1.6f / Bounds.size.z * OpeningScale(Opening(age));

        internal SpatialVisualMeshInstance Pose(Vector3 center, float scale, Color color, bool impact = false,
            SpatialVisualMesh drawing = null) => new(drawing ?? Model, center - foot * scale, Orientation,
            Vector3.one * scale, color, true, !impact, center, additive: false,
            surface: impact ? SpatialSurface.Metal : SpatialSurface.Energy);

        internal Bounds At(Vector3 center, float scale) => new(center + (Bounds.center - foot) * scale, Bounds.size * scale);
    }
}
