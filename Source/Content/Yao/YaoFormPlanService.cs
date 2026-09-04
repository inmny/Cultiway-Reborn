using System;
using Cultiway.Content.Components;
using Cultiway.Content.CreatureCompositions.Components;
using Cultiway.Content.CreatureCompositions.Models;
using Cultiway.Content.CreatureCompositions.Services;
using Cultiway.Core;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.YaoBeasts;

/// <summary>
///     妖兽永久身体方案 <see cref="YaoBody" /> 的唯一写入者。
///     任何器官、形态变化都先写入身体总表，再由本服务把活动形态交给共用表达服务。
/// </summary>
public static class YaoFormPlanService
{
    /// <summary>把形态方案转换成共用身体整理器接受的不可变计划。</summary>
    public static CreaturePhenotypePlan CreatePlan(YaoFormRecord form)
    {
        var organs = new CreatureOrganEntry[form.Organs?.Length ?? 0];
        for (int i = 0; i < organs.Length; i++)
        {
            organs[i] = new CreatureOrganEntry(form.Organs[i].SlotId, form.Organs[i].OrganId, form.Organs[i].Rank);
        }

        return new CreaturePhenotypePlan(form.BodyPlanId, form.MorphId, organs);
    }

    /// <summary>为刚启灵的妖兽建立只含一个真身的身体总表，并让真身立即生效。</summary>
    /// <param name="actor">目标妖兽。</param>
    /// <param name="bodyPlanId">真身身体结构。</param>
    /// <param name="morphId">真身固定形态。</param>
    /// <param name="innateOrgans">先天器官列表；每项都带槽位、器官、等级。</param>
    public static bool TryCreateTrueForm(
        ActorExtend actor, string bodyPlanId, string morphId, params YaoOrganRecord[] innateOrgans)
    {
        if (actor?.Base == null || actor.Base.isRekt()) return false;

        var form = new YaoFormRecord
        {
            FormId = YaoFormIds.TrueForm,
            Kind = YaoFormKind.TrueForm,
            BodyPlanId = bodyPlanId,
            MorphId = morphId,
            Organs = innateOrgans ?? Array.Empty<YaoOrganRecord>(),
            RequiredRealm = 0,
            Cooldown = 0f,
        };
        var body = new YaoBody
        {
            Forms = new[] { form },
            ActiveFormId = YaoFormIds.TrueForm,
            LastSwitchAt = YaoTime.Now,
            LockedUntil = 0f,
        };

        actor.E.AddComponent(body);
        return ExpressActiveForm(actor);
    }

    /// <summary>把身体总表中的活动形态提交给共用表达服务；身体规则不允许时保持原样。</summary>
    public static bool ExpressActiveForm(ActorExtend actor)
    {
        if (actor?.Base == null || actor.Base.isRekt()) return false;
        if (!actor.E.TryGetComponent(out YaoBody body)) return false;
        if (!body.TryGetActiveForm(out YaoFormRecord form)) return false;

        bool expressed = CreatureExpressionService.TryExpress(
            actor, CreatePlan(form), out _, out string failReason);
        if (!expressed) ModClass.LogWarning($"妖兽身体表达失败: {failReason}");
        return expressed;
    }

    /// <summary>向指定形态写入一个器官，并同步到全部兼容的其他形态后重新表达。</summary>
    public static bool TryAddOrgan(
        ActorExtend actor, string formId, string slotId, string organId, int rank, YaoOrganOrigin origin)
    {
        if (!actor.E.TryGetComponent(out YaoBody body)) return false;
        if (!body.TryGetForm(formId, out YaoFormRecord form)) return false;
        if (HasOrgan(form, slotId, organId)) return false;

        // 先在活动副本上试排，确认共用身体规则允许后再写回总表。
        YaoOrganRecord[] updated = AppendOrgan(form, slotId, organId, rank, origin);
        if (!CreaturePhenotypeCompiler.TryGetOrCompile(
                CreatePlan(WithOrgans(form, updated)), out _))
            return false;

        CommitOrgans(actor, ref body, formId, updated);
        return ExpressActiveForm(actor);
    }

    /// <summary>替换指定槽位上的器官；旧器官效果随共用表达自动撤销。</summary>
    public static bool TryReplaceOrgan(
        ActorExtend actor, string formId, string slotId, string organId, int rank, YaoOrganOrigin origin)
    {
        if (!actor.E.TryGetComponent(out YaoBody body)) return false;
        if (!body.TryGetForm(formId, out YaoFormRecord form)) return false;

        YaoOrganRecord[] updated = ReplaceOrgan(form, slotId, organId, rank, origin);
        if (!CreaturePhenotypeCompiler.TryGetOrCompile(
                CreatePlan(WithOrgans(form, updated)), out _))
            return false;

        CommitOrgans(actor, ref body, formId, updated);
        return ExpressActiveForm(actor);
    }

    /// <summary>把指定槽位上的器官提升到新等级；炼血突破使用。</summary>
    public static bool TryUpgradeOrgan(ActorExtend actor, string slotId, int newRank)
    {
        if (!actor.E.TryGetComponent(out YaoBody body)) return false;
        if (!body.TryGetActiveForm(out YaoFormRecord form)) return false;

        YaoOrganRecord target = default;
        bool found = false;
        foreach (YaoOrganRecord organ in form.Organs)
        {
            if (!string.Equals(organ.SlotId, slotId, StringComparison.Ordinal)) continue;
            target = organ;
            found = true;
            break;
        }

        if (!found || target.Rank >= newRank) return false;

        YaoOrganRecord[] updated = (YaoOrganRecord[])form.Organs.Clone();
        for (int i = 0; i < updated.Length; i++)
        {
            if (string.Equals(updated[i].SlotId, slotId, StringComparison.Ordinal)) updated[i].Rank = newRank;
        }

        if (!CreaturePhenotypeCompiler.TryGetOrCompile(
                CreatePlan(WithOrgans(form, updated)), out _))
            return false;

        CommitOrgans(actor, ref body, form.FormId, updated);
        return ExpressActiveForm(actor);
    }

    /// <summary>切换活动形态；尊重冷却与封锁，切换后立即表达新形态。</summary>
    public static bool TrySwitchForm(ActorExtend actor, string formId)
    {
        if (!actor.E.TryGetComponent(out YaoBody body)) return false;
        if (!body.TryGetForm(formId, out YaoFormRecord target)) return false;
        if (string.Equals(body.ActiveFormId, formId, StringComparison.Ordinal)) return true;
        if (YaoTime.Now < body.LockedUntil) return false;
        if (actor.GetCultisys<Yao>().CurrLevel < target.RequiredRealm) return false;

        body.ActiveFormId = formId;
        body.LastSwitchAt = YaoTime.Now;
        body.LockedUntil = YaoTime.Now + target.Cooldown;
        actor.E.GetComponent<YaoBody>() = body;

        bool expressed = ExpressActiveForm(actor);
        if (!expressed)
        {
            // 表达失败时回滚到旧形态，保证总表与生效身体一致。
            body.ActiveFormId = FindPreviousFormId(ref body);
            actor.E.GetComponent<YaoBody>() = body;
            ExpressActiveForm(actor);
        }

        return expressed;
    }

    /// <summary>用指定形态重新表达；用于读档恢复后的重建。</summary>
    public static bool RestoreAndExpress(ActorExtend actor, YaoBody body)
    {
        if (!actor.E.HasComponent<YaoBody>()) actor.E.AddComponent(body);
        else actor.E.GetComponent<YaoBody>() = body;
        return ExpressActiveForm(actor);
    }

    /// <summary>按稳定种子挑选一个可提升的器官并升到更高等级；淬血小层次使用。</summary>
    public static bool UpgradeRandomOrgan(ActorExtend actor, ref Yao yao, int seed)
    {
        if (!actor.E.TryGetComponent(out YaoBody body)) return false;
        if (!body.TryGetActiveForm(out YaoFormRecord form)) return false;

        // 只允许提升先天、血脉表达或固血来源的一级器官。
        var candidates = new System.Collections.Generic.List<int>();
        for (int i = 0; i < form.Organs.Length; i++)
        {
            YaoOrganRecord organ = form.Organs[i];
            if (organ.Rank != 1) continue;
            if (organ.Origin is not (YaoOrganOrigin.Innate or YaoOrganOrigin.BloodlineExpressed
                or YaoOrganOrigin.Solidified)) continue;
            candidates.Add(i);
        }

        if (candidates.Count == 0) return false;
        int index = candidates[new Random(seed).Next(candidates.Count)];
        int targetRank = form.Organs[index].Rank + 1;
        return TryUpgradeOrgan(actor, form.Organs[index].SlotId, targetRank);
    }

    private static void CommitOrgans(ActorExtend actor, ref YaoBody body, string formId, YaoOrganRecord[] organs)
    {
        for (int i = 0; i < body.Forms.Length; i++)
        {
            if (string.Equals(body.Forms[i].FormId, formId, StringComparison.Ordinal))
            {
                body.Forms[i].Organs = organs;
                break;
            }
        }

        // 吞噬所得与返祖器官同步到兼容形态：兼容才保留，不兼容自然休眠。
        if (!body.TryGetForm(formId, out YaoFormRecord source)) return;
        for (int i = 0; i < body.Forms.Length; i++)
        {
            YaoFormRecord other = body.Forms[i];
            if (string.Equals(other.FormId, formId, StringComparison.Ordinal)) continue;
            if (other.Organs == source.Organs) continue;
            body.Forms[i] = SyncOrgansToForm(source, other);
        }

        actor.E.GetComponent<YaoBody>() = body;
    }

    /// <summary>把来源形态的器官按兼容规则同步到目标形态。</summary>
    private static YaoFormRecord SyncOrgansToForm(YaoFormRecord source, YaoFormRecord target)
    {
        var synced = new YaoOrganRecord[target.Organs.Length];
        Array.Copy(target.Organs, synced, target.Organs.Length);

        foreach (YaoOrganRecord organ in source.Organs)
        {
            bool alreadyPresent = false;
            foreach (YaoOrganRecord existing in synced)
            {
                if (string.Equals(existing.SlotId, organ.SlotId, StringComparison.Ordinal) &&
                    string.Equals(existing.OrganId, organ.OrganId, StringComparison.Ordinal))
                {
                    alreadyPresent = true;
                    break;
                }
            }

            if (alreadyPresent) continue;
            if (!IsOrganCompatibleWithForm(organ, target)) continue;
            bool replaced = false;
            for (int i = 0; i < synced.Length; i++)
            {
                if (string.Equals(synced[i].SlotId, organ.SlotId, StringComparison.Ordinal))
                {
                    synced[i] = organ;
                    replaced = true;
                    break;
                }
            }

            if (!replaced)
            {
                Array.Resize(ref synced, synced.Length + 1);
                synced[^1] = organ;
            }
        }

        target.Organs = synced;
        return target;
    }

    /// <summary>按共用身体定义判断器官是否可以出现在目标形态中。</summary>
    private static bool IsOrganCompatibleWithForm(YaoOrganRecord organ, YaoFormRecord form)
    {
        CreatureCompositions.Libraries.CreatureOrganAsset asset =
            Content.Libraries.Manager.CreatureOrganLibrary.get(organ.OrganId);
        CreatureCompositions.Libraries.CreatureBodyPlanAsset bodyPlan =
            Content.Libraries.Manager.CreatureBodyPlanLibrary.get(form.BodyPlanId);
        CreatureCompositions.Libraries.CreatureMorphAsset morph =
            Content.Libraries.Manager.CreatureMorphLibrary.get(form.MorphId);
        if (asset == null || bodyPlan == null || morph == null) return false;

        return MatchesTags(asset.AllowedBodyPlanTags, bodyPlan.Tags) &&
               MatchesTags(asset.AllowedMorphTags, morph.Tags);
    }

    private static bool MatchesTags(string[] allowedTags, string[] actualTags)
    {
        if (allowedTags == null || allowedTags.Length == 0) return true;
        if (actualTags == null || actualTags.Length == 0) return false;
        foreach (string allowed in allowedTags)
        {
            foreach (string actual in actualTags)
            {
                if (string.Equals(allowed, actual, StringComparison.Ordinal)) return true;
            }
        }

        return false;
    }

    private static bool HasOrgan(YaoFormRecord form, string slotId, string organId)
    {
        foreach (YaoOrganRecord organ in form.Organs)
        {
            if (string.Equals(organ.SlotId, slotId, StringComparison.Ordinal) &&
                string.Equals(organ.OrganId, organId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static YaoOrganRecord[] AppendOrgan(
        YaoFormRecord form, string slotId, string organId, int rank, YaoOrganOrigin origin)
    {
        var updated = new YaoOrganRecord[form.Organs.Length + 1];
        Array.Copy(form.Organs, updated, form.Organs.Length);
        updated[^1] = new YaoOrganRecord
        {
            SlotId = slotId,
            OrganId = organId,
            Rank = rank,
            Origin = origin,
        };
        return updated;
    }

    private static YaoOrganRecord[] ReplaceOrgan(
        YaoFormRecord form, string slotId, string organId, int rank, YaoOrganOrigin origin)
    {
        var updated = (YaoOrganRecord[])form.Organs.Clone();
        for (int i = 0; i < updated.Length; i++)
        {
            if (string.Equals(updated[i].SlotId, slotId, StringComparison.Ordinal))
            {
                updated[i] = new YaoOrganRecord
                {
                    SlotId = slotId,
                    OrganId = organId,
                    Rank = rank,
                    Origin = origin,
                };
                return updated;
            }
        }

        return AppendOrgan(form, slotId, organId, rank, origin);
    }

    private static YaoFormRecord WithOrgans(YaoFormRecord form, YaoOrganRecord[] organs)
    {
        form.Organs = organs;
        return form;
    }

    private static string FindPreviousFormId(ref YaoBody body)
    {
        foreach (YaoFormRecord form in body.Forms)
        {
            if (!string.Equals(form.FormId, body.ActiveFormId, StringComparison.Ordinal)) return form.FormId;
        }

        return body.ActiveFormId;
    }
}

/// <summary>妖兽标准形态编号。</summary>
public static class YaoFormIds
{
    /// <summary>真身形态编号。</summary>
    public const string TrueForm = "true_form";

    /// <summary>人形形态编号。</summary>
    public const string HumanForm = "human_form";
}
