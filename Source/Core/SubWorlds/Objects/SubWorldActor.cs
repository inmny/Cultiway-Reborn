using System;
using Friflo.Engine.ECS;

namespace Cultiway.Core.SubWorlds.Objects;

/// <summary>声明一个 Entity 属于小世界 Actor 类别，并引用原版 ActorAsset。</summary>
internal struct SubWorldActor : IComponent
{
    internal SubWorldActor(string actorAssetId)
    {
        if (string.IsNullOrWhiteSpace(actorAssetId))
            throw new ArgumentException("ActorAsset ID 为空", nameof(actorAssetId));
        ActorAssetId = actorAssetId;
    }

    internal string ActorAssetId;
}
