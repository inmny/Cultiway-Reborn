using Cultiway.Content.Libraries;
using Friflo.Engine.ECS;
using Friflo.Json.Fliox;

namespace Cultiway.Content.Components;

public struct Elixir : IComponent
{
    public string elixir_id;
    public float value;
    [Ignore]
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