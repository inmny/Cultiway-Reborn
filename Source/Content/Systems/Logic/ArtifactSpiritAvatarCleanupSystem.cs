using System;
using System.Collections.Generic;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using strings;
using UnityEngine;

namespace Cultiway.Content.Systems.Logic;

/// <summary>根据法器持久状态和控制状态显化、维持或收回唯一的真实器灵生物。</summary>
public sealed class ArtifactSpiritAvatarCleanupSystem : QuerySystem<ActorBinder, ArtifactSpiritAvatar>
{
    private readonly List<Actor> expired = new();

    protected override void OnUpdate()
    {
        expired.Clear();
        Query.ForEachEntity((ref ActorBinder binder, ref ArtifactSpiritAvatar avatar, Entity _) =>
        {
            if (avatar.artifact.IsAvailable() && avatar.controller.IsAvailable()) return;
            avatar.recover_on_death = false;
            expired.Add(binder.Actor);
        });
        for (int i = 0; i < expired.Count; i++)
        {
            if (expired[i] != null && !expired[i].isRekt()) expired[i].dieAndDestroy(AttackType.None);
        }
    }
}
