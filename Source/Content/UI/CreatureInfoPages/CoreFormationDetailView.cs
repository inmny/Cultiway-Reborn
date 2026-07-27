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

/// <summary>金丹与元婴共用的结构化境界详情布局。</summary>
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

    /// <summary>刷新金丹或元婴的名称、品阶、强度、进度、组成、原子和代表法术。</summary>
    public void SetContent(CoreFormationPageModel model)
    {
        bool jindan = model.Realm == CoreFormationRealm.Jindan;
        string quality = string.Format(
            "Cultiway.RealmPage.CoreFormation.Quality".Localize(),
            model.Formation.quality.GetName());
        string strength = XianRealmPagePresentation.FormatNumber(model.Strength);
        string summary = jindan
            ? string.Format("Cultiway.RealmPage.Jindan.Summary".Localize(), model.Stage, strength)
            : string.Format("Cultiway.RealmPage.Yuanying.Summary".Localize(), strength);
        header.Set(model.Emblem, model.Name, quality, summary);

        RefreshContext(model);
        RefreshComposition(model.Formation.composition);
        RefreshAtoms(model);
        representativeSkill.SetSkill(model.Formation.representative_skill_id);
    }

    private void CreateContext(Transform parent)
    {
        GameObject context = UiLayout.Create(parent, "Realm Context", false,
            XianRealmPagePresentation.PageWidth, 22f, 2f);
        contextLine = UiLayout.Create(context.transform, "Context Line", true,
            XianRealmPagePresentation.PageWidth, 13f, 2f, TextAnchor.MiddleLeft);
        contextLeft = UiElements.CreateText(contextLine.transform, "Left", string.Empty, 112f, 13f, 6,
            TextAnchor.MiddleLeft, FontStyle.Bold);
        contextRight = UiElements.CreateText(contextLine.transform, "Right", string.Empty, 132f, 13f, 6,
            TextAnchor.MiddleRight, FontStyle.Bold);
        contextRight.color = UiTheme.Current.Palette.MutedText;
        ConfigureBestFit(contextLeft);
        ConfigureBestFit(contextRight);
        evolutionBar = UiWeightedSegmentBar.Create(context.transform, "Evolution", 246f, 7f);
    }

    private void CreateComposition(Transform parent)
    {
        GameObject section = UiLayout.Create(parent, "Element Composition", false,
            XianRealmPagePresentation.PageWidth, 51f, 2f);
        Text heading = UiElements.CreateText(section.transform, "Title",
            "Cultiway.RealmPage.Elements".Localize(), 246f, 12f, 7,
            TextAnchor.MiddleLeft, FontStyle.Bold);
        heading.color = UiTheme.Current.Palette.AccentText;
        compositionBar = UiWeightedSegmentBar.Create(section.transform, "Segments", 246f, 7f);

        GameObject fivePhaseRow = UiLayout.Create(section.transform, "Five Phase Legend", true, 246f, 13f, 1f,
            TextAnchor.MiddleLeft);
        elementLegends = new ElementLegendEntry[8];
        for (var i = ElementIndex.Iron; i <= ElementIndex.Earth; i++)
            elementLegends[i] = ElementLegendEntry.Create(fivePhaseRow.transform, i);

        GameObject extendedElementRow = UiLayout.Create(section.transform, "Yin Yang Chaos Legend", true,
            246f, 13f, 1f, TextAnchor.MiddleLeft);
        for (var i = ElementIndex.Neg; i <= ElementIndex.Entropy; i++)
            elementLegends[i] = ElementLegendEntry.Create(extendedElementRow.transform, i);
    }

    private void CreateAtoms(Transform parent)
    {
        GameObject section = UiLayout.Create(parent, "Formation Atoms", false,
            XianRealmPagePresentation.PageWidth, 52f, 2f);
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
        UiLayout.SetSize(grid.transform, 246f, 38f);
        GridLayoutGroup layout = grid.GetComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(78f, 18f);
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
        if (model.Realm == CoreFormationRealm.Jindan)
        {
            UiLayout.SetSize(contextLine.transform, 246f, 13f);
            UiLayout.SetSize(contextLeft.transform, 112f, 13f);
            UiLayout.SetSize(contextRight.transform, 132f, 13f);
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

        UiLayout.SetSize(contextLine.transform, 246f, 22f);
        UiLayout.SetSize(contextLeft.transform, 58f, 22f);
        UiLayout.SetSize(contextRight.transform, 186f, 22f);
        contextLeft.text = "Cultiway.RealmPage.Yuanying.Lineage".Localize();
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
        bool yuanying = model.Realm == CoreFormationRealm.Yuanying;
        inheritedLegend.SetActive(yuanying);
        manifestedLegend.SetActive(yuanying);

        var source = new CoreFormationEffectResolver.FormationSource(
            model.Formation,
            model.Stage,
            model.Strength);
        CoreFormationEffectResolver.Resolve(source, resolvedEffects);
        bool includeRuntimeState = IsCurrentFormation(model);
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

    /// <summary>判断页面展示的快照是否就是角色当前运行中的核心形成。</summary>
    private static bool IsCurrentFormation(CoreFormationPageModel model)
    {
        if (!CoreFormationEffectResolver.TryGetFormation(
                model.Actor,
                out CoreFormationEffectResolver.FormationSource current))
            return false;
        return current.Stage == model.Stage &&
               string.Equals(
                   current.Snapshot.signature,
                   model.Formation.signature,
                   System.StringComparison.Ordinal);
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
