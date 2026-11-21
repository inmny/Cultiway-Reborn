using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Persistence;
using Cultiway.Core.Semantics;
using Cultiway.Core.Skills;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>一次创作受理时固定的数值倾向；不持有人物、技能实体或图形资源。</summary>
public sealed class ConstructionInspiration
{
    /// <summary>角色已有资料形成的八维元素倾向。</summary>
    [JsonProperty(Required = Required.Always)]
    public readonly ElementComposition Elements;

    /// <summary>原版实际性格资料中的进攻倾向。</summary>
    [JsonProperty(Required = Required.Always)]
    public readonly float Aggression;

    /// <summary>原版理性及管理倾向形成的控制偏好。</summary>
    [JsonProperty(Required = Required.Always)]
    public readonly float Control;

    /// <summary>受理时真实身体是否支持既有掌部显化；第七版的其他实际部位由身体快照提供。</summary>
    [JsonProperty(Required = Required.Always)]
    public readonly bool CanProject;

    /// <summary>确定输入使用的生成规则，旧正式请求经迁移明确设为第一版。</summary>
    [JsonProperty(Required = Required.Always)]
    public readonly int Rules;

    /// <summary>固定可保存的创作输入，在读取边界拒绝损坏数值。</summary>
    /// <param name="elements">八维元素相对权重；空倾向允许自由探索。</param>
    /// <param name="aggression">零至一的进攻偏好。</param>
    /// <param name="control">零至一的控制偏好。</param>
    /// <param name="canProject">实际身体支持的动作条件。</param>
    /// <param name="rules">旧请求保留原规则，第三十五版接入实际近身战技。</param>
    [JsonConstructor]
    public ConstructionInspiration(ElementComposition elements, float aggression, float control, bool canProject,
        int rules = ConstructionPricing.MartialStudyRules)
    {
        for (int i = 0; i < ElementIndex.Count; i++)
            if (float.IsNaN(elements[i]) || float.IsInfinity(elements[i]) || elements[i] < 0f || elements[i] > 1.001f)
                throw new ArgumentException("创法元素倾向无效", nameof(elements));
        if (!Valid(aggression) || !Valid(control)) throw new ArgumentException("创法性格倾向需要零至一的有限数值");
        if (rules is not (1 or 2 or 3 or 4 or 5 or 6 or 7 or 8 or 9 or 10 or 11 or 12 or 13 or 14 or 15 or 16 or 17 or 18 or 19 or 20 or 21 or 22 or 23 or 24 or 25 or 26 or 27 or 28 or 29 or 30 or ConstructionPricing.ProcessTimeRules or ConstructionPricing.ApplicationRules or ConstructionPricing.PropagationRules or ConstructionPricing.DailyUseRules or ConstructionPricing.MartialStudyRules or ConstructionPricing.KnightStudyRules)) throw new ArgumentException("未知的创法生成规则", nameof(rules));
        Rules = rules;
        Elements = elements;
        Aggression = aggression;
        Control = control;
        CanProject = canProject;
    }

    /// <summary>从当前可用材料中带权选择，始终为其他选择保留探索机会。</summary>
    /// <param name="random">本次固定输入的随机源。</param>
    internal string Matter(System.Random random)
    {
        int selected = Choose(random, 1f + 4f * Elements.iron, 1f + 4f * Elements.wood,
            1f + 4f * Elements.water, 1f + 4f * Elements.fire, 1f + 3f * Elements.water + 2f * Elements.neg);
        return selected switch { 0 => "iron", 1 => "wood", 2 => "water", 3 => "fire", _ => "ice" };
    }

    /// <summary>从现有结构方向选择，不把某一种灵根变成唯一固定整招。</summary>
    /// <param name="random">本次固定输入的随机源。</param>
    internal int Family(System.Random random) => Choose(random,
        1.5f + Elements.water * 5f + Elements.fire * 3f,
        CanProject ? 1.5f + Elements.iron * 3f + Aggression * 3f : 0f,
        2.5f + Elements.wood * 5f + Control * 2f);

    /// <summary>有限个选项共享确定的累计权重选择。</summary>
    /// <param name="random">本次随机源。</param>
    /// <param name="weights">各实际可用选项的非负权重。</param>
    private static int Choose(System.Random random, params float[] weights)
    {
        double point = random.NextDouble() * weights.Sum();
        for (int i = 0; i < weights.Length - 1; i++)
        {
            point -= weights[i];
            if (point < 0d) return i;
        }

        return weights.Length - 1;
    }

    /// <summary>只在外部读取边界检查比例输入。</summary>
    /// <param name="value">待检查的比例。</param>
    private static bool Valid(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f && value <= 1f;
}

/// <summary>新构造实际付费开始后的有限使用记录，每次施放只记录一次。</summary>
public struct ConstructedUseHistory : IComponent
{
    /// <summary>最近与长期实际使用共同形成的平均元素组成。</summary>
    public ElementComposition Elements;

    /// <summary>用于形成平均值和经验强度的使用数量，最多4096。</summary>
    public int Uses;
}

/// <summary>把既有角色语义和实际使用经历接入共同创作。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>按已有贡献器读取灵根、功法、知识、经历，再读取原版实际性格。</summary>
    /// <param name="actor">需要创作的实际生灵。</param>
    public static ConstructionInspiration ReadInspiration(ActorExtend actor) =>
        ReadInspirationForRules(actor, ConstructionPricing.MartialStudyRules);

    /// <summary>在受理时按创作来源固定所用规则。</summary>
    private static ConstructionInspiration ReadInspirationForRules(ActorExtend actor, int rules)
    {
        if (actor?.Base?.data == null) throw new ArgumentException("缺少真实创造者", nameof(actor));
        SemanticProfile profile = SemanticContributorService.Build(actor);
        var elements = new ElementComposition();
        float total = 0f;
        for (int i = 0; i < ElementIndex.Count; i++)
        {
            float evidence = Mathf.Max(0f,
                profile.GetScore(ElementSemanticProfileService.GetIndexedSemantic(i), SemanticQueryPolicy.Default).Net);
            elements[i] = 2f * (1f - Mathf.Exp(-evidence / 2f));
            total += elements[i];
        }

        if (total > 0f)
            for (int i = 0; i < ElementIndex.Count; i++)
                elements[i] /= total;
        float offensive = Mathf.Max(0f,
            profile.GetScore(SkillSemantics.Role.Offensive, SemanticQueryPolicy.Default).Net);
        float controlEvidence = Mathf.Max(0f,
            profile.GetScore(SkillSemantics.Effect.Control, SemanticQueryPolicy.Default).Net);
        float aggression =
            Mathf.Clamp01(actor.Base.stats["personality_aggression"] + .2f * (1f - Mathf.Exp(-offensive)));
        float control = Mathf.Clamp01(.5f + actor.Base.stats["personality_rationality"] * .5f +
                                      actor.Base.stats["personality_administration"] * .4f - aggression * .2f +
                                      .15f * (1f - Mathf.Exp(-controlEvidence)));
        bool project = BodyActionBinding.CanBind(actor, AbilityCompiler.Compile(BodyActionCatalog.Palm).Body);
        return new ConstructionInspiration(elements, aggression, control, project, rules);
    }

    /// <summary>登记共同语义贡献和使用事实；画像自身按需重建，不复制到人物长期资料。</summary>
    private static void InitializeInspiration()
    {
        SemanticContributorService.Register(new ConstructedInspirationContributor());
        ModClass.I.WorldSaves.Actors.Register(new SaveSection<ActorExtend, InspirationHistorySave>(
            "ability_use_history", "构造法术使用经历", 1,
            actor => actor.E.TryGetComponent(out ConstructedUseHistory state)
                ? new InspirationHistorySave { Uses = state.Uses, Elements = state.Elements }
                : null,
            (actor, data) =>
            {
                if (data.Uses > 0)
                    actor.E.AddComponent(new ConstructedUseHistory { Uses = data.Uses, Elements = data.Elements });
            }, data =>
            {
                if (data.Uses < 0 || data.Uses > 4096) throw new InvalidDataException("构造使用数量无效");
                for (int i = 0; i < ElementIndex.Count; i++)
                    if (float.IsNaN(data.Elements[i]) || float.IsInfinity(data.Elements[i]) || data.Elements[i] < 0f ||
                        data.Elements[i] > 1.001f)
                        throw new InvalidDataException("构造使用元素组成无效");
            }));
    }

    /// <summary>实际使用事实的正式保存，不为没有使用记录的人物创建空区段。</summary>
    private sealed class InspirationHistorySave
    {
        /// <summary>平均材料元素组成。</summary>
        public ElementComposition Elements;

        /// <summary>有限使用数量。</summary>
        public int Uses;
    }

    /// <summary>真正开始并支付后的施放才积累经历，不按投射物、命中或显示帧增加。</summary>
    /// <param name="actor">实际付款并施放的人物。</param>
    /// <param name="plan">该次使用的确定构造。</param>
    private static void RecordInspirationUse(ActorExtend actor, CompiledAbility plan)
    {
        ElementComposition elements = PlanElements(plan);
        actor.E.TryGetComponent(out ConstructedUseHistory state);
        state.Uses = Math.Min(4096, state.Uses + 1);
        for (int i = 0; i < ElementIndex.Count; i++)
            state.Elements[i] += (elements[i] - state.Elements[i]) / state.Uses;
        actor.E.AddComponent(state);
    }

    /// <summary>不同部位和物态共用计划里的真实材料，不读取显示染色。</summary>
    /// <param name="plan">当前确定计划。</param>
    private static ElementComposition PlanElements(CompiledAbility plan)
    {
        var value = new ElementComposition();
        foreach (string matter in plan.Matters)
        {
            ElementComposition material = Composition(matter);
            for (int i = 0; i < ElementIndex.Count; i++) value[i] += Mathf.Max(0f, material[i]);
        }

        value.Normalize();
        return value;
    }

    /// <summary>补充新构造知识、真实构造使用及已有魔法使用记录，不重复实现旧贡献器。</summary>
    private sealed class ConstructedInspirationContributor : IActorSemanticContributor
    {
        /// <summary>稳定贡献编号。</summary>
        public string Id => "content.constructed_inspiration";

        /// <summary>在既有角色资料之后补充。</summary>
        public int Priority => 650;

        /// <summary>只读取确定的知识和使用事实，不因采样增加熟练或改写资料。</summary>
        /// <param name="actor">实际生灵。</param>
        /// <param name="builder">共同语义资料构建器。</param>
        public void Contribute(ActorExtend actor, SemanticProfileBuilder builder)
        {
            if (actor.E.TryGetComponent(out ConstructedKnowledge known) && known.Ids.Length > 0)
                foreach (string id in known.Ids)
                    if (definitions.ContainsKey(id))
                        ElementSemanticProfileService.Contribute(builder, PlanElements(PlanFor(id)),
                            1f / known.Ids.Length,
                            SemanticScope.Learned, new SemanticSourceRef(Id, id));
            if (actor.E.TryGetComponent(out ConstructedUseHistory history) && history.Uses > 0)
                ElementSemanticProfileService.Contribute(builder, history.Elements,
                    3f * (1f - Mathf.Exp(-history.Uses / 20f)),
                    SemanticScope.Historical, new SemanticSourceRef(Id + ".used"));
            foreach (MagicSpellKnowledgeRelation use in actor.E.GetRelations<MagicSpellKnowledgeRelation>())
                if (use.TotalCastCount > 0 && !use.SkillContainer.IsNull && actor.OwnsLearnedSkill(use.SkillContainer))
                    SkillSemanticCollector.ContributeProfile(use.SkillContainer, builder,
                        3f * (1f - Mathf.Exp(-Mathf.Min(use.TotalCastCount, 4096) / 20f)),
                        SemanticScope.Historical, Id + ".magic_use");
        }
    }
}

/// <summary>旧请求没有保存创作倾向，只取消尚未产生正式结果的请求，保留种子、等待和已有能力。</summary>
internal sealed class CreationV1ToV2 : ISaveMigration
{
    /// <summary>首版请求资料。</summary>
    public int FromVersion => 1;

    /// <summary>不倒推旧档缺失的输入；让恢复报告说明这次局部取消。</summary>
    /// <param name="data">保存层提供的旧资料副本。</param>
    public JObject Apply(JObject data)
    {
        if ((bool?)data["Pending"] == true)
        {
            data["Pending"] = false;
            data["DiscardedPending"] = true;
            data["LastFailure"] = "创法规则升级：旧请求没有保存倾向输入，本次未完成创作取消，已有法术保留";
        }

        data["Inspiration"] = JValue.CreateNull();
        return data;
    }
}
