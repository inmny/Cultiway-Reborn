using Cultiway.Content.Libraries;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Components;

public struct Jindan(string type, float strength) : IComponent
{
    public readonly string jindan_type = type;
    public float strength = strength;

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