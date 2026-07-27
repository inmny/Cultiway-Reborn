using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.UI.Prefab;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content.UI.CreatureInfoPages;

/// <summary>人物信息窗口中的金丹组合与演化详情页。</summary>
public sealed class JindanPage : MonoBehaviour
{
    private CoreFormationDetailView detailView;

    /// <summary>创建金丹与元婴共用的结构化详情布局。</summary>
    public static void Setup(CreatureInfoPage page)
    {
        var component = page.gameObject.AddComponent<JindanPage>();
        component.detailView = CoreFormationDetailView.Create(page);
    }

    /// <summary>刷新当前金丹名称、品阶、原始强度、演化节点、构成和代表法术。</summary>
    [Hotfixable]
    public static void Show(CreatureInfoPage page, Actor actor)
    {
        ActorExtend actorExtend = actor.GetExtend();
        ref Jindan jindan = ref actorExtend.GetJindan();
        var model = new CoreFormationPageModel(
            actorExtend,
            CoreFormationRealm.Jindan,
            jindan.formation,
            jindan.GetName(),
            jindan.strength,
            jindan.stage,
            null,
            CoreFormationComposer.GetNextEvolutionStage(jindan.stage));
        page.GetComponent<JindanPage>().detailView.SetContent(model);
    }
}
