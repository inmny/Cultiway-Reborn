using System;
using System.Collections.Generic;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Core.Visuals.Forms;

/// <summary>原版投影读取与物理相同的高度数组，纹理只在快照改变时上传。</summary>
internal static class FormTerrainTextures
{
    /// <summary>弱引用避免显示缓存延长已经无人使用的地表快照寿命。</summary>
    private static readonly List<(WeakReference<AbilityHeightField> Field, Texture2D Texture)> entries = new();

    /// <summary>为这份完成姿态设置对应地表，不改变共用材质。</summary>
    /// <param name="field">同一逻辑轮次的只读快照。</param>
    /// <param name="properties">本视图的独占属性。</param>
    internal static void Bind(AbilityHeightField field, MaterialPropertyBlock properties)
    {
        properties.SetFloat("_TerrainEnabled", field == null ? 0f : 1f);
        if (field == null) return;
        Texture2D texture = null;
        foreach (var entry in entries)
            if (entry.Field.TryGetTarget(out AbilityHeightField found) && ReferenceEquals(found, field)) { texture = entry.Texture; break; }
        if (texture == null)
        {
            texture = new Texture2D(field.Width, field.Height, TextureFormat.RFloat, false, true)
            { name = "Cultiway.FormGround", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            texture.SetPixelData(field.Values, 0); texture.Apply(false, true);
            entries.Add((new WeakReference<AbilityHeightField>(field), texture));
        }
        properties.SetTexture("_TerrainHeights", texture);
        properties.SetVector("_TerrainSize", new Vector4(field.Size.x, field.Size.y, field.Outside, 0));
    }

    /// <summary>主线程回收已经没有快照持有者的纹理，切世界时释放全部。</summary>
    /// <param name="all">是否为世界清理。</param>
    internal static void Collect(bool all = false)
    {
        for (int i = entries.Count - 1; i >= 0; i--)
            if (all || !entries[i].Field.TryGetTarget(out _)) { UnityEngine.Object.Destroy(entries[i].Texture); entries.RemoveAt(i); }
    }
}
