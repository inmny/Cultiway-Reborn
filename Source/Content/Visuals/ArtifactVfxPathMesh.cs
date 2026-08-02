using System.Collections.Generic;
using Cultiway.Core.Visuals;
using UnityEngine;

namespace Cultiway.Content.Visuals;

/// <summary>把法器路径样式适配到通用 Ribbon 网格，并处理纹理与端点精灵。</summary>
internal static class ArtifactVfxPathMesh
{
    internal static void Build(
        Mesh mesh,
        RibbonPathMeshBuffer buffer,
        IReadOnlyList<Vector3> source,
        float width,
        ArtifactVfxPathStyleDef style,
        float elapsed,
        Color color,
        float alpha,
        bool trail)
    {
        var ribbonStyle = new RibbonPathStyle(
            style.TileLength,
            style.FlowSpeed,
            style.StartWidth,
            style.MiddleWidth,
            style.EndWidth,
            style.Smooth);
        RibbonPathMesh.Build(mesh, buffer, source, width, ribbonStyle, elapsed, color, alpha, trail);
    }

    internal static void ApplyTexture(
        MeshRenderer renderer,
        Texture texture,
        ref MaterialPropertyBlock block)
    {
        block ??= new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        block.SetTexture("_MainTex", texture);
        block.SetColor("_Color", Color.white);
        renderer.SetPropertyBlock(block);
        renderer.enabled = texture != null;
    }

    internal static void ShowCap(
        SpriteRenderer renderer,
        Sprite sprite,
        Vector3 position,
        Vector3 direction,
        float size,
        Color color,
        bool reverse)
    {
        if (sprite == null || size <= 0f || color.a <= 0f)
        {
            renderer.enabled = false;
            return;
        }
        float rotation = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + (reverse ? 180f : 0f);
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.transform.position = position;
        renderer.transform.rotation = Quaternion.Euler(0f, 0f, rotation);
        renderer.transform.localScale = Vector3.one * size;
        renderer.enabled = true;
    }

}
