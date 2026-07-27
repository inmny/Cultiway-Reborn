using Cultiway.Const;
using Cultiway.Core;
using Cultiway.UI;
using Cultiway.UI.Prefab;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.UI.CreatureInfoPages;

/// <summary>用三花、五气比例和离散品阶展示筑基状态。</summary>
internal sealed class FoundationDetailView : MonoBehaviour
{
    private XianRealmHeaderView header;
    private FoundationMetricGroup threeFlowers;
    private FoundationMetricGroup fiveQi;

    /// <summary>在人物信息页中创建完整筑基详情布局。</summary>
    public static FoundationDetailView Create(CreatureInfoPage page)
    {
        var view = page.gameObject.AddComponent<FoundationDetailView>();
        GameObject root = UiLayout.Create(page.transform, "Foundation Detail", false,
            XianRealmPagePresentation.PageWidth, XianRealmPagePresentation.PageHeight, 4f);
        view.header = XianRealmHeaderView.Create(root.transform);
        view.threeFlowers = FoundationMetricGroup.Create(
            root.transform,
            "Three Flowers",
            "Cultiway.RealmPage.Foundation.ThreeFlowers".Localize(),
            XianRealmPagePresentation.ThreeFlowerIconPaths,
            new[]
            {
                XianRealmPagePresentation.ThreeFlowerNameKeys[0].Localize(),
                XianRealmPagePresentation.ThreeFlowerNameKeys[1].Localize(),
                XianRealmPagePresentation.ThreeFlowerNameKeys[2].Localize()
            },
            XianRealmPagePresentation.ThreeFlowerColors);
        view.fiveQi = FoundationMetricGroup.Create(
            root.transform,
            "Five Qi",
            "Cultiway.RealmPage.Foundation.FiveQi".Localize(),
            XianRealmPagePresentation.FiveQiIconPaths,
            new[]
            {
                ElementIndex.ElementNames[ElementIndex.Iron].Localize(),
                ElementIndex.ElementNames[ElementIndex.Wood].Localize(),
                ElementIndex.ElementNames[ElementIndex.Water].Localize(),
                ElementIndex.ElementNames[ElementIndex.Fire].Localize(),
                ElementIndex.ElementNames[ElementIndex.Earth].Localize()
            },
            XianRealmPagePresentation.FiveQiColors);
        return view;
    }

    /// <summary>刷新筑基完成度、综合评级和两组实际数值。</summary>
    public void SetContent(FoundationPageModel model)
    {
        string rating = StrengthLevelFormatter.GetLevelName(
            model.Foundation.GetStrength(),
            Cultisyses.Xian.DisplayStyle);
        header.Set(
            model.Emblem,
            "Cultiway.RealmPage.Foundation.Name".Localize(),
            string.Format("Cultiway.RealmPage.Foundation.Progress".Localize(), model.CompletedCount, 8),
            string.Format("Cultiway.RealmPage.Foundation.Rating".Localize(), rating));
        threeFlowers.SetValues(model.ThreeFlowerValues, XianRealmPagePresentation.ThreeFlowerColors);
        fiveQi.SetValues(model.FiveQiValues, XianRealmPagePresentation.FiveQiColors);
    }
}
