using Cultiway.Content.AIGC;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Extensions;
using Cultiway.Content.Events;
using Cultiway.Core.Components;
using Cultiway.Core.EventSystem;
using Cultiway.Core.EventSystem.Events;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;
using Cultiway.Content.Libraries;

namespace Cultiway.Content.Systems.Logic;

/// <summary>
/// 处理 LLM 返回的功法生成结果，落地生成书本并校准等待时长。
/// </summary>
public class CultibookGeneratedEventSystem : GenericEventSystem<CultibookGeneratedEvent>
{
    protected override void HandleEvent(CultibookGeneratedEvent evt)
    {
        if (evt.ActorId == 0 || string.IsNullOrEmpty(evt.RequestId)) return;

        var actor = World.world.units.get(evt.ActorId);
        if (actor == null || actor.isRekt()) return;

        var ae = actor.GetExtend();

        var book = World.world.books.CreateCultibookFromDraft(actor, evt.Draft);
        if (book != null)
        {
            var cultibookAsset = book.GetExtend().GetComponent<Cultibook>().Asset;
            ae.SetMainCultibook(cultibookAsset);
            ae.AddMainCultibookMastery(100);
        }
        actor.data.set(ContentActorDataKeys.WaitingForCultibookCreation_int, 0);
    }
}
