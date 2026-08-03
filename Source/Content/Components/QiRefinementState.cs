using Cultiway.Core;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Components;

/// <summary>角色在炼气期形成且在后续境界永久归档的命名真气成果。</summary>
public struct QiRefinementState : IComponent
{
    /// <summary>真气的名称、品阶、强度、层数、组成、原子与谱系快照。</summary>
    public CoreFormationSnapshot formation;

    /// <summary>已经完成的真气凝练层数；尚未形成真气时为零。</summary>
    public readonly int CompletedLayers => formation.IsValid ? formation.refinement : 0;

    /// <summary>复制成果内部数组，避免传承后的角色共享可变快照。</summary>
    public readonly QiRefinementState DeepClone()
    {
        var clone = this;
        clone.formation = formation.DeepClone();
        return clone;
    }
}
