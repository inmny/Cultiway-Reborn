using UnityEngine;

namespace Cultiway.Content.Visuals;

/// <summary>原版火光使用实心像素纹理，放大后会出现黄色方片；仅替换它的光晕表面。</summary>
internal static class NativeFireGlow
{
    /// <summary>已经处理的原版发射器。</summary>
    private static GlowParticles owner;
    /// <summary>整个游戏复用一份柔和光晕，不生成外部贴图资源。</summary>
    private static Texture2D texture;
    /// <summary>只替换火光自己的材质，不修改原版共享材质。</summary>
    private static Material material;

    /// <summary>世界建立或切换后绑定一次，保留原版发射位置、颜色、寿命和火焰玩法。</summary>
    internal static void Ensure()
    {
        var current = World.world?.particles_fire;
        if (current == null || owner == current) return;
        owner = current;
        if (texture == null)
        {
            const int size = 64;
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { name = "Cultiway_FireGlow", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float radius = new Vector2((x + .5f) / size * 2 - 1, (y + .5f) / size * 2 - 1).magnitude;
                float alpha = Mathf.Pow(Mathf.Clamp01(1 - radius * radius), 2.5f);
                pixels[y * size + x] = new Color(alpha, alpha, alpha, alpha);
            }
            texture.SetPixels(pixels); texture.Apply(false, true);
        }
        var renderer = current.GetComponent<ParticleSystemRenderer>();
        if (material != null) Object.Destroy(material);
        material = new Material(renderer.sharedMaterial)
        { name = "Cultiway_NativeFireGlow", mainTexture = texture, mainTextureScale = Vector2.one, mainTextureOffset = Vector2.zero };
        renderer.sharedMaterial = material;
    }
}
