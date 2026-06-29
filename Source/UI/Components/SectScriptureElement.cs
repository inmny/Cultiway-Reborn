using System.Collections;
using System.Collections.Generic;
using Cultiway.Content;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using strings;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Cultiway.UI.Components;

internal class SectScriptureElement : WindowMetaElement<Sect, SectData>
{
    private const float ShowStepTime = 0.025f;
    private const float ContentWidth = 214f;
    private const float ListRowWidth = 210f;
    private const float ListRowHeight = 31f;
    private const string CultibookIconPath = "books/book_icons/cultibook/02";
    private const string SkillIconPath = "books/book_icons/cultibook/32";
    private const string ElixirIconPath = "books/book_icons/cultibook/12";
    private const string HappinessIconPath = "ui/icons/iconHappiness_5";
    private const string ExperienceIconPath = "ui/icons/iconExperience";
    private const string ManaIconPath = "ui/icons/iconMana";
    private const string BooksReadIconPath = "ui/icons/iconBooksRead";
    private static readonly Color ListTitleColor = new(1f, 0.60730225f, 0.1102941f, 1f);
    private static readonly Color ListSubtitleColor = new(0.4f, 0.4f, 0.4f, 1f);
    private static readonly Color ListValueColor = new(1f, 0.60730225f, 0.1102941f, 1f);
    private static readonly Color FallbackListBackgroundColor = new(0.7735849f, 0.7735849f, 0.7735849f, 0.6313726f);
    private static readonly Color IconValueBackgroundColor = new(0.7735849f, 0.7735849f, 0.7735849f, 0.6313726f);
    private static Sprite _listRowBackgroundSprite;
    private static Sprite _iconValueBackgroundSprite;

    private Transform _typeTabsContainer;
    private Transform _listRoot;
    private SectScriptureBookRow _rowPrefab;
    private ObjectPoolGenericMono<SectScriptureBookRow> _rowPool;
    private GameObject _emptyMessage;
    private BookTypeAsset _selectedBookType;
    private bool _initialized;

    internal void Initialize(Transform typeTabsContainer)
    {
        if (_initialized) return;
        _initialized = true;
        _typeTabsContainer = typeTabsContainer;
        _selectedBookType = BookTypes.Cultibook;

        SetupLayout();
        RemoveVanillaBooksElements();
        SetupTitle();
        SetupTypeTabs();
        CreateBookRowPrefab();
        _listRoot = CreateBookList(transform);
        _rowPool = new ObjectPoolGenericMono<SectScriptureBookRow>(_rowPrefab, _listRoot);
        _emptyMessage = CreateEmptyMessage(transform);
    }

    public override IEnumerator showContent()
    {
        Initialize(_typeTabsContainer);

        Sect sect = meta_object;
        if (sect == null || sect.isRekt()) yield break;

        List<Book> books = sect.GetScriptureBooks(_selectedBookType);
        bool hasBooks = books.Count > 0;
        _emptyMessage.SetActive(!hasBooks);
        _listRoot.gameObject.SetActive(hasBooks);

        for (int i = 0; i < books.Count; i++)
        {
            SectScriptureBookRow row = _rowPool.getNext();
            row.Setup(books[i], _selectedBookType);
            yield return new WaitForSecondsRealtime(ShowStepTime);
        }
    }

    public override void clear()
    {
        _rowPool?.clear();
        if (_listRoot != null)
        {
            _listRoot.gameObject.SetActive(false);
        }

        if (_emptyMessage != null)
        {
            _emptyMessage.SetActive(false);
        }

        base.clear();
    }

    private void SetupLayout()
    {
        VerticalLayoutGroup layout = GetComponent<VerticalLayoutGroup>() ?? gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = false;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.spacing = 4f;

        ContentSizeFitter fitter = GetComponent<ContentSizeFitter>() ?? gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
    }

    private void RemoveVanillaBooksElements()
    {
        DestroyIfPresent("Books Grid");
        DestroyIfPresent("content_books_no_items");
        DestroyIfPresent("Title Books");
    }

    private void DestroyIfPresent(string name)
    {
        Transform target = transform.FindRecursive(name);
        if (target != null)
        {
            Object.DestroyImmediate(target.gameObject);
        }
    }

    private void SetupTitle()
    {
        Transform titleContainer = transform.FindRecursive("tab_title_container_books");
        if (titleContainer == null) return;

        titleContainer.Find("title_tab")?.GetComponent<LocalizedText>()?.setKeyAndUpdate("Cultiway.Sect.ScripturePavilion");

        Sprite icon = SpriteTextureLoader.getSprite("ui/icons/iconBooks");
        SetTitleIcon(titleContainer, "icon_left", icon);
        SetTitleIcon(titleContainer, "icon_right", icon);
    }

    private void SetupTypeTabs()
    {
        if (_typeTabsContainer == null) return;

        TabTogglesGroup group = _typeTabsContainer.GetComponent<TabTogglesGroup>() ?? _typeTabsContainer.gameObject.AddComponent<TabTogglesGroup>();
        group.tryAddButton(CultibookIconPath, "Cultiway.Sect.Scripture.Cultibooks", new TabToggleAction(refresh), new TabToggleAction(() => _selectedBookType = BookTypes.Cultibook));
        group.tryAddButton(SkillIconPath, "Cultiway.Sect.Scripture.Skills", new TabToggleAction(refresh), new TabToggleAction(() => _selectedBookType = BookTypes.Skillbook));
        group.tryAddButton(ElixirIconPath, "Cultiway.Sect.Scripture.ElixirRecipes", new TabToggleAction(refresh), new TabToggleAction(() => _selectedBookType = BookTypes.Elixirbook));

        TabToggle first = _typeTabsContainer.Find("Cultiway.Sect.Scripture.Cultibooks")?.GetComponentInChildren<TabToggle>(true);
        first?.select();
    }

    private void CreateBookRowPrefab()
    {
        GameObject rowObject = new("SectScriptureBookRowPrefab", typeof(RectTransform), typeof(Image), typeof(Button), typeof(TipButton), typeof(LayoutElement));
        rowObject.transform.SetParent(transform, false);
        rowObject.transform.localScale = Vector3.one;
        rowObject.SetActive(false);

        Image background = rowObject.GetComponent<Image>();
        background.sprite = GetListRowBackgroundSprite();
        background.color = background.sprite == null ? FallbackListBackgroundColor : Color.white;
        background.raycastTarget = true;

        LayoutElement rowLayoutElement = rowObject.GetComponent<LayoutElement>();
        rowLayoutElement.preferredWidth = ListRowWidth;
        rowLayoutElement.preferredHeight = ListRowHeight;

        CultureBookButton vanillaPrefab = Resources.Load<CultureBookButton>("ui/PrefabBook")
                                          ?? throw new System.InvalidOperationException("SectScriptureElement 找不到 ui/PrefabBook 预制体");
        GameObject bookObject = Object.Instantiate(vanillaPrefab.gameObject, rowObject.transform, false);
        bookObject.name = "Book";
        RectTransform bookRect = bookObject.GetComponent<RectTransform>();
        bookRect.anchorMin = new Vector2(0f, 0.5f);
        bookRect.anchorMax = new Vector2(0f, 0.5f);
        bookRect.pivot = new Vector2(0.5f, 0.5f);
        bookRect.anchoredPosition = new Vector2(17f, 0f);
        bookRect.sizeDelta = new Vector2(29f, 29f);

        CultureBookButton vanillaButton = bookObject.GetComponent<CultureBookButton>();
        Image cover = vanillaButton.cover;
        Image icon = vanillaButton.icon;
        TipButton bookTipButton = bookObject.GetComponent<TipButton>() ?? bookObject.AddComponent<TipButton>();
        Object.DestroyImmediate(vanillaButton);

        Text title = CreateListText(rowObject.transform, "Name", 7, FontStyle.Bold, ListTitleColor, new Vector2(112f, 13f), new Vector2(94f, 7.5f));
        Text rank = CreateListText(rowObject.transform, "Rank", 6, FontStyle.Bold, ListSubtitleColor, new Vector2(52f, 12f), new Vector2(178f, 7.5f), TextAnchor.MiddleRight);
        Text author = CreateListText(rowObject.transform, "Author", 6, FontStyle.Normal, ListSubtitleColor, new Vector2(58f, 12f), new Vector2(67f, -7f));
        BookStatWidget happiness = CreateBookStat(rowObject.transform, "Happiness", HappinessIconPath, new Vector2(109f, -7f));
        BookStatWidget experience = CreateBookStat(rowObject.transform, "Experience", ExperienceIconPath, new Vector2(138.5f, -7f));
        BookStatWidget mana = CreateBookStat(rowObject.transform, "Mana", ManaIconPath, new Vector2(168f, -7f));
        BookStatWidget reads = CreateBookStat(rowObject.transform, "Reads", BooksReadIconPath, new Vector2(197.5f, -7f));

        _rowPrefab = rowObject.AddComponent<SectScriptureBookRow>();
        _rowPrefab.Initialize(cover, icon, title, rank, author, happiness, experience, mana, reads, bookTipButton);
    }

    private static Sprite GetListRowBackgroundSprite()
    {
        if (_listRowBackgroundSprite != null) return _listRowBackgroundSprite;

        GameObject listElementPrefab = Resources.Load<ListWindow>("windows/list_kingdoms")?._list_element_prefab;
        Image background = listElementPrefab == null ? null : listElementPrefab.GetComponent<Image>();
        _listRowBackgroundSprite = background?.sprite;
        return _listRowBackgroundSprite;
    }

    private static Sprite GetIconValueBackgroundSprite()
    {
        if (_iconValueBackgroundSprite != null) return _iconValueBackgroundSprite;

        Image background = Resources.Load<GameObject>("ui/IconValue")?.GetComponent<Image>();
        _iconValueBackgroundSprite = background?.sprite;
        return _iconValueBackgroundSprite;
    }

    private static Text CreateListText(Transform parent, string name, int fontSize, FontStyle fontStyle, Color color, Vector2 size, Vector2 anchoredPosition, TextAnchor alignment = TextAnchor.MiddleLeft)
    {
        GameObject textObject = new(name, typeof(RectTransform), typeof(Text), typeof(Shadow));
        textObject.transform.SetParent(parent, false);
        textObject.transform.localScale = Vector3.one;

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Text text = textObject.GetComponent<Text>();
        text.font = GetCurrentFont();
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

    private static Image CreateListIcon(Transform parent, string name, Vector2 size, Vector2 anchoredPosition)
    {
        GameObject iconObject = new(name, typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(parent, false);
        iconObject.transform.localScale = Vector3.one;

        RectTransform rect = iconObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = iconObject.GetComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    private static BookStatWidget CreateBookStat(Transform parent, string name, string iconPath, Vector2 anchoredPosition)
    {
        GameObject statObject = new(name, typeof(RectTransform), typeof(Image));
        statObject.transform.SetParent(parent, false);
        statObject.transform.localScale = Vector3.one;

        RectTransform rect = statObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(25f, 12f);

        Image background = statObject.GetComponent<Image>();
        background.sprite = GetIconValueBackgroundSprite();
        background.type = background.sprite == null ? Image.Type.Simple : Image.Type.Sliced;
        background.color = IconValueBackgroundColor;
        background.raycastTarget = false;

        Image icon = CreateListIcon(statObject.transform, "Icon", new Vector2(9f, 9f), new Vector2(5.5f, 0f));
        icon.sprite = SpriteTextureLoader.getSprite(iconPath);
        Text value = CreateListText(statObject.transform, "Value", 6, FontStyle.Bold, ListValueColor, new Vector2(14f, 12f), new Vector2(18f, 0f), TextAnchor.MiddleCenter);
        value.horizontalOverflow = HorizontalWrapMode.Overflow;
        BookStatWidget widget = statObject.AddComponent<BookStatWidget>();
        widget.Initialize(icon, value);
        return widget;
    }

    private static Transform CreateBookList(Transform parent)
    {
        GameObject listObject = new("scripture_book_list", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement));
        listObject.transform.SetParent(parent, false);
        listObject.transform.localScale = Vector3.one;

        VerticalLayoutGroup layout = listObject.GetComponent<VerticalLayoutGroup>();
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = false;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.spacing = 2f;

        ContentSizeFitter fitter = listObject.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        LayoutElement layoutElement = listObject.GetComponent<LayoutElement>();
        layoutElement.preferredWidth = ContentWidth;
        return listObject.transform;
    }

    private static void SetTitleIcon(Transform titleContainer, string childName, Sprite icon)
    {
        Image image = titleContainer.Find(childName)?.GetComponent<Image>();
        if (image != null)
        {
            image.sprite = icon;
        }
    }

    private static GameObject CreateEmptyMessage(Transform parent)
    {
        GameObject messageObject = new("content_sect_scripture_empty", typeof(RectTransform), typeof(Text), typeof(Shadow), typeof(LayoutElement));
        messageObject.transform.SetParent(parent, false);
        messageObject.transform.localScale = Vector3.one;

        Text text = messageObject.GetComponent<Text>();
        text.font = GetCurrentFont();
        text.fontSize = 7;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;
        text.text = "Cultiway.Sect.Scripture.Empty".Localize();

        Shadow shadow = messageObject.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
        shadow.effectDistance = new Vector2(0.5f, -0.5f);

        LayoutElement layout = messageObject.GetComponent<LayoutElement>();
        layout.preferredWidth = ContentWidth;
        layout.preferredHeight = 20f;
        return messageObject;
    }

    private static string GetBookTypeShortName(BookTypeAsset bookType)
    {
        if (bookType == BookTypes.Cultibook) return "功法";
        if (bookType == BookTypes.Skillbook) return "术法";
        if (bookType == BookTypes.Elixirbook) return "丹方";
        return bookType.id.Localize();
    }

    private static string GetBookRankText(BookExtend bookExtend, BookTypeAsset bookType)
    {
        if (bookExtend.HasComponent<ItemLevel>())
        {
            return bookExtend.GetComponent<ItemLevel>().GetName();
        }

        if (bookType == BookTypes.Cultibook && bookExtend.HasComponent<Cultibook>())
        {
            var cultibook = bookExtend.GetComponent<Cultibook>().Asset;
            return cultibook == null ? GetBookTypeShortName(bookType) : cultibook.Level.GetName();
        }

        if (bookType == BookTypes.Elixirbook && bookExtend.HasComponent<Elixirbook>())
        {
            var elixir = bookExtend.GetComponent<Elixirbook>().Asset;
            return elixir == null ? GetBookTypeShortName(bookType) : elixir.base_level.GetName();
        }

        return GetBookTypeShortName(bookType);
    }

    private static string GetBookAuthorText(Book book)
    {
        string author = string.IsNullOrEmpty(book.data.author_name) ? "未知" : book.data.author_name;
        return author;
    }

    private static Font GetCurrentFont()
    {
        return WorldboxGame.I?.CurrentFont ?? LocalizedTextManager.current_font ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    private sealed class BookStatWidget : MonoBehaviour
    {
        [SerializeField]
        private Image _icon;
        [SerializeField]
        private Text _value;

        internal void Initialize(Image icon, Text value)
        {
            _icon = icon;
            _value = value;
        }

        internal void SetValue(int value)
        {
            gameObject.SetActive(true);
            _value.text = Toolbox.formatNumber(value, 4);
            _value.color = ListValueColor;
            _icon.color = Color.white;
        }
    }

    private sealed class SectScriptureBookRow : MonoBehaviour
    {
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
        private BookStatWidget _happiness;
        [SerializeField]
        private BookStatWidget _experience;
        [SerializeField]
        private BookStatWidget _mana;
        [SerializeField]
        private BookStatWidget _reads;
        [SerializeField]
        private TipButton _tipButton;
        [SerializeField]
        private TipButton _bookTipButton;
        private Book _book;

        internal void Initialize(
            Image cover,
            Image icon,
            Text title,
            Text rank,
            Text author,
            BookStatWidget happiness,
            BookStatWidget experience,
            BookStatWidget mana,
            BookStatWidget reads,
            TipButton bookTipButton)
        {
            _cover = cover;
            _icon = icon;
            _title = title;
            _rank = rank;
            _author = author;
            _happiness = happiness;
            _experience = experience;
            _mana = mana;
            _reads = reads;
            _tipButton = GetComponent<TipButton>() ?? gameObject.AddComponent<TipButton>();
            _bookTipButton = bookTipButton;
            SetupTipButtons();
        }

        internal void Setup(Book book, BookTypeAsset bookType)
        {
            _book = book;
            BookTypeAsset actualBookType = book.getAsset();
            BookExtend bookExtend = book.GetExtend();
            SetupTipButtons();
            _cover.sprite = SpriteTextureLoader.getSprite("books/book_covers/" + book.data.path_cover);
            _icon.sprite = SpriteTextureLoader.getSprite("books/book_icons/" + actualBookType.path_icons + book.data.path_icon);
            _title.text = book.data.name;
            _rank.text = GetBookRankText(bookExtend, bookType);
            _author.text = GetBookAuthorText(book);
            _happiness.SetValue(book.getHappiness());
            _experience.SetValue(book.getExperience());
            _mana.SetValue(book.getMana());
            _reads.SetValue(book.data.times_read);
            gameObject.name = $"SectScriptureBook_{book.id}";
        }

        private void SetupTipButtons()
        {
            SetupTipButton(_tipButton);
            SetupTipButton(_bookTipButton);
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
    }
}
