using Cultiway.Content.Crafting;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Components;

/// <summary>标识半成品所属的炼制会话，防止新任务接管旧半成品。</summary>
public struct CraftSession : IComponent
{
    public string session_id;
    public long actor_id;
    public long order_id;
    public CraftProcessType process;
}
