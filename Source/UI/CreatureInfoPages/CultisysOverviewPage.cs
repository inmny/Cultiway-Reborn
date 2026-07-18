using Cultiway.Abstract;
using Cultiway.Core;
using Cultiway.Core.Progression;
using Cultiway.UI.Prefab;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI.CreatureInfoPages;

/// <summary>只读展示人物当前拥有的全部修炼体系及其进阶状态。</summary>
public sealed class CultisysOverviewPage : MonoBehaviour
{
    private Text _summary;
    private UiScrollPane _list;
    private UiEmptyState _emptyState;
    private MonoObjPool<CultisysOverviewRow> _rowPool;

    public static void Setup(CreatureInfoPage page)
    {
        var component = page.gameObject.AddComponent<CultisysOverviewPage>();
        GameObject root = UiLayout.Create(page.transform, "CultisysOverviewRoot", false, 246f, 208f, 4f);
        component._summary = UiElements.CreateText(root.transform, "Summary", string.Empty, 246f, 18f, 8,
            TextAnchor.MiddleLeft, FontStyle.Bold);

        component._list = UiScrollPane.CreateVertical(root.transform, "Cultisyses", 246f, 186f);
        component._emptyState = new UiEmptyState(component._list.Content,
            "Cultiway.CultisysOverview.UI.Empty".Localize(), 230f, 48f);
        component._rowPool = new MonoObjPool<CultisysOverviewRow>(CultisysOverviewRow.Prefab,
            component._list.Content);
    }

    [Hotfixable]
    public static void Show(CreatureInfoPage page, Actor actor)
    {
        page.GetComponent<CultisysOverviewPage>().Refresh(actor.GetExtend());
    }

    private void Refresh(ActorExtend actor)
    {
        _rowPool.Clear();
        int count = 0;
        var cultisyses = ProgressionService.RegisteredCultisyses;
        for (var i = 0; i < cultisyses.Count; i++)
        {
            var cultisys = cultisyses[i];
            if (!cultisys.IsOwnedBy(actor)) continue;
            _rowPool.GetNext().Setup(cultisys, actor);
            count++;
        }

        _summary.text = string.Format("Cultiway.CultisysOverview.UI.Format.Count".Localize(), count);
        _emptyState.SetVisible(count == 0);
        _list.ResetToTop();
    }
}
