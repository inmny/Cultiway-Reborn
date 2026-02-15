using Cultiway.Content.AIGC;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Extensions;
using Cultiway.Content.Events;
using Cultiway.Utils.Extension;
using strings;
using UnityEngine;
using Cultiway.Core.EventSystem;

namespace Cultiway.Content.Systems.Logic;

/// <summary>
/// 处理 LLM 返回的功法改进结果，落地生成改进版功法并转修。
/// </summary>
public class CultibookImprovedEventSystem : GenericEventSystem<CultibookImprovedEvent>
{
    protected override void HandleEvent(CultibookImprovedEvent evt)
    {
        if (evt.ActorId == 0 || string.IsNullOrEmpty(evt.RequestId) || evt.ImprovedDraft == null) return;

        var actor = World.world.units.get(evt.ActorId);
        if (actor == null || actor.isRekt()) return;

        var ae = actor.GetExtend();
        var mainCultibook = ae.GetMainCultibook();
        if (mainCultibook == null || mainCultibook.id != evt.OriginalCultibook.id) return;

        // 计算成功率（基于智慧）
        float intelligence = ae.GetStat(S.intelligence);
        float successRate = Mathf.Clamp(0.5f + intelligence / 10f * 0.01f, 0.5f, 0.9f);

        if (!Randy.randomChance(successRate))
        {
            // 改进失败，不影响功法
            actor.data.set(ContentActorDataKeys.WaitingForCultibookImprovement_int, 0);
            return;
        }

        // 创建改进版功法书并添加到城市
        var newBook = World.world.books.CreateCultibookFromDraft(actor, evt.ImprovedDraft);
        if (newBook == null)
        {
            actor.data.set(ContentActorDataKeys.WaitingForCultibookImprovement_int, 0);
            return;
        }

        var improvedCultibook = evt.ImprovedDraft;

        ae.SetMainCultibook(improvedCultibook);
        ae.AddMainCultibookMastery(100f);

        actor.data.set(ContentActorDataKeys.WaitingForCultibookImprovement_int, 0);
    }
}

