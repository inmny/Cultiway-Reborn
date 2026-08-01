using Friflo.Engine.ECS;

namespace Cultiway.Core.Components;

/// <summary>记录同类状态参与“强者覆盖、同强刷新、弱者忽略”比较时使用的强度。</summary>
public struct StatusPotency : IComponent
{
    public float Value;
}
