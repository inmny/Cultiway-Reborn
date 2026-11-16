using System.Collections.Generic;
using Cultiway.Core.EventSystem.Systems;
using Cultiway.Core.Skills;

namespace Cultiway.Content.Skills;

public sealed partial class ConstructedAbilityService
{
    /// <summary>只在真实命中后抽取至多一个候选，未触发时不抽取候选编号。</summary>
    /// <param name="choice">已经编译的候选资料。</param><param name="result">真实接触结果。</param>
    private static CompiledConstructionContactOutcome SelectContactOutcome(CompiledConstructionRandomContact choice, ApplyAttackState result)
    {
        if (choice?.HasEffect != true || result != ApplyAttackState.Hit || choice.Chance < 1f && !Randy.randomChance(choice.Chance)) return null;
        return choice.Outcomes[Randy.randomInt(0, choice.Outcomes.Count)];
    }

    /// <summary>按顺序完成同次追加伤害，每次回调后核对世界和目标，避免向池中替身继续作用。</summary>
    /// <param name="running">本次真实施放。</param><param name="node">实际接触部位。</param>
    /// <param name="target">真实人物。</param><param name="targetId">接触时确认的人物编号。</param>
    /// <param name="effects">已经编译的伤害资料。</param><param name="strength">接触位置或传播路程的余力倍率。</param>
    /// <param name="attempted">是否实际向共同受击入口提交了至少一份伤害。</param>
    private static bool ApplyAdditionalDamage(RunningCast running, int node, Actor target, long targetId,
        IReadOnlyList<CompiledConstructionContactDamage> effects, float strength, out bool attempted)
    {
        attempted = false;
        if (effects == null || effects.Count == 0) return true;
        float force = (node < 0 ? running.Power : running.Cast.RemainingForce(node)) * strength;
        if (force <= 0f) return true;
        long generation = worldGeneration;
        foreach (CompiledConstructionContactDamage effect in effects)
        {
            var damage = running.Owner(node).Damage;
            damage.TargetID = targetId;
            damage.Damage = force * effect.PerForce;
            damage.Element = effect.Element;
            attempted = true;
            GetHitEventSystem.Resolve(damage);
            if (generation != worldGeneration || target.data?.id != targetId || !target.isAlive() || !target.hasHealth()) return false;
        }
        return true;
    }
}
