using Friflo.Engine.ECS;
using Friflo.Json.Fliox;
using UnityEngine;

namespace Cultiway.Core.Components;

public struct SpecialItem : IComponent
{
    [Ignore]
    public Entity    self;

    public ItemShape Shape => self.GetComponent<ItemShape>();
    public ItemIconData IconData => self.GetComponent<ItemIconData>();
    public Sprite GetSprite()
    {
        if (Shape.Type.GetIcon != null)
        {
            var sprite = Shape.Type.GetIcon(self);
            if (sprite != null) return sprite;
        }
        return ItemIconGenerator.GenerateIcon(Shape, IconData);
    }
}
