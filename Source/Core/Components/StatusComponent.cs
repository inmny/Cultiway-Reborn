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
    /// <summary>施加时的人物编号，防止对象池重用后新人物继承旧来源身份。</summary>
    [Ignore]
    public long SourceActorId;

    internal void SetSource(BaseSimObject source)
    {
        Source = source;
        SourceActorId = (source as Actor)?.data.id ?? 0;
    }
    [Ignore]
    public float? SourcePowerLevel;
}
