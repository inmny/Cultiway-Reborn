using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.Components;

public struct AnimTint : IComponent
{
    public Color Value;

    public AnimTint(Color value)
    {
        Value = value;
    }
}
