using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.UI.Prefab;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.UI.CreatureInfoPages;

/// <summary>人物信息窗口中的命名真气成果页。</summary>
public sealed class QiRefinementPage : MonoBehaviour
{
    private CoreFormationDetailView detailView;
    private Actor actor;
    private float refreshTimer;

    /// <summary>创建命名真气的结构化详情布局。</summary>
    public static void Setup(CreatureInfoPage page)
    {
        var component = page.gameObject.AddComponent<QiRefinementPage>();
        component.detailView = CoreFormationDetailView.Create(page);
    }

    /// <summary>刷新角色当前真气成果或突破后保留的归档。</summary>
    public static void Show(CreatureInfoPage page, Actor actor)
    {
        QiRefinementPage component = page.GetComponent<QiRefinementPage>();
        component.actor = actor;
        component.refreshTimer = 0f;
        component.Refresh();
    }

    /// <summary>页面可见期间低频刷新正在变化的凝练层数和状态。</summary>
    private void Update()
    {
        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer > 0f) return;
        refreshTimer = 0.25f;
        Refresh();
    }

    /// <summary>从当前绑定角色重建真气成果展示模型。</summary>
    private void Refresh()
    {
        if (actor.isRekt()) return;
        ActorExtend actorExtend = actor.GetExtend();
        CoreFormationSnapshot formation = actorExtend.GetComponent<QiRefinementState>().formation;
        int layers = formation.IsValid ? formation.refinement : 0;
        int nextMilestone = Cultisyses.MinimumFoundationQiLayers;
        string name = formation.IsFinalized
            ? formation.canonical_name
            : formation.IsValid
                ? "Cultiway.RealmPage.QiRefinement.Forming".Localize()
                : "Cultiway.RealmPage.QiRefinement.Unformed".Localize();
        detailView.SetContent(new CoreFormationPageModel(
            actorExtend,
            CoreFormationRealm.QiRefinement,
            formation,
            name,
            formation.IsValid ? formation.strength : 0f,
            layers,
            null,
            nextMilestone));
    }
}
