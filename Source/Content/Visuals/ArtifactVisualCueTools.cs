using Cultiway.Content.Artifacts;
using Cultiway.Content.Libraries;
using Cultiway.Core.Components;
using UnityEngine;

namespace Cultiway.Content.Visuals;

/// <summary>程序化法器视觉共用的锚点、配色和渲染资源工具。</summary>
internal static class ArtifactVisualCueTools
{
    private static Material visualMaterial;

    internal static Color ResolveColor(ArtifactVisualTheme theme, ArtifactVisualColorRole role)
    {
        return role switch
        {
            ArtifactVisualColorRole.Primary => theme.primary,
            ArtifactVisualColorRole.Secondary => theme.secondary,
            ArtifactVisualColorRole.Glow => theme.glow,
            _ => theme.primary,
        };
    }

    internal static Vector3 ResolveDirection(ArtifactAbilityVisualContext context)
    {
        if (context.direction.sqrMagnitude >= 0.0001f) return context.direction.normalized;
        if (!context.artifact.IsNull && context.artifact.HasComponent<Rotation>())
        {
            Vector3 direction = context.artifact.GetComponent<Rotation>().value;
            if (direction.sqrMagnitude >= 0.0001f) return direction.normalized;
        }
        return Vector3.right;
    }

    internal static bool TryResolve(
        ArtifactAbilityVisualContext context,
        ArtifactVisualAnchorRef anchor,
        Vector3 offset,
        bool matchActorScale,
        out Vector3 position)
    {
        if (!ArtifactAbilityVisuals.TryResolveAnchorPosition(context, anchor, out position)) return false;
        float scale = matchActorScale ? ArtifactAbilityVisuals.ResolveActorScale(context) : 1f;
        position += offset * scale;
        return true;
    }

    internal static Material VisualMaterial
    {
        get
        {
            if (visualMaterial != null) return visualMaterial;
            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");
            visualMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
            return visualMaterial;
        }
    }

    internal static SpriteRenderer AddSprite(
        Transform parent,
        string name,
        string sortingLayer,
        int sortingOrder)
    {
        GameObject obj = new(name, typeof(SpriteRenderer));
        obj.transform.SetParent(parent, false);
        SpriteRenderer renderer = obj.GetComponent<SpriteRenderer>();
        renderer.sharedMaterial = VisualMaterial;
        renderer.sortingLayerName = sortingLayer;
        renderer.sortingOrder = sortingOrder;
        renderer.spriteSortPoint = SpriteSortPoint.Pivot;
        return renderer;
    }

    internal static MeshRenderer AddMesh(
        Transform parent,
        string name,
        string sortingLayer,
        int sortingOrder,
        out MeshFilter filter)
    {
        GameObject obj = new(name, typeof(MeshFilter), typeof(MeshRenderer));
        obj.transform.SetParent(parent, false);
        filter = obj.GetComponent<MeshFilter>();
        MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = VisualMaterial;
        renderer.sortingLayerName = sortingLayer;
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }
}

internal sealed class ArtifactEmptyVisualLease : IArtifactVisualLease
{
    internal static readonly ArtifactEmptyVisualLease Instance = new();
    public bool IsAlive => false;
    public void Refresh(ArtifactAbilityVisualContext context, double now, float duration) { }
    public void Update(ArtifactAbilityVisualContext context, double now) { }
    public void End() { }
}
