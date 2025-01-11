using Cultiway.Core.Libraries;
using Friflo.Engine.ECS;
using Friflo.Json.Fliox;
using UnityEngine;

namespace Cultiway.Core.Components;

public struct ItemShape(string shape_id) : IComponent
{
    public string shape_id = shape_id;
    private ItemShapeAsset _type;
    public  int            texture_idx = -1;

    [Ignore]
    public ItemShapeAsset Type
    {
        get
        {
            if (_type == null) _type = ModClass.L.ItemShapeLibrary.get(shape_id);

            return _type;
        }
    }

    public Sprite GetSprite()
    {
        if (texture_idx == -1) texture_idx = Type.GetRandomTextureIdx();

        return Type.GetSprite(texture_idx);
    }
}