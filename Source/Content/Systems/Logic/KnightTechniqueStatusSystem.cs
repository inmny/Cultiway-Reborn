using System.Collections.Generic;
using Cultiway.Content.Components;
using Cultiway.Content.KnightCombat;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Systems.Logic;

/// <summary>换装、死亡或强控后立即移除绑定武器的骑士架势。</summary>
internal sealed class KnightTechniqueStatusSystem : QuerySystem<StatusComponent, KnightBoundWeaponStatus>
{
    private readonly List<InvalidStatus> invalid = new();

    public KnightTechniqueStatusSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab, TagInactive, TagRecycle>());
    }

    protected override void OnUpdate()
    {
        invalid.Clear();
        Query.ForEachEntity((ref StatusComponent _, ref KnightBoundWeaponStatus bound, Entity status) =>
        {
            Actor owner = null;
            foreach (Entity ownerEntity in status.GetIncomingLinks<StatusRelation>().Entities)
            {
                if (!ownerEntity.TryGetComponent(out ActorBinder binder)) continue;
                owner = binder.Actor;
                break;
            }
            if (owner != null && owner.TryGetExtend(out var extend) &&
                KnightTechniqueRuntimeService.IsCurrentTechniqueWeapon(extend, bound.Technique, bound.Weapon))
                return;
            invalid.Add(new InvalidStatus(owner, status));
        });

        for (var i = 0; i < invalid.Count; i++)
        {
            InvalidStatus item = invalid[i];
            if (item.Owner != null && !item.Owner.isRekt()) item.Owner.GetExtend().RemoveSharedStatus(item.Status);
            ModClass.I.CommandBuffer.AddTag<TagRecycle>(item.Status.Id);
        }
    }

    private readonly struct InvalidStatus
    {
        public readonly Actor Owner;
        public readonly Entity Status;

        public InvalidStatus(Actor owner, Entity status)
        {
            Owner = owner;
            Status = status;
        }
    }
}
