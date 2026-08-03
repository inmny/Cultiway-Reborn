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

/// <summary>命名真气、金丹与元婴共用的结构化成果详情布局。</summary>
internal sealed class CoreFormationDetailView : MonoBehaviour
{
    private static readonly Color EvolutionRemainder = new(1f, 1f, 1f, 0.13f);

    private XianRealmHeaderView header;
    private GameObject contextLine;
    private Text contextLeft;
    private Text contextRight;
    private UiWeightedSegmentBar evolutionBar;
    private UiWeightedSegmentBar compositionBar;
    private ElementLegendEntry[] elementLegends;
    private GameObject inheritedLegend;
    private GameObject manifestedLegend;
    private CoreFormationAtomEntry[] atomEntries;
    private RepresentativeSkillView representativeSkill;
    private readonly List<CoreFormationResolvedEffect> resolvedEffects =
        new(CoreFormationGrantRuntime.MaxEffects);

    /// <summary>在原版人物信息页的固定内容区创建共享布局。</summary>
    public static CoreFormationDetailView Create(CreatureInfoPage page)
    {
        var view = page.gameObject.AddComponent<CoreFormationDetailView>();
        GameObject root = UiLayout.Create(page.transform, "Core Formation Detail", false,
            XianRealmPagePresentation.PageWidth, XianRealmPagePresentation.PageHeight, 2f);
        view.header = XianRealmHeaderView.Create(root.transform);
        view.CreateContext(root.transform);
        view.CreateComposition(root.transform);
        view.CreateAtoms(root.transform);
        view.representativeSkill = RepresentativeSkillView.Create(root.transform);
        return view;
    }

    /// <summary>刷新成果名称、品阶、强度、进度、组成、原子和代表法术。</summary>
    public void SetContent(CoreFormationPageModel model)
    {
        string quality = string.Format(
            "Cultiway.RealmPage.CoreFormation.Quality".Localize(),
            model.Formation.IsFinalized ? model.Formation.quality.GetName() : "--");
        string strength = XianRealmPagePresentation.FormatNumber(model.Strength);
        string summary = model.Realm switch
        {
            CoreFormationRealm.QiRefinement => string.Format(
                "Cultiway.RealmPage.QiRefinement.Summary".Localize(), model.Stage, strength),
            CoreFormationRealm.Jindan => string.Format(
                "Cultiway.RealmPage.Jindan.Summary".Localize(), model.Stage, strength),
            _ => string.Format("Cultiway.RealmPage.Yuanying.Summary".Localize(), strength)
        };
        if (!model.IsCurrent)
            summary = string.Format("Cultiway.RealmPage.Archived".Localize(), summary);
        header.Set(model.Emblem, model.Name, quality, summary);

        RefreshContext(model);
        RefreshComposition(model.Formation.composition);
        RefreshAtoms(model);
        representativeSkill.SetSkill(model.Formation.representative_skill_id);
    }

    private void CreateContext(Transform parent)
    {
        GameObject context = UiLayout.Create(parent, "Realm Context", false,
            XianRealmPagePresentation.PageWidth, 18f, 2f);
        contextLine = UiLayout.Create(context.transform, "Context Line", true,
            XianRealmPagePresentation.PageWidth, 11f, 2f, TextAnchor.MiddleLeft);
        contextLeft = UiElements.CreateText(contextLine.transform, "Left", string.Empty, 112f, 11f, 6,
            TextAnchor.MiddleLeft, FontStyle.Bold);
        contextRight = UiElements.CreateText(contextLine.transform, "Right", string.Empty, 132f, 11f, 6,
            TextAnchor.MiddleRight, FontStyle.Bold);
        contextRight.color = UiTheme.Current.Palette.MutedText;
        ConfigureBestFit(contextLeft);
        ConfigureBestFit(contextRight);
        evolutionBar = UiWeightedSegmentBar.Create(context.transform, "Evolution", 246f, 5f);
    }

    private void CreateComposition(Transform parent)
    {
        GameObject section = UiLayout.Create(parent, "Element Composition", false,
            XianRealmPagePresentation.PageWidth, 68f, 2f);
        Text heading = UiElements.CreateText(section.transform, "Title",
            "Cultiway.RealmPage.Elements".Localize(), 246f, 11f, 7,
            TextAnchor.MiddleLeft, FontStyle.Bold);
        heading.color = UiTheme.Current.Palette.AccentText;
        compositionBar = UiWeightedSegmentBar.Create(section.transform, "Segments", 246f, 7f);

        GameObject fivePhaseRow = UiLayout.Create(section.transform, "Five Phase Legend", true,
            246f, XianRealmPagePresentation.MetricCellHeight, 5f, TextAnchor.MiddleCenter);
        elementLegends = new ElementLegendEntry[8];
        for (var i = ElementIndex.Iron; i <= ElementIndex.Earth; i++)
            elementLegends[i] = ElementLegendEntry.Create(fivePhaseRow.transform, i);

        GameObject extendedElementRow = UiLayout.Create(section.transform, "Yin Yang Chaos Legend", true,
            246f, XianRealmPagePresentation.MetricCellHeight, 5f, TextAnchor.MiddleCenter);
        for (var i = ElementIndex.Neg; i <= ElementIndex.Entropy; i++)
            elementLegends[i] = ElementLegendEntry.Create(extendedElementRow.transform, i);
    }

    private void CreateAtoms(Transform parent)
    {
        GameObject section = UiLayout.Create(parent, "Formation Atoms", false,
            XianRealmPagePresentation.PageWidth, 50f, 2f);
        GameObject titleRow = UiLayout.Create(section.transform, "Title Row", true, 246f, 12f, 3f,
            TextAnchor.MiddleLeft);
        Text heading = UiElements.CreateText(titleRow.transform, "Title",
            "Cultiway.RealmPage.Atoms".Localize(), 66f, 12f, 7,
            TextAnchor.MiddleLeft, FontStyle.Bold);
        heading.color = UiTheme.Current.Palette.AccentText;
        inheritedLegend = CreateOriginLegend(titleRow.transform, "Inherited",
            "Cultiway.RealmPage.Atom.Inherited".Localize(),
            XianRealmPagePresentation.JindanPrimary, 82f);
        manifestedLegend = CreateOriginLegend(titleRow.transform, "Manifested",
            "Cultiway.RealmPage.Atom.Manifested".Localize(),
            XianRealmPagePresentation.YuanyingPrimary, 89f);

        var grid = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup), typeof(LayoutElement));
        grid.transform.SetParent(section.transform, false);
        UiLayout.SetSize(grid.transform, 246f, 36f);
        GridLayoutGroup layout = grid.GetComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(78f, 17f);
        layout.spacing = new Vector2(6f, 2f);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 3;
        layout.childAlignment = TextAnchor.UpperLeft;

        atomEntries = new CoreFormationAtomEntry[6];
        for (var i = 0; i < atomEntries.Length; i++)
            atomEntries[i] = CoreFormationAtomEntry.Create(grid.transform, i);
    }

    private void RefreshContext(CoreFormationPageModel model)
    {
        if (model.Realm == CoreFormationRealm.QiRefinement)
        {
            UiLayout.SetSize(contextLine.transform, 246f, 11f);
            UiLayout.SetSize(contextLeft.transform, 112f, 11f);
            UiLayout.SetSize(contextRight.transform, 132f, 11f);
            contextLeft.text = "Cultiway.RealmPage.QiRefinement.Refinement".Localize();
            contextRight.text = model.Formation.IsFinalized
                ? "Cultiway.RealmPage.QiRefinement.Finalized".Localize()
                : string.Format(
                    "Cultiway.RealmPage.QiRefinement.NextMilestone".Localize(),
                    model.NextEvolutionStage);
            evolutionBar.gameObject.SetActive(true);
            float progress = model.Formation.IsFinalized
                ? 1f
                : Mathf.Clamp01(model.Stage / (float)Cultisyses.MinimumFoundationQiLayers);
            evolutionBar.SetSegments(
                new[] { progress, 1f - progress },
                new[] { XianRealmPagePresentation.QiRefinementPrimary, EvolutionRemainder });
            return;
        }

        if (model.Realm == CoreFormationRealm.Jindan)
        {
            UiLayout.SetSize(contextLine.transform, 246f, 11f);
            UiLayout.SetSize(contextLeft.transform, 112f, 11f);
            UiLayout.SetSize(contextRight.transform, 132f, 11f);
            contextLeft.text = "Cultiway.RealmPage.Jindan.Evolution".Localize();
            contextRight.text = model.NextEvolutionStage > 0
                ? string.Format("Cultiway.RealmPage.Jindan.NextEvolution".Localize(),
                    model.NextEvolutionStage)
                : "Cultiway.RealmPage.Jindan.EvolutionComplete".Localize();
            evolutionBar.gameObject.SetActive(true);

            ResolveEvolutionProgress(model.Stage, out float progress);
            evolutionBar.SetSegments(
                new[] { progress, 1f - progress },
                new[] { XianRealmPagePresentation.JindanPrimary, EvolutionRemainder });
            return;
        }

        UiLayout.SetSize(contextLine.transform, 246f, 18f);
        UiLayout.SetSize(contextLeft.transform, 58f, 18f);
        UiLayout.SetSize(contextRight.transform, 186f, 18f);
        contextLeft.text = "Cultiway.RealmPage.Lineage".Localize();
        contextRight.text = string.IsNullOrEmpty(model.Lineage)
            ? "Cultiway.RealmPage.Yuanying.LineageEmpty".Localize()
            : model.Lineage;
        evolutionBar.gameObject.SetActive(false);
    }

    private void RefreshComposition(ElementComposition composition)
    {
        float[] values = XianRealmPagePresentation.GetNormalizedComposition(composition);
        var colors = new Color[values.Length];
        for (var i = 0; i < colors.Length; i++)
        {
            colors[i] = XianRealmPagePresentation.GetElementColor(i);
            elementLegends[i].SetValue(values[i]);
        }
        compositionBar.SetSegments(values, colors);
    }

    private void RefreshAtoms(CoreFormationPageModel model)
    {
        bool hasInheritedAtoms = model.Realm != CoreFormationRealm.QiRefinement;
        inheritedLegend.SetActive(hasInheritedAtoms);
        manifestedLegend.SetActive(hasInheritedAtoms);
        if (hasInheritedAtoms)
        {
            SetLegendColor(inheritedLegend, XianRealmPagePresentation.GetInheritedRealmColor(model.Realm));
            SetLegendColor(manifestedLegend, XianRealmPagePresentation.GetRealmColor(model.Realm));
        }

        var source = new CoreFormationEffectResolver.FormationSource(
            model.Formation,
            model.Stage,
            model.Strength);
        CoreFormationEffectResolver.Resolve(source, resolvedEffects);
        bool includeRuntimeState = model.IsCurrent;
        List<CoreFormationAtomPresentation> atoms =
            XianRealmPagePresentation.ResolveActiveAtoms(model.Formation, model.Stage);
        for (var i = 0; i < atomEntries.Length; i++)
        {
            if (i < atoms.Count)
                atomEntries[i].SetValue(
                    atoms[i],
                    model.Realm,
                    model.Actor,
                    resolvedEffects,
                    includeRuntimeState);
            else atomEntries[i].Hide();
        }
    }

    private static GameObject CreateOriginLegend(
        Transform parent,
        string name,
        string label,
        Color color,
        float width)
    {
        GameObject root = UiLayout.Create(parent, name, true, width, 12f, 2f, TextAnchor.MiddleRight);
        var marker = new GameObject("Marker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
            typeof(LayoutElement));
        marker.transform.SetParent(root.transform, false);
        UiLayout.SetSize(marker.transform, 5f, 5f);
        marker.GetComponent<Image>().color = color;
        Text text = UiElements.CreateText(root.transform, "Label", label, width - 7f, 12f, 5,
            TextAnchor.MiddleLeft);
        text.color = UiTheme.Current.Palette.MutedText;
        return root;
    }

    /// <summary>刷新图例标记色，使归档来源与当前境界显化一目了然。</summary>
    private static void SetLegendColor(GameObject legend, Color color)
    {
        legend.transform.Find("Marker").GetComponent<Image>().color = color;
    }

    private static void ResolveEvolutionProgress(int stage, out float progress)
    {
        if (stage >= 9)
        {
            progress = 1f;
            return;
        }
        int previous = stage >= 6 ? 6 : stage >= 3 ? 3 : 0;
        int next = previous + 3;
        progress = Mathf.Clamp01((float)(stage - previous) / (next - previous));
    }

    private static void ConfigureBestFit(Text text)
    {
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 4;
        text.resizeTextMaxSize = 6;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
    }
}
