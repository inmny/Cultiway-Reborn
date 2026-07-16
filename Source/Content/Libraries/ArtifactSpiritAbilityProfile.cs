using System;
using Cultiway.Content.Components;
using Cultiway.Core;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Libraries;

/// <summary>能力显化持久器灵实体时使用的战斗参数。</summary>
public sealed class ArtifactSpiritAbilityProfile
{
    public ArtifactControlState minimum_state = ArtifactControlState.Ready;
    public Func<ArtifactAbilityInstance, ArtifactSpiritState, float> ResolveDamageRatio;
    public Func<ArtifactAbilityInstance, ArtifactSpiritState, float> ResolveHealthRatio;
    public Func<ArtifactAbilityInstance, ArtifactSpiritState, float> ResolveArmorBonus;
    public Func<ArtifactAbilityInstance, ArtifactSpiritState, float> ResolveRecoveryDuration;
}
