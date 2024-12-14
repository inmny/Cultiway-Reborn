using System.Collections.Generic;
using UnityEngine;

namespace Cultiway.Core.Libraries;

public class MaterialShapeAsset : Asset
{
    public string       major_texture_folder;
    public List<Sprite> major_textures = new();

    public void LoadTextures()
    {
        major_textures.Clear();
        major_textures.AddRange(SpriteTextureLoader.getSpriteList(major_texture_folder));
    }

    public Sprite GetSprite(int idx)
    {
        return major_textures[idx % major_textures.Count];
    }
}