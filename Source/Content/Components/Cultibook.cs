using Cultiway.Content.Libraries;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Components;

public struct Cultibook : IComponent
{
    public string ID;
    private CultibookAsset _asset;

    public CultibookAsset Asset
    {
        get
        {
            if (_asset == null)
            {
                _asset = Libraries.Manager.CultibookLibrary.get(ID);
            }

            return _asset;
        }
    }
}