using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Artifacts;

/// <summary>
/// 法器能力的统一查询与同步事件入口。领域系统只发布自己的上下文，不感知具体法器能力类型。
/// </summary>
public static class ArtifactAbilityDispatcher
{
    /// <summary>
    /// 将领域事件依次交给驾驭者全部已装备法器中注册了该事件类型的能力。
    /// </summary>
    /// <returns>实际执行的能力处理器数量。</returns>
    public static int Dispatch<TEvent>(Entity controller, TEvent evt) where TEvent : class
    {
        int handled = 0;
        var relations = controller.GetRelations<EquippedArtifactRelation>();
        for (int i = 0; i < relations.Length; i++)
        {
            EquippedArtifactRelation relation = relations[i];
            Entity artifact = relation.artifact;
            if (!artifact.IsAvailable()) continue;
            if (!artifact.TryGetComponent(out ArtifactAbilitySet abilitySet)) continue;

            ref ArtifactAbilityRuntime runtime = ref artifact.GetComponent<ArtifactAbilityRuntime>();
            ArtifactAbilityExecutionContext context = new(controller, artifact, relation.state);
            for (int j = 0; j < abilitySet.abilities.Length; j++)
            {
                ArtifactAbilityInstance ability = abilitySet.abilities[j];
                ArtifactAbilityAsset asset = Cultiway.Content.Libraries.Manager.ArtifactAbilityLibrary.get(
                    ability.ability_id);
                if (asset == null) continue;
                if (asset.TryHandle(context, ability, ref runtime.abilities[j], evt)) handled++;
            }
        }
        return handled;
    }

    /// <summary>
    /// 即时汇总法器所有能力向自动调度器声明的用途权重。
    /// </summary>
    public static ArtifactUseProfile ResolveUseProfile(Entity artifact)
    {
        ArtifactUseProfile result = default;
        if (!artifact.TryGetComponent(out ArtifactAbilitySet abilitySet)) return result;

        for (int i = 0; i < abilitySet.abilities.Length; i++)
        {
            ArtifactAbilityAsset asset = Cultiway.Content.Libraries.Manager.ArtifactAbilityLibrary.get(
                abilitySet.abilities[i].ability_id);
            if (asset == null) continue;
            result.offensive += asset.use_profile.offensive;
            result.defensive += asset.use_profile.defensive;
            result.support += asset.use_profile.support;
            result.cultivate += asset.use_profile.cultivate;
            result.production += asset.use_profile.production;
        }
        return result;
    }

    /// <summary>
    /// 汇总能力为法器基础操控档案增加的复杂度与分念需求。
    /// </summary>
    public static void ResolveControlContribution(
        Entity artifact,
        out float complexity,
        out int threadCost)
    {
        complexity = 0f;
        threadCost = 0;
        if (!artifact.TryGetComponent(out ArtifactAbilitySet abilitySet)) return;

        for (int i = 0; i < abilitySet.abilities.Length; i++)
        {
            ArtifactAbilityAsset asset = Cultiway.Content.Libraries.Manager.ArtifactAbilityLibrary.get(
                abilitySet.abilities[i].ability_id);
            if (asset == null) continue;
            complexity += asset.control_complexity;
            threadCost += asset.thread_cost;
        }
    }

    /// <summary>
    /// 判断法器是否拥有带指定语义标签的能力。
    /// </summary>
    public static bool HasAbilityTag(Entity artifact, string tag)
    {
        if (!artifact.TryGetComponent(out ArtifactAbilitySet abilitySet)) return false;
        for (int i = 0; i < abilitySet.abilities.Length; i++)
        {
            ArtifactAbilityAsset asset = Cultiway.Content.Libraries.Manager.ArtifactAbilityLibrary.get(
                abilitySet.abilities[i].ability_id);
            if (asset != null && asset.HasTag(tag)) return true;
        }
        return false;
    }
}
