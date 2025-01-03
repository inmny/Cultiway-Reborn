using Cultiway.Core.Libraries;
using Friflo.Engine.ECS;

namespace Cultiway.Core.Components;

public struct StatusComponent : IComponent
{
    public string id;
    private StatusEffectAsset _type;
    public StatusEffectAsset Type
    {
        get
        {
            if (_type == null) _type = ModClass.L.StatusEffectLibrary.get(id);

            return _type;
        }
    }
}