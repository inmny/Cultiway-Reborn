using Cultiway.Core;
using Cultiway.Core.Libraries;
using Friflo.Engine.ECS;
using Friflo.Json.Fliox;

namespace Cultiway.Core.Components;

public struct StatusComponent : IComponent
{
    public string id;
    private StatusEffectAsset _type;
    [Ignore]
    public StatusEffectAsset Type
    {
        get
        {
            if (_type == null) _type = ModClass.L.StatusEffectLibrary.get(id);

            return _type;
        }
    }
    
    /// <summary>
    /// 状态施加方信息，用于计算powerlevel差距
    /// </summary>
    [Ignore]
    public BaseSimObject Source;
}