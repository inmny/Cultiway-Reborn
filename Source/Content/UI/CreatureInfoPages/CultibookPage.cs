using Cultiway.Abstract;
using Cultiway.UI;
using Cultiway.UI.Prefab;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Cultiway.Content.UI.CreatureInfoPages;

/// <summary>以主修功法、修炼实践、资源和已知功法组成的人物功法页。</summary>
public sealed class CultibookPage : MonoBehaviour, IWorldBoundCreatureInfoPage
{
    private const float ContentWidth = 234f;

    private Actor actor;
    private ScrollRect scroll;
    private RectTransform content;
    private GameObject mainSection;
    private CultibookSummaryRow mainRow;
    private UiEmptyState mainEmpty;
    private Text currentMethod;
    private Text totalPractice;
    private CultivationElementExposureView elementExposure;
    private UiEmptyState practiceEmpty;
    private MonoObjPool<CultivationPracticeRow> practicePool;
    private GameObject resourceSection;
    private MonoObjPool<CultivationResourceRow> resourcePool;
    private GameObject knownSection;
    private MonoObjPool<CultibookKnownRow> knownPool;

    /// <summary>让页面根节点直接承担滚动，并一次性创建内容与池化列表。</summary>
    public static void Setup(CreatureInfoPage page)
    {
        CultibookPage component = page.gameObject.AddComponent<CultibookPage>();
        component.Build(page.transform);
    }

    /// <summary>绑定当前人物并刷新整页只读快照。</summary>
    [Hotfixable]
    public static void Show(CreatureInfoPage page, Actor actor)
    {
        page.GetComponent<CultibookPage>().Bind(actor);
    }

    private void Build(Transform parent)
    {
        scroll = parent.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 60f;

        GameObject viewportObject = new("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        viewportObject.transform.SetParent(parent, false);
        RectTransform viewport = viewportObject.GetComponent<RectTransform>();
        UiLayout.Stretch(viewport);
        Image viewportInput = viewportObject.GetComponent<Image>();
        viewportInput.color = Color.clear;
        viewportInput.raycastTarget = true;

        GameObject contentObject = new("Content", typeof(RectTransform), typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter));
        contentObject.transform.SetParent(viewport, false);
        content = contentObject.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;
        scroll.viewport = viewport;
        scroll.content = content;

        VerticalLayoutGroup contentLayout = contentObject.GetComponent<VerticalLayoutGroup>();
        contentLayout.spacing = UiTheme.Current.Metrics.SpacingMd;
        contentLayout.padding = new RectOffset(6, 6, 2, 8);
        contentLayout.childAlignment = TextAnchor.UpperLeft;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = false;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        contentObject.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        mainSection = CreateDynamicSection(content, "MainSection");
        CreateSectionTitle(mainSection.transform, "MainTitle", "Cultiway.CultibookPage.Section.Main");
        mainRow = Object.Instantiate(CultibookSummaryRow.Prefab, mainSection.transform, false);
        mainEmpty = new UiEmptyState(
            mainSection.transform,
            "Cultiway.CultibookPage.Empty.Main".Localize(),
            ContentWidth,
            24f);
        mainEmpty.Text.color = UiTheme.Current.Palette.Disabled;

        GameObject practiceSection = CreateDynamicSection(content, "PracticeSection");
        CreateSectionTitle(practiceSection.transform, "PracticeTitle", "Cultiway.CultibookPage.Section.Practice");
        CreatePracticeSummary(practiceSection.transform);
        elementExposure = CultivationElementExposureView.Create(practiceSection.transform);
        GameObject practiceList = CreateDynamicSection(practiceSection.transform, "PracticeList", 2f);
        practicePool = new MonoObjPool<CultivationPracticeRow>(CultivationPracticeRow.Prefab,
            practiceList.transform);
        practiceEmpty = new UiEmptyState(
            practiceList.transform,
            "Cultiway.CultibookPage.Empty.Practice".Localize(),
            ContentWidth,
            22f);
        practiceEmpty.Text.color = UiTheme.Current.Palette.Disabled;

        resourceSection = CreateDynamicSection(content, "ResourceSection");
        CreateSectionTitle(resourceSection.transform, "ResourceTitle", "Cultiway.CultibookPage.Section.Resources");
        GameObject resourceList = CreateDynamicSection(resourceSection.transform, "ResourceList", 2f);
        resourcePool = new MonoObjPool<CultivationResourceRow>(CultivationResourceRow.Prefab,
            resourceList.transform);

        knownSection = CreateDynamicSection(content, "KnownSection");
        CreateSectionTitle(knownSection.transform, "KnownTitle", "Cultiway.CultibookPage.Section.Known");
        GameObject knownList = CreateDynamicSection(knownSection.transform, "KnownList", 2f);
        knownPool = new MonoObjPool<CultibookKnownRow>(CultibookKnownRow.Prefab, knownList.transform,
            deactive_action: row => row.ClearWorldBinding());
    }

    private void Bind(Actor selectedActor)
    {
        bool actorChanged = actor != selectedActor;
        actor = selectedActor;
        Refresh();
        if (actorChanged) scroll.verticalNormalizedPosition = 1f;
    }

    private void Refresh()
    {
        practicePool.Clear();
        resourcePool.Clear();
        knownPool.Clear();
        if (actor == null) return;

        CultibookPageModel model = CultibookPagePresentation.Build(actor.GetExtend());
        mainSection.SetActive(true);
        mainRow.gameObject.SetActive(model.Main != null);
        mainEmpty.SetVisible(model.Main == null);
        if (model.Main != null) mainRow.Setup(model.Main, model.Actor);

        string currentMethodName = string.IsNullOrEmpty(model.CurrentMethodName)
            ? "Cultiway.CultibookPage.Value.NoMainMethod".Localize()
            : model.CurrentMethodName;
        currentMethod.text = string.Format(
            "Cultiway.CultibookPage.Format.CurrentMethod".Localize(),
            currentMethodName);
        totalPractice.text = string.Format(
            "Cultiway.CultibookPage.Format.TotalPractice".Localize(),
            CultibookPagePresentation.FormatNumber(model.TotalPracticeMonths));

        elementExposure.Root.SetActive(model.HasElementExposure);
        if (model.HasElementExposure) elementExposure.SetValues(model.ElementExposure);
        for (var i = 0; i < model.Practices.Count; i++)
            practicePool.GetNext().Setup(model.Practices[i]);
        practiceEmpty.SetVisible(model.Practices.Count == 0);

        resourceSection.SetActive(model.Resources.Count > 0);
        for (var i = 0; i < model.Resources.Count; i++)
            resourcePool.GetNext().Setup(model.Resources[i]);

        knownSection.SetActive(model.KnownCultibooks.Count > 0);
        for (var i = 0; i < model.KnownCultibooks.Count; i++)
            knownPool.GetNext().Setup(model.KnownCultibooks[i], model.Actor);

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
    }

    public void ClearWorldBinding()
    {
        actor = null;
        mainRow.ClearWorldBinding();
        practicePool.Clear();
        resourcePool.Clear();
        knownPool.Clear();
    }

    private void CreatePracticeSummary(Transform parent)
    {
        GameObject row = UiLayout.Create(parent, "PracticeSummary", true, ContentWidth, 22f, 2f,
            TextAnchor.MiddleLeft);
        currentMethod = UiElements.CreateText(row.transform, "Method", string.Empty, 144f, 22f, 6);
        totalPractice = UiElements.CreateText(row.transform, "Total", string.Empty, 88f, 22f, 6,
            TextAnchor.MiddleRight, FontStyle.Bold);
        currentMethod.color = UiTheme.Current.Palette.MutedText;
        CultibookSummaryRow.ConfigureBestFit(currentMethod, 4, 6);
        CultibookSummaryRow.ConfigureBestFit(totalPractice, 4, 6);
    }

    private static GameObject CreateDynamicSection(Transform parent, string name, float spacing = 4f)
    {
        GameObject section = new(name, typeof(RectTransform), typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter));
        section.transform.SetParent(parent, false);
        VerticalLayoutGroup layout = section.GetComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        section.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return section;
    }

    private static void CreateSectionTitle(Transform parent, string name, string localeKey)
    {
        Text title = UiElements.CreateSectionTitle(parent, name, localeKey.Localize(), ContentWidth);
        title.rectTransform.sizeDelta = new Vector2(ContentWidth, 16f);
        LayoutElement layout = title.GetComponent<LayoutElement>();
        layout.minHeight = 16f;
        layout.preferredHeight = 16f;
    }

}
