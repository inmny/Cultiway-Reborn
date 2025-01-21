using Cultiway.Content.Libraries;
using Friflo.Engine.ECS;
using Friflo.Json.Fliox;

namespace Cultiway.Content.Components;

public struct Yuanying(string type, float strength) : IComponent
{
    public readonly string yuanying_type = type;
    public float strength = strength;
    public int stage = 0;
    [Ignore]
    public YuanyingAsset Type
    {
        get
        {
            if (_type != null) return _type;
            _type = Libraries.Manager.YuanyingLibrary.get(yuanying_type);
            return _type;
        }
    }

    private YuanyingAsset _type;
}