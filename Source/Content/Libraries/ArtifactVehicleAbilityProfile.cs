using System;
using Cultiway.Content.Components;
using Cultiway.Core;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Libraries;

/// <summary>能力向角色移动系统提供的御器载运参数。</summary>
public sealed class ArtifactVehicleAbilityProfile
{
    public ArtifactControlState minimum_state = ArtifactControlState.Operating;
    public Func<ArtifactAbilityInstance, float> ResolveSpeedMultiplier;
    public Func<ArtifactAbilityInstance, int> ResolvePassengerCapacity;
}
