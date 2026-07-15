using System.IO;
using Cultiway.UI.ModInfoPages;
using Cultiway.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI;

public class WindowModInfo : TabbedWindow
{
    public const string WindowId = "Cultiway.UI.WindowModInfo";

    private const float ContentWidth = 214f;
    private const float HeaderContentHeight = 30f;
    private const float HeaderIconSize = 48f;
    private const float HeaderTextWidth = 176f;
    private const string HeaderContentName = "Cultiway Header Content";
    private static Sprite _modIconSprite;

    private static readonly IUiTabbedPage[] Pages =
    [
        new OverviewPage(),
        new CultivationPage(),
        new SectPage(),
        new SkillPage(),
        new ItemsPage(),
        new WorldPage(),
        new WarhammerPage(),
        new AIGCPage(),
    ];

    internal static void Init()
    {
        UiTabbedWindowAdapter.Create<WindowModInfo>(new UiTabbedWindowOptions(
            WindowId,
            "Cultiway.UI.WindowModInfo Title",
            "../../cultiway/icons/iconTab",
            "模组介绍窗口")
        {
            ContentWidth = ContentWidth,
            ConfigureHeader = ConfigureHeader,
        }, Pages);
    }

    private static void ConfigureHeader(Transform header)
    {
        header.gameObject.SetActive(true);
        Transform oldContent = header.Find(HeaderContentName);
        if (oldContent != null) DestroyImmediate(oldContent.gameObject);

        RectTransform headerRect = header.GetComponent<RectTransform>();
        headerRect.sizeDelta = new Vector2(228f, HeaderContentHeight);
        ConfigureHeaderRootLayout(header);

        GameObject content = UiLayout.Create(header, HeaderContentName, true, ContentWidth,
            HeaderContentHeight, UiTheme.Current.Metrics.SpacingMd, TextAnchor.MiddleLeft);
        HorizontalLayoutGroup layout = content.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(4, 4, 2, 2);

        GameObject iconObject = new("Mod Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        iconObject.transform.SetParent(content.transform, false);
        UiLayout.SetSize(iconObject.transform, HeaderIconSize, HeaderIconSize);
        Image icon = iconObject.GetComponent<Image>();
        icon.preserveAspect = true;
        icon.sprite = GetModIconSprite();
        icon.overrideSprite = icon.sprite;

        GameObject information = UiLayout.Create(content.transform, "Mod Info", false, HeaderTextWidth,
            24f, 0f, TextAnchor.MiddleLeft);
        Text title = UiElements.CreateText(information.transform, "Name Version", string.Empty,
            HeaderTextWidth, 13f, 8, TextAnchor.MiddleLeft, FontStyle.Bold);
        var declaration = ModClass.I.GetDeclaration();
        title.text = $"{declaration.Name}  v{declaration.Version}";
        title.resizeTextForBestFit = true;
        title.resizeTextMinSize = 5;
        title.resizeTextMaxSize = 8;

        Text authors = UiElements.CreateText(information.transform, "Authors", $"作者: {declaration.Author}",
            HeaderTextWidth, 11f, 6, TextAnchor.MiddleLeft);
        authors.color = UiTheme.Current.Palette.MutedText;
        authors.resizeTextForBestFit = true;
        authors.resizeTextMinSize = 4;
        authors.resizeTextMaxSize = 6;
        LayoutRebuilder.ForceRebuildLayoutImmediate(headerRect);
    }

    private static void ConfigureHeaderRootLayout(Transform header)
    {
        VerticalLayoutGroup layout = header.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(0, 0, 3, 3);
        layout.spacing = 0f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        LayoutElement headerLayout = header.GetComponent<LayoutElement>();
        headerLayout.minHeight = HeaderContentHeight + 6f;
        headerLayout.preferredHeight = HeaderContentHeight + 6f;
        headerLayout.flexibleHeight = 0f;
    }

    private static Sprite GetModIconSprite()
    {
        if (_modIconSprite != null) return _modIconSprite;

        var declaration = ModClass.I.GetDeclaration();
        string iconPath = string.IsNullOrWhiteSpace(declaration.IconPath) ? "icon.png" : declaration.IconPath;
        string fullPath = Path.Combine(declaration.FolderPath, iconPath);
        if (File.Exists(fullPath))
        {
            Texture2D texture = new(2, 2) { filterMode = FilterMode.Point };
            if (texture.LoadImage(File.ReadAllBytes(fullPath)))
            {
                _modIconSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f), 1f);
            }
        }

        return _modIconSprite ?? SpriteTextureLoader.getSprite("cultiway/icons/iconTab");
    }
}
