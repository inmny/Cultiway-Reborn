using Friflo.Engine.ECS;

namespace Cultiway.Content.Components;

/// <summary>角色当前由金丹或元婴授予的一项形成效果定义。</summary>
public struct CoreFormationGrantedEffect
{
    /// <summary>跨原子合并后的稳定效果族 ID。</summary>
    public string family_id;

    /// <summary>当前授予定义在效果族中的覆盖等级。</summary>
    public int rank;
}

/// <summary>
/// 记录角色当前形成来源授予了哪些 Skill 和 Status 定义。
/// 实际冷却、持续时间和机制数据分别归通用 Skill 与 Status 组件管理。
/// </summary>
public struct CoreFormationGrantRuntime : IComponent
{
    /// <summary>解析结果上限，防止异常形成快照产生无界授予列表。</summary>
    public const int MaxEffects = 12;

    /// <summary>最近一次同步的形成组合签名。</summary>
    public string signature;

    /// <summary>最近一次同步的金丹或元婴显化阶段。</summary>
    public int stage;

    /// <summary>按效果族稳定排序的当前授予定义。</summary>
    public CoreFormationGrantedEffect[] effects;
}
