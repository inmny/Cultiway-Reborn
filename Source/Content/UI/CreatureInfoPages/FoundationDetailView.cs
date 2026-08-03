using System.Collections.Generic;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.UI;
using Cultiway.UI.Prefab;
using Cultiway.Utils.Extension;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.Content.UI.CreatureInfoPages;

/// <summary>用三花、五气比例和离散品阶展示筑基状态。</summary>
internal sealed class FoundationDetailView : MonoBehaviour
{
    private XianRealmHeaderView header;
    private FoundationMetricGroup threeFlowers;
    private FoundationMetricGroup fiveQi;
    private CoreFormationAtomEntry[] atomEntries;
    private readonly List<CoreFormationResolvedEffect> resolvedEffects =
        new(CoreFormationGrantRuntime.MaxEffects);

    /// <summary>在人物信息页中创建完整筑基详情布局。</summary>
    public static FoundationDetailView Create(CreatureInfoPage page)
    {
        var view = page.gameObject.AddComponent<FoundationDetailView>();
        GameObject root = UiLayout.Create(page.transform, "Foundation Detail", false,
            XianRealmPagePresentation.PageWidth, XianRealmPagePresentation.PageHeight, 2f);
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
        view.CreateAtoms(root.transform);
        return view;
    }

    /// <summary>刷新筑基完成度、综合评级和两组实际数值。</summary>
    public void SetContent(FoundationPageModel model)
    {
        CoreFormationSnapshot formation = model.Foundation.formation;
        string rating = formation.IsFinalized ? formation.quality.GetName() : "--";
        string status = string.Format(
            "Cultiway.RealmPage.Foundation.Result".Localize(),
            rating,
            XianRealmPagePresentation.FormatNumber(model.Foundation.GetStrength()));
        if (!model.IsCurrent)
            status = string.Format("Cultiway.RealmPage.Archived".Localize(), status);
        header.Set(
            model.Emblem,
            formation.IsFinalized
                ? formation.canonical_name
                : "Cultiway.RealmPage.Foundation.Forming".Localize(),
            string.Format("Cultiway.RealmPage.Foundation.Progress".Localize(), model.CompletedCount, 8),
            status);
        threeFlowers.SetValues(model.ThreeFlowerValues, XianRealmPagePresentation.ThreeFlowerColors);
        fiveQi.SetValues(model.FiveQiValues, XianRealmPagePresentation.FiveQiColors);
        RefreshAtoms(model);
    }

    /// <summary>创建继承真气与仙基结构两个核心构成条目。</summary>
    private void CreateAtoms(Transform parent)
    {
        GameObject row = UiLayout.Create(parent, "Foundation Atoms", true,
            XianRealmPagePresentation.PageWidth, 20f, 4f, TextAnchor.MiddleLeft);
        Text title = UiElements.CreateText(row.transform, "Title",
            "Cultiway.RealmPage.Atoms".Localize(), 70f, 20f, 7,
            TextAnchor.MiddleLeft, FontStyle.Bold);
        title.color = UiTheme.Current.Palette.AccentText;
        atomEntries = new[]
        {
            CoreFormationAtomEntry.Create(row.transform, 0),
            CoreFormationAtomEntry.Create(row.transform, 1)
        };
    }

    /// <summary>刷新仙基已经显化的继承原子和结构原子。</summary>
    private void RefreshAtoms(FoundationPageModel model)
    {
        CoreFormationSnapshot formation = model.Foundation.formation;
        var source = new CoreFormationEffectResolver.FormationSource(
            formation, formation.refinement, formation.strength);
        CoreFormationEffectResolver.Resolve(source, resolvedEffects);
        List<CoreFormationAtomPresentation> atoms =
            XianRealmPagePresentation.ResolveActiveAtoms(formation, formation.refinement);
        for (var i = 0; i < atomEntries.Length; i++)
        {
            if (i < atoms.Count)
                atomEntries[i].SetValue(
                    atoms[i],
                    CoreFormationRealm.Foundation,
                    model.Actor,
                    resolvedEffects,
                    model.IsCurrent);
            else
                atomEntries[i].Hide();
        }
    }
}
