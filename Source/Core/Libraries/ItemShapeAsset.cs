using System.Collections.Generic;
using UnityEngine;

namespace Cultiway.Core.Libraries;

public class ItemShapeAsset : Asset
{
    public string       major_texture_folder;
    public List<Sprite> major_textures = new();

    public void LoadTextures()
    {
        major_textures.Clear();
        if (!string.IsNullOrEmpty(major_texture_folder))
            major_textures.AddRange(SpriteTextureLoader.getSpriteList(major_texture_folder));
    }

    public Sprite GetSprite(int idx)
    {
        return major_textures[idx % major_textures.Count];
    }

    public int GetRandomTextureIdx()
    {
        return Randy.randomInt(0, major_textures.Count);
    }
}