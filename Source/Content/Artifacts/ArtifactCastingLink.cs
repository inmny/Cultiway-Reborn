using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Artifacts;

/// <summary>一次真正绑定到法宝的召唤或器灵，供施法前与维持中校验原关系。</summary>
internal readonly struct ArtifactCastingLink
{
    /// <summary>借用载体的主人。</summary>
    internal readonly ActorExtend Owner;

    /// <summary>真正出手的临时人物。</summary>
    private readonly ActorExtend carrier;

    /// <summary>维持该人物的原法宝。</summary>
    private readonly Entity artifact;

    /// <summary>原法宝的具体能力实例。</summary>
    private readonly string instance;

    /// <summary>冻结本次依附的原载体与关系。</summary>
    /// <param name="owner">原法宝主人。</param>
    /// <param name="carrier">实际载体。</param>
    /// <param name="artifact">原法宝。</param>
    /// <param name="instance">原能力实例。</param>
    private ArtifactCastingLink(ActorExtend owner, ActorExtend carrier, Entity artifact, string instance)
    {
        Owner = owner;
        this.carrier = carrier;
        this.artifact = artifact;
        this.instance = instance;
    }

    /// <summary>识别由法宝活动维持的人物，野生同种生物仍是独立人物。</summary>
    /// <param name="actor">实际请求者。</param>
    internal static bool IsManifestation(ActorExtend actor) =>
        actor.HasComponent<ArtifactSummon>() || actor.HasComponent<ArtifactSpiritAvatar>();

    /// <summary>失效后立即拒绝全部主动入口，存续期间自己的能力仍用自己的身份。</summary>
    /// <param name="requested">实际请求者。</param>
    internal static SkillCasterContext? ResolveOwnContext(ActorExtend requested) =>
        requested.Base != null && !requested.Base.isRekt() && IsManifestation(requested) &&
        !TryResolve(requested, out _) ? default(SkillCasterContext) : null;

    /// <summary>同一法宝的能力或主人改变后，旧法术不能跟随替身。</summary>
    internal bool IsCurrent() => TryResolve(carrier, out ArtifactCastingLink current) &&
        current.Owner == Owner && current.artifact == artifact && current.instance == instance;

    /// <summary>读取活着、在原法宝有效活动中的实际绑定。</summary>
    /// <param name="carrier">实际载体。</param>
    /// <param name="link">有效的原绑定。</param>
    internal static bool TryResolve(ActorExtend carrier, out ArtifactCastingLink link)
    {
        link = default;
        Actor actor = carrier.Base;
        if (actor == null || actor.isRekt() || !actor.isAlive() || !actor.hasHealth()) return false;
        if (carrier.TryGetComponent(out ArtifactSummon summon))
        {
            if (World.world.getCurWorldTime() >= summon.expires_at || !ArtifactSummonService.IsMaintained(summon) ||
                !TryGetEquippedOwner(summon.controller, summon.artifact, out ActorExtend summoner)) return false;
            link = new ArtifactCastingLink(summoner, carrier, summon.artifact, summon.ability_instance_id);
            return true;
        }

        if (!carrier.TryGetComponent(out ArtifactSpiritAvatar avatar) ||
            !TryGetEquippedOwner(avatar.controller, avatar.artifact, out ActorExtend owner) ||
            !avatar.artifact.TryGetComponent(out ArtifactSpiritState spirit) || !spirit.awakened ||
            !avatar.artifact.TryGetComponent(out ArtifactAbilityRuntime runtime) || !runtime.attached ||
            runtime.controller != avatar.controller ||
            !avatar.artifact.TryGetComponent(out ArtifactAbilitySet abilities)) return false;
        var relations = avatar.artifact.GetRelations<ArtifactSpiritAvatarRelation>();
        bool present = false;
        for (int i = 0; i < relations.Length && !present; i++) present = relations[i].avatar == carrier.E;
        if (!present) return false;
        foreach (ArtifactAbilityInstance entry in abilities.abilities)
        {
            if (entry.instance_id != avatar.ability_instance_id) continue;
            var profile = Libraries.Manager.ArtifactAbilityLibrary.get(entry.ability_id)?.spirit_use;
            if (profile == null || !ArtifactAbilityLifecycle.MeetsState(runtime.control_state, profile.minimum_state))
                return false;
            link = new ArtifactCastingLink(owner, carrier, avatar.artifact, avatar.ability_instance_id);
            return true;
        }
        return false;
    }

    /// <summary>使用实时装备关系核对原主人，及时处理尚未轮到清理的卸装。</summary>
    /// <param name="controller">原活动记录中的主人。</param>
    /// <param name="item">原法宝。</param>
    /// <param name="owner">当前仍拥有该装备关系的活人。</param>
    private static bool TryGetEquippedOwner(Entity controller, Entity item, out ActorExtend owner)
    {
        owner = null;
        if (!item.IsAvailable() || !controller.IsAvailable() || !controller.TryGetComponent(out ActorBinder binder) ||
            binder.Actor == null || binder.Actor.isRekt() || !binder.Actor.isAlive()) return false;
        var relations = controller.GetRelations<EquippedArtifactRelation>();
        for (int i = 0; i < relations.Length; i++)
        {
            if (relations[i].artifact != item) continue;
            owner = binder.AE;
            return true;
        }
        return false;
    }
}
