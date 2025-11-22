using Cultiway.Core.Systems.Render;
using Friflo.Engine.ECS;
using Friflo.Json.Fliox;

namespace Cultiway.Core.Components;

internal struct AnimBindRenderer : IComponent
{
    [Ignore]
    public AnimRenderer value;
}