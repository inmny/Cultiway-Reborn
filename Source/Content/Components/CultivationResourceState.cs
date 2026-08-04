using Friflo.Engine.ECS;

namespace Cultiway.Content.Components;

/// <summary>保存角色自身持有、由修炼资源系统统一管理的可消耗资源。</summary>
public struct CultivationResourceState : IComponent
{
    /// <summary>杀戮或特殊环境修炼后暂存在角色体内、尚未炼化的浊气。</summary>
    public float personal_dirty_wakan;
}
