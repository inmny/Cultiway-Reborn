using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.Components;

public struct SpecialItem : IComponent
{
    public Entity    self;
    public ItemShape Shape => self.GetComponent<ItemShape>();

    public Sprite GetSprite()
    {
        return Shape.GetSprite();
    }
}