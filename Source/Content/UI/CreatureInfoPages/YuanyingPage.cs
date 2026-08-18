using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
using Cultiway.Core;
using Cultiway.UI.Prefab;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;
using strings;
using UnityEngine;

namespace Cultiway.Content.UI.CreatureInfoPages;

/// <summary>人物信息窗口中的元婴谱系与显化详情页。</summary>
public sealed class YuanyingPage : MonoBehaviour
{
    private CoreFormationDetailView detailView;

    /// <summary>创建金丹与元婴共用的结构化详情布局。</summary>
    public static void Setup(CreatureInfoPage page)
    {
        var component = page.gameObject.AddComponent<YuanyingPage>();
        component.detailView = CoreFormationDetailView.Create(page);
    }

    /// <summary>刷新当前元婴名称、品阶、原始强度、金丹谱系、构成和代表法术。</summary>
    [Hotfixable]
    public static void Show(CreatureInfoPage page, Actor actor)
    {
        ActorExtend actorExtend = actor.GetExtend();
        CoreFormationSnapshot formation;
        string name;
        float strength;
        int stage;
        string sourceJindanName;
        if (actorExtend.TryGetComponent(out Yuanying active))
        {
            formation = active.formation;
            name = active.GetName();
            strength = active.strength;
            stage = active.stage;
            sourceJindanName = active.source_jindan_name;
        }
        else
        {
            YuanyingSeed seed = actorExtend.GetComponent<YuanyingSeed>();
            formation = seed.formation;
            name = $"{seed.formation.canonical_name} ({"Cultiway.YuanyingSeed.Dormant".Localize()})";
            strength = seed.strength;
            stage = seed.formation.refinement;
            sourceJindanName = seed.formation.source_name;
        }

        var model = new CoreFormationPageModel(
            actorExtend,
            CoreFormationRealm.Yuanying,
            formation,
            name,
            strength,
            stage,
            sourceJindanName,
            -1);
        page.GetComponent<YuanyingPage>().detailView.SetContent(model);
    }
}
