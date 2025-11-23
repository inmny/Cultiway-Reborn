using Cultiway.Core;
using Friflo.Engine.ECS;

namespace Cultiway.Core.Components;

public struct StatusTickState : IComponent
{
    public float Timer;
    public float Value;
    public ElementComposition Element;
    public BaseSimObject Source;
}
