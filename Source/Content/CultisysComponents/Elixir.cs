using Cultiway.Content.Libraries;
using Friflo.Engine.ECS;

namespace Cultiway.Content.CultisysComponents;

public struct Elixir : IComponent
{
    public string elixir_id;

    public ElixirAsset Type
    {
        get
        {
            _type ??= Libraries.Manager.ElixirLibrary.get(elixir_id);
            return _type;
        }
    }

    private ElixirAsset _type;
}