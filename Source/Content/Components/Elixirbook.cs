using Cultiway.Content.Libraries;
using Friflo.Engine.ECS;
using Friflo.Json.Fliox;

namespace Cultiway.Content.Components;

public struct Elixirbook(string id) : IComponent
{
    [Ignore]
    public string ID => id;
    public ElixirAsset Asset {
        get
        {
            if (_asset == null)
            {
                _asset = Libraries.Manager.ElixirLibrary.get(id);
            }

            return _asset;
        }
    }
    [Ignore]
    private ElixirAsset _asset;
}