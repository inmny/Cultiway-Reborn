using Cultiway.Content.AIGC;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Extensions;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Libraries;
using Cultiway.Core.Progression;
using Cultiway.Utils;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>仙道元婴境界的结婴、奖励、同步与传承规则。</summary>
public partial class Cultisyses
{
    /// <summary>继承金丹组合并根据结婴时的神识、功法与语义生成元婴蜕变。</summary>
    private static ProgressionResolution ResolveYuanying(ActorExtend actor, CultisysAsset<Xian> cultisys,
                                                          ref Xian component)
    {
        Jindan jindan = actor.GetComponent<Jindan>();
        XianBase xianBase = actor.TryGetComponent(out XianBase existing) ? existing : default;
        CoreFormationSnapshot formation = CoreFormationComposer.ComposeYuanying(
            actor, xianBase, jindan.formation, jindan.stage, jindan.strength);
        return ProgressionResolution.Success(new YuanyingPayload(formation, jindan.formation.DeepClone(),
            jindan.stage, xianBase.GetStrength(), jindan.strength));
    }

    /// <summary>直接结婴仍要求已有九转金丹；满足时复用正常的继承与蜕变组合。</summary>
    private static ProgressionResolution ResolveGrantedYuanying(ActorExtend actor,
        CultisysAsset<Xian> cultisys, ref Xian component)
    {
        return actor.HasComponent<Jindan>()
            ? ResolveYuanying(actor, cultisys, ref component)
            : ProgressionResolution.Failure(reason: "xian.jindan_missing");
    }

    /// <summary>创建或替换元婴组件，并保留金丹作为只读谱系归档。</summary>
    private static void ApplyYuanyingTransformation(ActorExtend actor, CultisysAsset<Xian> cultisys,
                                                     ref Xian component, object payload)
    {
        var data = (YuanyingPayload)payload;
        ref Yuanying yuanying = ref actor.GetOrAddComponent<Yuanying>();
        yuanying = new Yuanying(data.Formation, data.SourceJindan, data.JindanStage, data.JindanStrength);
        actor.MarkSemanticProfileDirty();
    }

    /// <summary>结婴后记录统计、处理植物命名，并学习元婴组合的代表法术。</summary>
    private static void ApplyYuanyingReward(ActorExtend actor, CultisysAsset<Xian> cultisys,
                                            ref Xian component, object payload)
    {
        var data = (YuanyingPayload)payload;
        if (!actor.Base.hasTrait(WorldboxGame.ActorTraits.ScarOfDivinity.id))
        {
            PersistentLogger.Get("JindanStats.log").Log(
                $"{data.JindanStage}, {data.FoundationStrength}, {data.JindanStrength}");
        }

        if (actor.Base.asset == Actors.Plant)
        {
            string rootName = actor.HasElementRoot() ? actor.GetElementRoot().Type.GetName() : null;
            PlantNameGenerator.Instance.NewNameGenerateRequest(
                GetPlantNameParams(actor, cultisys.GetLevelName(XianLevels.Yuanying), rootName,
                    data.Formation.canonical_name), actor.Base);
        }

        GrantRepresentativeSkill(actor, data.Formation);
    }

    /// <summary>同步到元婴境界时补齐完整前置谱系，并保留金丹归档。</summary>
    private static void NormalizeYuanyingRealm(ActorExtend actor, CultisysAsset<Xian> cultisys,
                                                ref Xian component, object payload)
    {
        XianBase xianBaseValue = CompleteFoundationForGrant(actor);
        ref XianBase xianBase = ref actor.GetOrAddComponent<XianBase>();
        xianBase = xianBaseValue;

        if (!actor.HasComponent<Yuanying>())
        {
            float strength = xianBaseValue.GetStrength();
            CoreFormationSnapshot jindanFormation;
            int jindanStage = YuanyingRequiredJindanStage;
            if (actor.HasComponent<Jindan>())
            {
                ref Jindan jindan = ref actor.GetComponent<Jindan>();
                strength = jindan.strength;
                jindanStage = Mathf.Max(jindan.stage, YuanyingRequiredJindanStage);
                jindanFormation = jindan.formation.DeepClone();
            }
            else
            {
                jindanFormation = CoreFormationComposer.ComposeJindan(actor, xianBaseValue, strength);
                CoreFormationComposer.EvolveJindan(ref jindanFormation, 0, YuanyingRequiredJindanStage);
                actor.AddComponent(new Jindan(jindanFormation, strength));
                actor.GetComponent<Jindan>().stage = YuanyingRequiredJindanStage;
            }

            CoreFormationSnapshot formation = CoreFormationComposer.ComposeYuanying(
                actor, xianBaseValue, jindanFormation, jindanStage, strength);
            actor.AddComponent(new Yuanying(formation, jindanFormation, jindanStage, strength));
        }
        actor.MarkSemanticProfileDirty();
    }

    /// <summary>将来源元婴及其数组快照深拷贝给目标；来源没有元婴时同步移除目标元婴。</summary>
    private static void TransferYuanying(ActorExtend source, ActorExtend target)
    {
        if (source.HasComponent<Yuanying>())
        {
            Yuanying component = source.GetComponent<Yuanying>();
            component.formation = component.formation.DeepClone();
            ref Yuanying targetComponent = ref target.GetOrAddComponent<Yuanying>();
            targetComponent = component;
        }
        else if (target.HasComponent<Yuanying>())
        {
            target.E.RemoveComponent<Yuanying>();
        }
    }

    /// <summary>结婴判定后传给结构变换、统计和奖励阶段的不可变数据。</summary>
    private sealed class YuanyingPayload
    {
        /// <summary>固化元婴组合、来源金丹谱系、继承转数及结婴前强度数据。</summary>
        public YuanyingPayload(CoreFormationSnapshot formation, CoreFormationSnapshot sourceJindan,
                               int jindanStage, float foundationStrength, float jindanStrength)
        {
            Formation = formation;
            SourceJindan = sourceJindan;
            JindanStage = jindanStage;
            FoundationStrength = foundationStrength;
            JindanStrength = jindanStrength;
        }

        /// <summary>继承金丹并加入结婴蜕变后的元婴快照。</summary>
        public CoreFormationSnapshot Formation { get; }

        /// <summary>结婴前金丹的深拷贝，用于保存谱系。</summary>
        public CoreFormationSnapshot SourceJindan { get; }

        /// <summary>结婴前金丹已经完成的淬炼层数，仅用于统计和后续奖励。</summary>
        public int JindanStage { get; }

        /// <summary>结婴前筑基结构的综合强度，仅用于统计。</summary>
        public float FoundationStrength { get; }

        /// <summary>结婴前最终金丹强度，同时作为新元婴的初始强度。</summary>
        public float JindanStrength { get; }
    }
}
