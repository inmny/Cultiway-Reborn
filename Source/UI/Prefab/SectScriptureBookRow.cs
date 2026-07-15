using Cultiway.Utils;
using Cultiway.Abstract;
using Cultiway.Content;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using strings;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Cultiway.UI.Prefab;

public class SectScriptureBookRow : APrefabPreview<SectScriptureBookRow>
{
    private const float RowWidth = 210f;
    private const float RowHeight = 31f;
    private const string HappinessIconPath = "ui/icons/iconHappiness_5";
    private const string ExperienceIconPath = "ui/icons/iconExperience";
    private const string ManaIconPath = "ui/icons/iconMana";
    private const string BooksReadIconPath = "ui/icons/iconBooksRead";
    private static readonly Color TitleColor = new(1f, 0.60730225f, 0.1102941f, 1f);
    private static readonly Color SecondaryTextColor = new(0.4f, 0.4f, 0.4f, 1f);
    private static readonly Color FallbackBackgroundColor = new(0.7735849f, 0.7735849f, 0.7735849f, 0.6313726f);

    [SerializeField]
    private Image _cover;
    [SerializeField]
    private Image _icon;
    [SerializeField]
    private Text _title;
    [SerializeField]
    private Text _rank;
    [SerializeField]
    private Text _author;
    [SerializeField]
    private SectScriptureStatValue _happiness;
    [SerializeField]
    private SectScriptureStatValue _experience;
    [SerializeField]
    private SectScriptureStatValue _mana;
    [SerializeField]
    private SectScriptureStatValue _reads;
    [SerializeField]
    private TipButton _tipButton;
    [SerializeField]
    private TipButton _bookTipButton;

    private Book _book;

    protected override void OnInit()
    {
        _tipButton ??= GetComponent<TipButton>();
        _bookTipButton ??= transform.Find("Book").GetComponent<TipButton>();
        _title ??= transform.Find("Name").GetComponent<Text>();
        _rank ??= transform.Find("Rank").GetComponent<Text>();
        _author ??= transform.Find("Author").GetComponent<Text>();
        _happiness ??= transform.Find("Happiness").GetComponent<SectScriptureStatValue>();
        _experience ??= transform.Find("Experience").GetComponent<SectScriptureStatValue>();
        _mana ??= transform.Find("Mana").GetComponent<SectScriptureStatValue>();
        _reads ??= transform.Find("Reads").GetComponent<SectScriptureStatValue>();
        SetupTipButton(_tipButton);
        SetupTipButton(_bookTipButton);
    }

    public void Setup(Book book, BookTypeAsset bookType)
    {
        Init();
        _book = book;
        BookTypeAsset actualBookType = book.getAsset();
        BookExtend bookExtend = book.GetExtend();

        _cover.sprite = SpriteTextureLoader.getSprite("books/book_covers/" + book.data.path_cover);
        _icon.sprite = SpriteTextureLoader.getSprite("books/book_icons/" + actualBookType.path_icons + book.data.path_icon);
        _title.text = book.data.name;
        _rank.text = GetBookRankText(bookExtend, bookType);
        _author.text = GetBookAuthorText(book);
        _happiness.Setup(SpriteTextureLoader.getSprite(HappinessIconPath), book.getHappiness());
        _experience.Setup(SpriteTextureLoader.getSprite(ExperienceIconPath), book.getExperience());
        _mana.Setup(SpriteTextureLoader.getSprite(ManaIconPath), book.getMana());
        _reads.Setup(SpriteTextureLoader.getSprite(BooksReadIconPath), book.data.times_read);
        name = $"SectScriptureBook_{book.id}";
    }

    private void SetupTipButton(TipButton tipButton)
    {
        tipButton.hoverAction = null;
        tipButton.setHoverAction(new TooltipAction(ShowTooltip), true);
    }

    private void ShowTooltip()
    {
        if (_book == null || _book.isRekt()) return;

        string tooltipId = _book.getAsset() == BookTypes.Cultibook ? Tooltips.Cultibook.id : S_Tooltip.book;
        Tooltip.show(gameObject, tooltipId, new TooltipData
        {
            book = _book
        });
    }

    private static void _init()
    {
        GameObject obj = ModClass.NewPrefabPreview(nameof(SectScriptureBookRow), typeof(Image), typeof(Button), typeof(TipButton), typeof(LayoutElement));
        SetRect(obj.GetComponent<RectTransform>(), new Vector2(RowWidth, RowHeight), Vector2.zero);

        Image background = obj.GetComponent<Image>();
        background.sprite = Resources.Load<ListWindow>("windows/list_kingdoms")?._list_element_prefab.GetComponent<Image>()?.sprite;
        background.color = background.sprite == null ? FallbackBackgroundColor : Color.white;
        background.raycastTarget = true;

        LayoutElement layout = obj.GetComponent<LayoutElement>();
        layout.preferredWidth = RowWidth;
        layout.preferredHeight = RowHeight;

        CultureBookButton bookButton = AddBookPreview(obj.transform);
        Text title = AddText(obj.transform, "Name", 7, FontStyle.Bold, TitleColor, new Vector2(112f, 13f), new Vector2(94f, 7.5f));
        Text rank = AddText(obj.transform, "Rank", 6, FontStyle.Bold, SecondaryTextColor, new Vector2(52f, 12f), new Vector2(178f, 7.5f), TextAnchor.MiddleRight);
        Text author = AddText(obj.transform, "Author", 6, FontStyle.Normal, SecondaryTextColor, new Vector2(58f, 12f), new Vector2(67f, -7f));
        SectScriptureStatValue happiness = AddStat(obj.transform, "Happiness", new Vector2(109f, -7f));
        SectScriptureStatValue experience = AddStat(obj.transform, "Experience", new Vector2(138.5f, -7f));
        SectScriptureStatValue mana = AddStat(obj.transform, "Mana", new Vector2(168f, -7f));
        SectScriptureStatValue reads = AddStat(obj.transform, "Reads", new Vector2(197.5f, -7f));

        Prefab = obj.AddComponent<SectScriptureBookRow>();
        Prefab._cover = bookButton.cover;
        Prefab._icon = bookButton.icon;
        Prefab._title = title;
        Prefab._rank = rank;
        Prefab._author = author;
        Prefab._happiness = happiness;
        Prefab._experience = experience;
        Prefab._mana = mana;
        Prefab._reads = reads;
        Prefab._tipButton = obj.GetComponent<TipButton>();
        Prefab._bookTipButton = bookButton.GetComponent<TipButton>();

        Object.DestroyImmediate(bookButton);
    }

    private static CultureBookButton AddBookPreview(Transform parent)
    {
        CultureBookButton prefab = Resources.Load<CultureBookButton>("ui/PrefabBook")
                                   ?? throw new System.InvalidOperationException("SectScriptureBookRow 找不到 ui/PrefabBook 预制体");
        GameObject book = Object.Instantiate(prefab.gameObject, parent, false);
        book.name = "Book";
        SetRect(book.GetComponent<RectTransform>(), new Vector2(29f, 29f), new Vector2(17f, 0f));
        return book.GetComponent<CultureBookButton>();
    }

    private static Text AddText(Transform parent, string name, int fontSize, FontStyle fontStyle, Color color, Vector2 size, Vector2 position, TextAnchor alignment = TextAnchor.MiddleLeft)
    {
        GameObject textObject = new(name, typeof(RectTransform), typeof(Text), typeof(Shadow));
        textObject.transform.SetParent(parent, false);
        textObject.transform.localScale = Vector3.one;
        SetRect(textObject.GetComponent<RectTransform>(), size, position);

        Text text = textObject.GetComponent<Text>();
        text.font = Cultiway.UI.UiTheme.Current.Font;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;

        Shadow shadow = textObject.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
        shadow.effectDistance = new Vector2(0.5f, -0.5f);
        return text;
    }

    private static SectScriptureStatValue AddStat(Transform parent, string name, Vector2 position)
    {
        SectScriptureStatValue stat = Object.Instantiate(SectScriptureStatValue.Prefab, parent, false);
        stat.name = name;
        SetRect(stat.GetComponent<RectTransform>(), new Vector2(25f, 12f), position);
        return stat;
    }

    private static void SetRect(RectTransform rect, Vector2 size, Vector2 position)
    {
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static string GetBookRankText(BookExtend bookExtend, BookTypeAsset bookType)
    {
        if (bookExtend.HasComponent<ItemLevel>())
        {
            return bookExtend.GetComponent<ItemLevel>().GetName();
        }

        if (bookType == BookTypes.Cultibook && bookExtend.HasComponent<Cultibook>())
        {
            CultibookAsset cultibook = bookExtend.GetComponent<Cultibook>().Asset;
            return cultibook == null ? GetBookTypeShortName(bookType) : cultibook.Level.GetName();
        }

        if (bookType == BookTypes.Elixirbook && bookExtend.HasComponent<Elixirbook>())
        {
            ElixirAsset elixir = bookExtend.GetComponent<Elixirbook>().Asset;
            return elixir == null ? GetBookTypeShortName(bookType) : elixir.base_level.GetName();
        }

        return GetBookTypeShortName(bookType);
    }

    private static string GetBookTypeShortName(BookTypeAsset bookType)
    {
        if (bookType == BookTypes.Cultibook) return "功法";
        if (bookType == BookTypes.Skillbook) return "术法";
        if (bookType == BookTypes.Elixirbook) return "丹方";
        return bookType.id.Localize();
    }

    private static string GetBookAuthorText(Book book)
    {
        return string.IsNullOrEmpty(book.data.author_name) ? "未知" : book.data.author_name;
    }

}
