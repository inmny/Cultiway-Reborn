using Friflo.Engine.ECS;

namespace Cultiway.Content.Components;

public struct CraftingElixir : IComponent
{
    public string elixir_id;
    public int    progress;
}