using Cultiway.Core.Libraries;
using UnityEngine;

namespace Cultiway.Core.Components;

public struct SpecialMaterial
{
    public  string             shape_id;
    private MaterialShapeAsset _type;
    public  int                texture_idx;

    public MaterialShapeAsset Type
    {
        get
        {
            if (_type == null) _type = ModClass.L.MaterialShapeLibrary.get(shape_id);

            return _type;
        }
    }

    public Sprite GetSprite()
    {
        return Type.GetSprite(texture_idx);
    }
}