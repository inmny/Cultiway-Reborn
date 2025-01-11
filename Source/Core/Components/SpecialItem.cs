using Friflo.Engine.ECS;
using Friflo.Json.Fliox;
using UnityEngine;

namespace Cultiway.Core.Components;

public struct SpecialItem : IComponent
{
    [Ignore]
    public Entity    self;
    [Ignore]
    public ItemShape Shape => self.GetComponent<ItemShape>();

    public Sprite GetSprite()
    {
        return Shape.GetSprite();
    }
}