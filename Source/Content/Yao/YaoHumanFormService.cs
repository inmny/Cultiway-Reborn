using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Progression;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.YaoBeasts;

/// <summary>化形劫的挑战阶段：检验妖丹、灵智和身体重构。</summary>
public sealed class YaoHumanTransformationStage : IProgressionStage
{
    /// <summary>化形劫进行期间保持挑战在运行状态。</summary>
    public ProgressionGateResult Evaluate(ProgressionStageContext context)
    {
        if (!context.Actor.E.TryGetComponent(out Yao yao)) return ProgressionGateResult.Blocked("yao.not_yao");
        if (!string.IsNullOrEmpty(yao.CorePreparationPatternId))
            return ProgressionGateResult.Blocked("yao.core_preparing");

        // 化形劫沿袭天劫流程：给妖兽挂上过程组件并按波次结算。
        return YaoTribulationService.Evaluate(context.Actor);
    }

    /// <summary>开始化形劫。</summary>
    public void Start(ProgressionStageContext context)
    {
        YaoHumanFormService.TryStartTransformation(context.Actor);
    }
}

/// <summary>化形与人形方案的服务入口。</summary>
public static class YaoHumanFormService
{
    /// <summary>人形固定形态编号。</summary>
    public const string HumanMorphId = "yao.human.base";

    /// <summary>人形身体结构编号。</summary>
    public const string HumanBodyPlanId = "yao.human";

    private static readonly System.Collections.Generic.Dictionary<long, bool> transformationSucceeded = new();

    /// <summary>开始化形劫：化形劫沿用凝丹天劫的运行流程。</summary>
    public static bool TryStartTransformation(ActorExtend actor)
    {
        if (actor?.Base == null || actor.Base.isRekt() || !actor.HasCultisys<Yao>()) return false;
        ref Yao yao = ref actor.GetCultisys<Yao>();
        if (!string.IsNullOrEmpty(yao.CorePreparationPatternId)) return false;
        if (!actor.E.TryGetComponent(out YaoCore core) || core.Cracks > 0 || core.Quality < 40f) return false;

        // 化形劫不消耗凝丹资源，直接以妖丹稳定度对峙劫伤。
        actor.E.AddComponent(new YaoTribulation
        {
            TotalWaves = 3,
            CurrentWave = 1,
            StartedAt = YaoTime.Now,
            ExpiresAt = YaoTime.Now + 120f,
            NextStrikeAt = YaoTime.Now + 3f,
            RequiredDamageEvidence = actor.Base.getMaxHealth() * 0.35f,
            CoreIntegrity = 1f,
        });
        return true;
    }

    /// <summary>化形劫成功：建立人形方案、写入身体总表并切换活动形态。</summary>
    public static void CommitHumanForm(ActorExtend actor, ref Yao yao)
    {
        if (!actor.E.TryGetComponent(out YaoBody body)) return;
        if (body.TryGetForm(YaoFormIds.HumanForm, out _)) return;
        if (!body.TryGetActiveForm(out YaoFormRecord trueForm)) return;

        // 人形的总强度不能超过真身、境界和转换规则允许的上限：
        // 人形只保留兼容器官，属性由共用身体重新整理得出。
        var humanForm = new YaoFormRecord
        {
            FormId = YaoFormIds.HumanForm,
            Kind = YaoFormKind.HumanForm,
            BodyPlanId = HumanBodyPlanId,
            MorphId = HumanMorphId,
            Organs = RetainCompatibleOrgans(trueForm),
            RequiredRealm = 4,
            Cooldown = 60f,
        };

        var forms = new YaoFormRecord[body.Forms.Length + 1];
        body.Forms.CopyTo(forms, 0);
        forms[^1] = humanForm;
        body.Forms = forms;
        actor.E.GetComponent<YaoBody>() = body;

        // 化形成功只代表获得人形，不会自动加入城市、文化、宗门或人类文明。
        YaoFormPlanService.TrySwitchForm(actor, YaoFormIds.HumanForm);
        YaoWorldLog.HumanTransformation(actor);
    }

    /// <summary>人形保留兼容器官：按共用身体定义判断兼容性，不兼容的自然休眠。</summary>
    private static YaoOrganRecord[] RetainCompatibleOrgans(YaoFormRecord trueForm)
    {
        var retained = new System.Collections.Generic.List<YaoOrganRecord>();
        foreach (YaoOrganRecord organ in trueForm.Organs)
        {
            CreatureCompositions.Libraries.CreatureOrganAsset asset =
                Content.Libraries.Manager.CreatureOrganLibrary.get(organ.OrganId);
            CreatureCompositions.Libraries.CreatureBodyPlanAsset bodyPlan =
                Content.Libraries.Manager.CreatureBodyPlanLibrary.get(HumanBodyPlanId);
            CreatureCompositions.Libraries.CreatureMorphAsset morph =
                Content.Libraries.Manager.CreatureMorphLibrary.get(HumanMorphId);
            if (asset == null || bodyPlan == null || morph == null) continue;

            bool compatible =
                MatchesTags(asset.AllowedBodyPlanTags, bodyPlan.Tags) &&
                MatchesTags(asset.AllowedMorphTags, morph.Tags);
            if (compatible) retained.Add(organ);
        }

        return retained.ToArray();
    }

    private static bool MatchesTags(string[] allowedTags, string[] actualTags)
    {
        if (allowedTags == null || allowedTags.Length == 0) return true;
        if (actualTags == null || actualTags.Length == 0) return false;
        foreach (string allowed in allowedTags)
        {
            foreach (string actual in actualTags)
            {
                if (string.Equals(allowed, actual, System.StringComparison.Ordinal)) return true;
            }
        }

        return false;
    }

    /// <summary>清理世界时丢弃化形成败记录。</summary>
    public static void ClearWorldState()
    {
        transformationSucceeded.Clear();
    }
}
