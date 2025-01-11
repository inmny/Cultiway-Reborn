using Cultiway.Content.Libraries;
using Friflo.Engine.ECS;
using Friflo.Json.Fliox;

namespace Cultiway.Content.Components;

public struct Jindan(string type, float strength) : IComponent
{
    public readonly string jindan_type = type;
    public float strength = strength;
    [Ignore]
    public JindanAsset Type
    {
        get
        {
            if (_type != null) return _type;
            _type = Libraries.Manager.JindanLibrary.get(jindan_type);
            return _type;
        }
    }

    private JindanAsset _type;
}