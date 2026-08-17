using System;
using System.Collections.Generic;
using Cultiway.Core.SubWorlds;
using Cultiway.Core.SubWorlds.Model;
using Cultiway.Utils.Extension;
using NeoModLoader.api;
using NeoModLoader.General.UI.Window;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI.SubWorlds;

/// <summary>WORLD 小世界分区的原版滚动窗口，提供模板与参数创建流程。</summary>
public sealed class SubWorldCreationWindow : AbstractWindow<SubWorldCreationWindow>
{
    public const string Id = "Cultiway.UI.SubWorldCreationWindow";

    private const float ContentWidth = 190f;
    private const float TemplateCellSize = 40f;

    private static readonly SizeChoice[] StandardSizes =
    {
        new(64, 64, "Cultiway.SubWorld.Create.Size.Small"),
        new(96, 96, "Cultiway.SubWorld.Create.Size.Medium"),
        new(128, 128, "Cultiway.SubWorld.Create.Size.Large")
    };

    private static readonly SettingSpec[] SettingSpecs =
    {
        new("random_shapes_amount", "Cultiway.SubWorld.Settings.RandomShapes", 0, 40),
        new("cubicle_size", "Cultiway.SubWorld.Settings.CubicleSize", 2, 15),
        new("perlin_scale_stage_1", "Cultiway.SubWorld.Settings.PerlinStage1", 0, 30),
        new("perlin_scale_stage_2", "Cultiway.SubWorld.Settings.PerlinStage2", 0, 30),
        new("perlin_scale_stage_3", "Cultiway.SubWorld.Settings.PerlinStage3", 0, 30),
        new("random_biomes", "Cultiway.SubWorld.Settings.RandomBiomes"),
        new("add_mountain_edges", "Cultiway.SubWorld.Settings.MountainEdges"),
        new("add_vegetation", "Cultiway.SubWorld.Settings.Vegetation"),
        new("add_center_lake", "Cultiway.SubWorld.Settings.CenterLake"),
        new("add_center_gradient_land", "Cultiway.SubWorld.Settings.CenterLand"),
        new("gradient_round_edges", "Cultiway.SubWorld.Settings.RoundEdges"),
        new("square_edges", "Cultiway.SubWorld.Settings.SquareEdges"),
        new("ring_effect", "Cultiway.SubWorld.Settings.RingEffect"),
        new("low_ground", "Cultiway.SubWorld.Settings.LowGround"),
        new("high_ground", "Cultiway.SubWorld.Settings.HighGround"),
        new("remove_mountains", "Cultiway.SubWorld.Settings.RemoveMountains")
    };

    private static SubWorldManager openingManager;
    private static SubWorldCreationWindow instance;

    private SubWorldManager manager;
    private GameObject templatePage;
    private GameObject settingsPage;
    private Button backButton;
    private Button resetButton;
    private Button createButton;
    private Button sizePreviousButton;
    private Button sizeNextButton;
    private Text pageTitle;
    private Text pageDescription;
    private Text templateTitle;
    private Text templateDescription;
    private Text sizeValue;
    private Text status;
    private Image templatePreview;
    private Transform templateGrid;
    private readonly Dictionary<string, Button> templateButtons = new();
    private readonly Dictionary<string, SettingRow> settingRows = new();
    private readonly List<SubWorldTemplateAsset> selectableTemplates = new();

    private string selectedTemplateId;
    private SubWorldGenerationSettings draftSettings;
    private int selectedWidth;
    private int selectedHeight;
    private int selectedSizeIndex;

    internal static void Open(SubWorldManager targetManager)
    {
        openingManager = targetManager ?? throw new ArgumentNullException(nameof(targetManager));
        if (!ScrollWindow.windowLoaded(Id)) CreateAndInit(Id);
        PowerButtonSelector.instance?.unselectAll();
        ScrollWindow.showWindow(Id);
    }

    internal static void CloseIfOpen()
    {
        if (instance == null || !instance.gameObject.activeInHierarchy) return;
        instance.Close();
    }

    protected override void Init()
    {
        instance = this;
        manager = openingManager ?? throw new InvalidOperationException("创建小世界窗口缺少 SubWorldManager");
        ConfigureNativeContent();

        GameObject header = UiLayout.Create(ContentTransform, "Header", true, ContentWidth, 24f, 4f,
            TextAnchor.MiddleLeft);
        backButton = UiElements.CreateIconButton(header.transform, "Back", UiIcons.Previous, 24f, 24f,
            ShowTemplatePage);
        UiTooltip.Set(backButton.gameObject, "Cultiway.SubWorld.Create.Back".Localize(),
            "Cultiway.SubWorld.Create.Back.Description".Localize());
        pageTitle = UiElements.CreateSectionTitle(header.transform, "Title",
            "Cultiway.SubWorld.Create.Title".Localize(), ContentWidth - 28f);

        pageDescription = UiElements.CreateText(ContentTransform, "PageDescription", string.Empty, ContentWidth, 20f,
            7, TextAnchor.MiddleCenter, FontStyle.Normal, VerticalWrapMode.Overflow);
        pageDescription.color = UiTheme.Current.Palette.MutedText;

        templatePage = CreatePage(ContentTransform, "TemplatePage");
        templateGrid = CreateTemplateGrid(templatePage.transform);
        GameObject templateActions = UiLayout.Create(templatePage.transform, "Actions", true, ContentWidth, 28f, 6f,
            TextAnchor.MiddleCenter);
        Button templateCancel = UiElements.CreateIconTextButton(templateActions.transform, "Cancel", UiIcons.Cancel,
            "Cultiway.SubWorld.Create.Cancel".Localize(), 112f, 26f, Close);
        UiTooltip.Set(templateCancel.gameObject, "Cultiway.SubWorld.Create.Cancel".Localize(),
            "Cultiway.SubWorld.Create.Cancel.Description".Localize());

        settingsPage = CreatePage(ContentTransform, "SettingsPage");
        GameObject templateInfo = UiLayout.Create(settingsPage.transform, "TemplateInfo", true, ContentWidth, 44f,
            6f, TextAnchor.MiddleLeft);
        GameObject previewObject = new("Preview", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        previewObject.transform.SetParent(templateInfo.transform, false);
        UiLayout.SetSize(previewObject.transform, 44f, 44f);
        templatePreview = previewObject.GetComponent<Image>();
        templatePreview.preserveAspect = true;
        GameObject templateText = UiLayout.Create(templateInfo.transform, "Text", false, 140f, 44f,
            2f, TextAnchor.UpperLeft);
        templateTitle = UiElements.CreateSectionTitle(templateText.transform, "Name", string.Empty, 140f);
        templateDescription = UiElements.CreateText(templateText.transform, "Description", string.Empty,
            140f, 22f, 7, TextAnchor.UpperLeft, FontStyle.Normal, VerticalWrapMode.Truncate);
        templateDescription.color = UiTheme.Current.Palette.MutedText;

        GameObject sizeRow = UiLayout.Create(settingsPage.transform, "SizeRow", true, ContentWidth, 26f, 4f,
            TextAnchor.MiddleLeft);
        UiElements.CreateText(sizeRow.transform, "Label", "Cultiway.SubWorld.Create.SizeLabel".Localize(), 40f, 26f,
            7);
        sizePreviousButton = UiElements.CreateIconButton(sizeRow.transform, "Previous", UiIcons.Previous, 24f, 24f,
            () => ChangeSize(-1));
        UiTooltip.Set(sizePreviousButton.gameObject, "Cultiway.SubWorld.Create.SizePrevious".Localize(),
            "Cultiway.SubWorld.Create.SizePrevious.Description".Localize());
        sizeValue = UiElements.CreateText(sizeRow.transform, "Value", string.Empty, 80f, 26f, 8,
            TextAnchor.MiddleCenter, FontStyle.Bold);
        sizeNextButton = UiElements.CreateIconButton(sizeRow.transform, "Next", UiIcons.Next, 24f, 24f,
            () => ChangeSize(1));
        UiTooltip.Set(sizeNextButton.gameObject, "Cultiway.SubWorld.Create.SizeNext".Localize(),
            "Cultiway.SubWorld.Create.SizeNext.Description".Localize());

        BuildSettingRows();

        status = UiElements.CreateText(settingsPage.transform, "Status", string.Empty, ContentWidth, 30f, 7,
            TextAnchor.MiddleCenter, FontStyle.Normal, VerticalWrapMode.Truncate);
        status.color = UiTheme.Current.Palette.MutedText;

        GameObject settingsActions = UiLayout.Create(settingsPage.transform, "Actions", true, ContentWidth, 28f, 5f,
            TextAnchor.MiddleCenter);
        resetButton = UiElements.CreateIconButton(settingsActions.transform, "Reset", UiIcons.Reset,
            28f, 26f, ResetSettings);
        UiTooltip.Set(resetButton.gameObject, "Cultiway.SubWorld.Create.Reset".Localize(),
            "Cultiway.SubWorld.Create.Reset.Description".Localize());
        Button settingsCancel = UiElements.CreateIconTextButton(settingsActions.transform, "Cancel", UiIcons.Cancel,
            "Cultiway.SubWorld.Create.Cancel".Localize(), 70f, 26f, Close);
        UiTooltip.Set(settingsCancel.gameObject, "Cultiway.SubWorld.Create.Cancel".Localize(),
            "Cultiway.SubWorld.Create.Cancel.Description".Localize());
        createButton = UiElements.CreateIconTextButton(settingsActions.transform, "Create", UiIcons.Add,
            "Cultiway.SubWorld.Create.Confirm".Localize(), 82f, 26f, TryCreate);
        UiTooltip.Set(createButton.gameObject, "Cultiway.SubWorld.Create.Confirm".Localize(),
            "Cultiway.SubWorld.Create.Confirm.Description".Localize());

        ShowTemplatePage();
    }

    public override void OnNormalEnable()
    {
        manager = openingManager ?? manager;
        RefreshTemplateCatalog();
        EnsureInitialTemplate();
        RefreshTemplateButtons();
        ShowTemplatePage();
    }

    private void OnDestroy()
    {
        if (ReferenceEquals(instance, this)) instance = null;
    }

    private void Close()
    {
        GetComponent<ScrollWindow>().clickHide();
    }

    private void ConfigureNativeContent()
    {
        VerticalLayoutGroup layout = ContentTransform.GetComponent<VerticalLayoutGroup>() ??
                                     ContentTransform.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = UiTheme.Current.Metrics.SpacingSm;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = ContentTransform.GetComponent<ContentSizeFitter>() ??
                                   ContentTransform.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private static GameObject CreatePage(Transform parent, string name)
    {
        GameObject page = new(name, typeof(RectTransform), typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter), typeof(LayoutElement));
        page.transform.SetParent(parent, false);
        RectTransform rect = page.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(ContentWidth, 0f);
        VerticalLayoutGroup layout = page.GetComponent<VerticalLayoutGroup>();
        layout.spacing = UiTheme.Current.Metrics.SpacingSm;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        page.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        LayoutElement element = page.GetComponent<LayoutElement>();
        element.minWidth = ContentWidth;
        element.preferredWidth = ContentWidth;
        element.flexibleWidth = 0f;
        return page;
    }

    private static Transform CreateTemplateGrid(Transform parent)
    {
        GameObject grid = new("TemplateGrid", typeof(RectTransform), typeof(GridLayoutGroup),
            typeof(ContentSizeFitter), typeof(LayoutElement));
        grid.transform.SetParent(parent, false);
        RectTransform rect = grid.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(ContentWidth, 0f);
        GridLayoutGroup layout = grid.GetComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(TemplateCellSize, TemplateCellSize);
        layout.spacing = new Vector2(UiTheme.Current.Metrics.SpacingMd, UiTheme.Current.Metrics.SpacingSm);
        layout.padding = new RectOffset(6, 6, 4, 4);
        layout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        layout.startAxis = GridLayoutGroup.Axis.Horizontal;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 4;
        grid.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        LayoutElement element = grid.GetComponent<LayoutElement>();
        element.minWidth = ContentWidth;
        element.preferredWidth = ContentWidth;
        element.flexibleWidth = 0f;
        return grid.transform;
    }

    private void ResetScrollToTop()
    {
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)ContentTransform);
        ScrollRect scrollRect = GetComponent<ScrollWindow>().scrollRect;
        if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f;
    }

    private void RefreshTemplateCatalog()
    {
        if (ModClass.L == null || ModClass.L.SubWorldTemplateLibrary == null)
        {
            selectableTemplates.Clear();
            return;
        }

        selectableTemplates.Clear();
        for (int i = 0; i < ModClass.L.SubWorldTemplateLibrary.list.Count; i++)
        {
            SubWorldTemplateAsset template = ModClass.L.SubWorldTemplateLibrary.list[i];
            if (template.allow_user_creation) selectableTemplates.Add(template);
        }

        selectableTemplates.Sort((left, right) =>
        {
            int order = left.display_order.CompareTo(right.display_order);
            return order != 0 ? order : string.CompareOrdinal(left.id, right.id);
        });
        if (templateButtons.Count != 0) return;

        for (int i = 0; i < selectableTemplates.Count; i++)
        {
            SubWorldTemplateAsset template = selectableTemplates[i];
            string templateId = template.id;
            Button button = UiElements.CreateIconButton(templateGrid, templateId,
                template.icon_path, 40f, 40f, () => SelectTemplate(templateId), 2f);
            templateButtons.Add(templateId, button);
            UiTooltip.Set(button.gameObject, GetTemplateLabel(template), GetTemplateDescription(template));
        }
    }

    private void EnsureInitialTemplate()
    {
        if (GetSelectedTemplate() != null) return;
        if (selectableTemplates.Count == 0)
        {
            selectedTemplateId = null;
            draftSettings = null;
            return;
        }

        SelectTemplateDraft(selectableTemplates[0]);
    }

    private void SelectTemplate(string templateId)
    {
        SubWorldTemplateAsset template = FindTemplate(templateId);
        if (template == null) return;
        SelectTemplateDraft(template);
        RefreshTemplateButtons();
        ShowSettingsPage();
    }

    private void SelectTemplateDraft(SubWorldTemplateAsset template)
    {
        selectedTemplateId = template.id;
        draftSettings = template.generation_settings?.Clone() ?? new SubWorldGenerationSettings();
        draftSettings.Clamp();
        selectedSizeIndex = StandardSizes.Length - 1;
        selectedWidth = StandardSizes[selectedSizeIndex].Width;
        selectedHeight = StandardSizes[selectedSizeIndex].Height;
        createButton.interactable = true;
        status.text = string.Empty;
        status.color = UiTheme.Current.Palette.MutedText;
    }

    private void RefreshTemplateButtons()
    {
        foreach (KeyValuePair<string, Button> pair in templateButtons)
            UiStateStyle.SetSelected(pair.Value, pair.Key == selectedTemplateId);
    }

    private void ShowTemplatePage()
    {
        templatePage.SetActive(true);
        settingsPage.SetActive(false);
        backButton.gameObject.SetActive(false);
        pageTitle.text = "Cultiway.SubWorld.Create.Title".Localize();
        pageDescription.text = "Cultiway.SubWorld.Create.TemplatePageDescription".Localize();
        RefreshTemplateButtons();
        if (isActiveAndEnabled) ResetScrollToTop();
    }

    private void ShowSettingsPage()
    {
        SubWorldTemplateAsset template = GetSelectedTemplate();
        if (template == null)
        {
            ShowTemplatePage();
            return;
        }

        templatePage.SetActive(false);
        settingsPage.SetActive(true);
        backButton.gameObject.SetActive(true);
        pageTitle.text = GetTemplateLabel(template);
        pageDescription.text = "Cultiway.SubWorld.Create.SettingsPageDescription".Localize();
        templateTitle.text = GetTemplateLabel(template);
        templateDescription.text = GetTemplateDescription(template);
        UiResources.SetImage(templatePreview, template.icon_path);
        resetButton.gameObject.SetActive(template.generation_profile_id != "empty");
        RefreshSizeView();
        RefreshSettingsRows();
        if (isActiveAndEnabled) ResetScrollToTop();
        if (template.generation_profile_id == "empty")
        {
            status.text = "Cultiway.SubWorld.Create.NoSettings".Localize();
            status.color = UiTheme.Current.Palette.MutedText;
        }
    }

    private void ChangeSize(int delta)
    {
        if (draftSettings == null) return;
        selectedSizeIndex = Mathf.Clamp(selectedSizeIndex + delta, 0, StandardSizes.Length - 1);
        selectedWidth = StandardSizes[selectedSizeIndex].Width;
        selectedHeight = StandardSizes[selectedSizeIndex].Height;
        RefreshSizeView();
    }

    private void RefreshSizeView()
    {
        if (draftSettings == null) return;
        SizeChoice choice = StandardSizes[selectedSizeIndex];
        sizeValue.text = string.Format("Cultiway.SubWorld.Create.SizeFormat".Localize(),
            choice.NameKey.Localize(), choice.Width, choice.Height);
        sizePreviousButton.interactable = selectedSizeIndex > 0;
        sizeNextButton.interactable = selectedSizeIndex < StandardSizes.Length - 1;
    }

    private void BuildSettingRows()
    {
        for (int i = 0; i < SettingSpecs.Length; i++)
        {
            SettingSpec spec = SettingSpecs[i];
            SettingRow row = new(settingsPage.transform, spec,
                () => ChangeInteger(spec.Id, -1),
                () => ChangeInteger(spec.Id, 1),
                value => ChangeToggle(spec.Id, value));
            settingRows.Add(spec.Id, row);
        }
    }

    private void RefreshSettingsRows()
    {
        if (draftSettings == null) return;
        SubWorldTemplateAsset template = GetSelectedTemplate();
        for (int i = 0; i < SettingSpecs.Length; i++)
        {
            SettingSpec spec = SettingSpecs[i];
            SettingRow row = settingRows[spec.Id];
            row.Root.SetActive(IsSettingVisible(spec, template));
            row.Refresh(GetInteger(spec.Id), GetToggle(spec.Id));
        }
    }

    private static bool IsSettingVisible(SettingSpec spec, SubWorldTemplateAsset template)
    {
        if (template == null) return false;
        string profile = template.generation_profile_id;
        SubWorldGenerationSettings defaults = template.generation_settings;
        if (spec.Id == "perlin_scale_stage_1") return defaults == null || defaults.main_perlin_noise_stage;
        if (spec.Id == "perlin_scale_stage_2") return defaults == null || defaults.perlin_noise_stage_2;
        if (spec.Id == "perlin_scale_stage_3") return defaults == null || defaults.perlin_noise_stage_3;
        if (spec.Id == "random_shapes_amount")
        {
            return profile != "checkerboard" && profile != "cubicles" && profile != "anthill" &&
                   profile != "empty";
        }
        if (spec.Id == "cubicle_size") return profile == "cubicles";
        if (profile == "empty") return false;
        if (profile == "checkerboard")
        {
            return spec.Id == "random_biomes" || spec.Id == "add_vegetation" ||
                   spec.Id == "low_ground" || spec.Id == "high_ground";
        }
        if (profile == "cubicles")
        {
            return spec.Id == "cubicle_size" || spec.Id == "random_biomes" ||
                   spec.Id == "add_vegetation" || spec.Id == "low_ground" || spec.Id == "high_ground";
        }
        return true;
    }

    private void ChangeInteger(string id, int delta)
    {
        if (draftSettings == null) return;
        SettingSpec spec = FindSpec(id);
        int value = Mathf.Clamp(GetInteger(id) + delta, spec.MinValue, spec.MaxValue);
        SetInteger(id, value);
        RefreshSettingsRows();
    }

    private void ChangeToggle(string id, bool value)
    {
        if (draftSettings == null) return;
        SetToggle(id, value);
        RefreshSettingsRows();
    }

    private void ResetSettings()
    {
        SubWorldTemplateAsset template = GetSelectedTemplate();
        if (template == null) return;
        draftSettings = template.generation_settings?.Clone() ?? new SubWorldGenerationSettings();
        draftSettings.Clamp();
        selectedSizeIndex = StandardSizes.Length - 1;
        selectedWidth = StandardSizes[selectedSizeIndex].Width;
        selectedHeight = StandardSizes[selectedSizeIndex].Height;
        createButton.interactable = true;
        status.text = string.Empty;
        status.color = UiTheme.Current.Palette.MutedText;
        RefreshSizeView();
        RefreshSettingsRows();
    }

    private void TryCreate()
    {
        if (draftSettings == null || string.IsNullOrEmpty(selectedTemplateId)) return;
        createButton.interactable = false;
        try
        {
            manager.CreateFromWorld(selectedTemplateId, selectedWidth, selectedHeight, draftSettings);
            Close();
        }
        catch (Exception exception)
        {
            UnityEngine.Debug.LogException(exception);
            createButton.interactable = true;
            status.text = "Cultiway.SubWorld.Create.Error".Localize();
            status.color = UiTheme.Current.Palette.Error;
        }
    }

    private SubWorldTemplateAsset GetSelectedTemplate()
    {
        return FindTemplate(selectedTemplateId);
    }

    private SubWorldTemplateAsset FindTemplate(string templateId)
    {
        if (string.IsNullOrEmpty(templateId)) return null;
        for (int i = 0; i < selectableTemplates.Count; i++)
        {
            if (selectableTemplates[i].id == templateId) return selectableTemplates[i];
        }

        if (ModClass.L?.SubWorldTemplateLibrary == null) return null;
        SubWorldTemplateAsset template = ModClass.L.SubWorldTemplateLibrary.get(templateId);
        return template != null && template.allow_user_creation ? template : null;
    }

    private static string GetTemplateLabel(SubWorldTemplateAsset template)
    {
        string key = string.IsNullOrEmpty(template.display_name_key) ? template.id : template.display_name_key;
        return key.Localize();
    }

    private static string GetTemplateDescription(SubWorldTemplateAsset template)
    {
        string key = string.IsNullOrEmpty(template.description_key)
            ? "Cultiway.SubWorld.Create.TemplateDescription"
            : template.description_key;
        return key.Localize();
    }

    private SettingSpec FindSpec(string id)
    {
        for (int i = 0; i < SettingSpecs.Length; i++)
        {
            if (SettingSpecs[i].Id == id) return SettingSpecs[i];
        }

        throw new InvalidOperationException($"未注册的小世界生成设置: {id}");
    }

    private int GetInteger(string id)
    {
        return id switch
        {
            "random_shapes_amount" => draftSettings.random_shapes_amount,
            "cubicle_size" => draftSettings.cubicle_size,
            "perlin_scale_stage_1" => draftSettings.perlin_scale_stage_1,
            "perlin_scale_stage_2" => draftSettings.perlin_scale_stage_2,
            "perlin_scale_stage_3" => draftSettings.perlin_scale_stage_3,
            _ => 0
        };
    }

    private void SetInteger(string id, int value)
    {
        switch (id)
        {
            case "random_shapes_amount": draftSettings.random_shapes_amount = value; break;
            case "cubicle_size": draftSettings.cubicle_size = value; break;
            case "perlin_scale_stage_1": draftSettings.perlin_scale_stage_1 = value; break;
            case "perlin_scale_stage_2": draftSettings.perlin_scale_stage_2 = value; break;
            case "perlin_scale_stage_3": draftSettings.perlin_scale_stage_3 = value; break;
        }
    }

    private bool GetToggle(string id)
    {
        return id switch
        {
            "random_biomes" => draftSettings.random_biomes,
            "add_mountain_edges" => draftSettings.add_mountain_edges,
            "add_vegetation" => draftSettings.add_vegetation,
            "add_center_lake" => draftSettings.add_center_lake,
            "add_center_gradient_land" => draftSettings.add_center_gradient_land,
            "gradient_round_edges" => draftSettings.gradient_round_edges,
            "square_edges" => draftSettings.square_edges,
            "ring_effect" => draftSettings.ring_effect,
            "low_ground" => draftSettings.low_ground,
            "high_ground" => draftSettings.high_ground,
            "remove_mountains" => draftSettings.remove_mountains,
            _ => false
        };
    }

    private void SetToggle(string id, bool value)
    {
        switch (id)
        {
            case "random_biomes": draftSettings.random_biomes = value; break;
            case "add_mountain_edges": draftSettings.add_mountain_edges = value; break;
            case "add_vegetation": draftSettings.add_vegetation = value; break;
            case "add_center_lake": draftSettings.add_center_lake = value; break;
            case "add_center_gradient_land": draftSettings.add_center_gradient_land = value; break;
            case "gradient_round_edges": draftSettings.gradient_round_edges = value; break;
            case "square_edges": draftSettings.square_edges = value; break;
            case "ring_effect": draftSettings.ring_effect = value; break;
            case "low_ground": draftSettings.low_ground = value; break;
            case "high_ground": draftSettings.high_ground = value; break;
            case "remove_mountains": draftSettings.remove_mountains = value; break;
        }
    }

    private readonly struct SizeChoice
    {
        internal SizeChoice(int width, int height, string nameKey)
        {
            Width = width;
            Height = height;
            NameKey = nameKey;
        }

        internal int Width { get; }
        internal int Height { get; }
        internal string NameKey { get; }
    }

    private readonly struct SettingSpec
    {
        internal SettingSpec(string id, string labelKey, int minValue, int maxValue)
        {
            Id = id;
            LabelKey = labelKey;
            MinValue = minValue;
            MaxValue = maxValue;
            IsInteger = true;
        }

        internal SettingSpec(string id, string labelKey)
        {
            Id = id;
            LabelKey = labelKey;
            MinValue = 0;
            MaxValue = 1;
            IsInteger = false;
        }

        internal string Id { get; }
        internal string LabelKey { get; }
        internal int MinValue { get; }
        internal int MaxValue { get; }
        internal bool IsInteger { get; }
    }

    private sealed class SettingRow
    {
        private readonly SettingSpec spec;
        private readonly Text valueText;
        private readonly Button decreaseButton;
        private readonly Button increaseButton;
        private readonly Toggle toggle;

        internal SettingRow(Transform parent, SettingSpec spec, Action decrease, Action increase,
            Action<bool> toggleChanged)
        {
            this.spec = spec;
            Root = UiLayout.Create(parent, spec.Id, true, ContentWidth, 27f, 4f,
                TextAnchor.MiddleLeft);
            Image background = Root.AddComponent<Image>();
            UiResources.ApplySurface(background, UiSurface.WindowInner, UiTheme.Current.Palette.InnerPanelTint);

            float labelWidth = spec.IsInteger ? 94f : 150f;
            UiElements.CreateText(Root.transform, "Label", spec.LabelKey.Localize(), labelWidth, 25f, 7,
                TextAnchor.MiddleLeft);
            if (spec.IsInteger)
            {
                decreaseButton = UiElements.CreateIconButton(Root.transform, "Decrease", UiIcons.Remove,
                    24f, 24f, () => decrease());
                valueText = UiElements.CreateText(Root.transform, "Value", string.Empty, 32f, 25f, 8,
                    TextAnchor.MiddleCenter, FontStyle.Bold);
                increaseButton = UiElements.CreateIconButton(Root.transform, "Increase", UiIcons.Add,
                    24f, 24f, () => increase());
            }
            else
            {
                toggle = UiElements.CreateStateIconToggle(Root.transform, "Toggle", UiIcons.ToggleOff,
                    UiIcons.ToggleOn, false, 32f, 24f);
                toggle.onValueChanged.AddListener(value => toggleChanged(value));
            }
        }

        internal GameObject Root { get; }

        internal void Refresh(int value, bool toggleValue)
        {
            if (spec.IsInteger)
            {
                valueText.text = value.ToString();
                decreaseButton.interactable = value > spec.MinValue;
                increaseButton.interactable = value < spec.MaxValue;
                return;
            }

            UiElements.SetStateIconToggleWithoutNotify(toggle, toggleValue);
        }
    }
}
