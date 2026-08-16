using System;
using Friflo.Engine.ECS;

namespace Cultiway.Core.SubWorlds.Objects;

/// <summary>Actor 与 Building 共享的视觉阶段和稳定变体选择。</summary>
internal struct SubWorldVisual : IComponent
{
    internal SubWorldVisual(int variantIndex = 0, SubWorldVisualState state = SubWorldVisualState.Default)
    {
        if (variantIndex < 0) throw new ArgumentOutOfRangeException(nameof(variantIndex));
        VariantIndex = variantIndex;
        State = state;
    }

    internal int VariantIndex;
    internal SubWorldVisualState State;
}

internal enum SubWorldVisualState : byte
{
    Default,
    Ruin,
    Disabled,
    Special
}
