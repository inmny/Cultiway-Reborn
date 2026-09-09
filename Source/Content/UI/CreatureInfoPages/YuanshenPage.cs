using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
using Cultiway.Core;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.UI.Prefab;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;
using strings;
using UnityEngine;

namespace Cultiway.Content.UI.CreatureInfoPages;

/// <summary>人物信息窗口中的元神成果与元婴谱系详情页。</summary>
public sealed class YuanshenPage : MonoBehaviour
{
    /// <summary>元神成果共用的结构化详情布局。</summary>
    private CoreFormationDetailView detailView;

    /// <summary>创建元神详情布局。</summary>
    /// <param name="page">人物信息页容器。</param>
    public static void Setup(CreatureInfoPage page)
    {
        var component = page.gameObject.AddComponent<YuanshenPage>();
        component.detailView = CoreFormationDetailView.Create(page);
    }

    /// <summary>刷新当前元神名称、品阶、层数、强度、元婴谱系、构成和代表法术。</summary>
    /// <param name="page">人物信息页容器。</param>
    /// <param name="actor">当前展示的人物。</param>
    [Hotfixable]
    public static void Show(CreatureInfoPage page, Actor actor)
    {
        SkillCasterContext carrierContext = SkillCasterContextService.Resolve(actor.GetExtend());
        if (carrierContext.IsValid) actor = carrierContext.Owner.Base;
        ActorExtend actorExtend = actor.GetExtend();
        Yuanshen yuanshen = actorExtend.GetComponent<Yuanshen>();
        float availableShare = 100f;
        float injuryShare = 0f;
        if (actorExtend.TryGetComponent(out YuanshenRuntimeState runtime))
        {
            availableShare = runtime.AvailableShare;
            injuryShare = runtime.injury_locked_share;
        }
        DivineSenseBudget budget = DivineSenseBudgetService.Resolve(actorExtend);
        string runtimeSummary = string.Format(
            "Cultiway.Yuanshen.Page.RuntimeSummary".Localize(),
            yuanshen.stage,
            XianRealmPagePresentation.FormatNumber(yuanshen.strength),
            Mathf.RoundToInt(availableShare),
            Mathf.RoundToInt(injuryShare),
            DivineSenseBudgetService.ResolveUsedThreads(actorExtend, in budget),
            budget.TotalThreadCapacity,
            YuanshenTravelService.CountActiveNodes(actorExtend),
            YuanshenAnchorNetworkService.CountOwnedFacilities(actorExtend));
        var model = new CoreFormationPageModel(
            actorExtend,
            CoreFormationRealm.Yuanshen,
            yuanshen.formation,
            yuanshen.GetName(),
            yuanshen.strength,
            yuanshen.stage,
            yuanshen.source_yuanying_name,
            -1,
            runtimeSummary);
        page.GetComponent<YuanshenPage>().detailView.SetContent(model);
    }
}
