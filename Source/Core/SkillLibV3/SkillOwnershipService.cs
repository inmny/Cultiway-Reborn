using Cultiway.Const;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Utils;
using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3;

public enum SkillOwnershipResult
{
    Added,
    Replaced,
    Forgotten,
    Duplicate,
    NotOwned,
    Invalid,
    Disabled
}

public static class SkillOwnershipService
{
    public static SkillOwnershipResult Learn(ActorExtend owner, Entity container, bool clone = false)
    {
        if (!GeneralSettings.EnableSkillSystems) return SkillOwnershipResult.Disabled;
        if (container.IsNull || !container.HasComponent<SkillContainer>()) return SkillOwnershipResult.Invalid;

        if (clone)
        {
            container = container.Store.CloneEntity(container);
            if (container.Tags.Has<TagOccupied>())
            {
                container.RemoveTag<TagOccupied>();
            }
            if (container.Tags.Has<TagRecycle>()) container.RemoveTag<TagRecycle>();
        }

        var signature = SkillContainerSignature.Build(container);
        foreach (var owned in owner.GetLearnedSkillsInOrder())
        {
            if (SkillContainerSignature.Build(owned) == signature)
            {
                RecycleIfUnmastered(container);
                return SkillOwnershipResult.Duplicate;
            }
        }

        if (container.Tags.Has<TagOccupied>()) container.RemoveTag<TagOccupied>();
        if (container.Tags.Has<TagRecycle>()) container.RemoveTag<TagRecycle>();
        owner.AttachLearnedSkill(container);
        return SkillOwnershipResult.Added;
    }

    public static SkillOwnershipResult Forget(ActorExtend owner, Entity container)
    {
        if (!owner.DetachLearnedSkill(container)) return SkillOwnershipResult.NotOwned;
        return SkillOwnershipResult.Forgotten;
    }

    public static SkillOwnershipResult Replace(ActorExtend owner, Entity oldContainer, Entity newContainer)
    {
        if (!GeneralSettings.EnableSkillSystems) return SkillOwnershipResult.Disabled;
        if (!owner.OwnsLearnedSkill(oldContainer)) return SkillOwnershipResult.NotOwned;
        if (newContainer.IsNull || !newContainer.HasComponent<SkillContainer>()) return SkillOwnershipResult.Invalid;

        var signature = SkillContainerSignature.Build(newContainer);
        foreach (var owned in owner.GetLearnedSkillsInOrder())
        {
            if (owned == oldContainer) continue;
            if (SkillContainerSignature.Build(owned) != signature) continue;
            RecycleIfUnmastered(newContainer);
            return SkillOwnershipResult.Duplicate;
        }

        if (newContainer.Tags.Has<TagOccupied>()) newContainer.RemoveTag<TagOccupied>();
        if (newContainer.Tags.Has<TagRecycle>()) newContainer.RemoveTag<TagRecycle>();
        if (!owner.ReplaceLearnedSkill(oldContainer, newContainer)) return SkillOwnershipResult.NotOwned;

        return SkillOwnershipResult.Replaced;
    }

    private static void RecycleIfUnmastered(Entity container)
    {
        if (container.GetIncomingLinks<SkillMasterRelation>().Count == 0)
        {
            if (container.Tags.Has<TagOccupied>()) container.RemoveTag<TagOccupied>();
            if (!container.Tags.Has<TagRecycle>()) container.AddTag<TagRecycle>();
        }
    }
}
