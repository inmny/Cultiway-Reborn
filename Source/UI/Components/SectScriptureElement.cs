using System.Collections;
using System.Collections.Generic;
using Cultiway.Content;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using strings;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Cultiway.UI.Components;

internal class SectScriptureElement : WindowMetaElement<Sect, SectData>
{
    private const float ShowStepTime = 0.025f;
    private const float ContentWidth = 214f;
    private const string CultibookIconPath = "books/book_icons/cultibook/02";
    private const string SkillIconPath = "books/book_icons/cultibook/32";
    private const string ElixirIconPath = "books/book_icons/cultibook/12";

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
        GameObject rowObject = new("SectScriptureBookRowPrefab", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        rowObject.transform.SetParent(transform, false);
        rowObject.transform.localScale = Vector3.one;
        rowObject.SetActive(false);

        Image background = rowObject.GetComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0f);
        background.raycastTarget = true;

        HorizontalLayoutGroup rowLayout = rowObject.GetComponent<HorizontalLayoutGroup>();
        rowLayout.childControlHeight = false;
        rowLayout.childControlWidth = false;
        rowLayout.childForceExpandHeight = false;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childAlignment = TextAnchor.MiddleLeft;
        rowLayout.padding = new RectOffset(4, 4, 2, 2);
        rowLayout.spacing = 6f;

        LayoutElement rowLayoutElement = rowObject.GetComponent<LayoutElement>();
        rowLayoutElement.preferredWidth = ContentWidth;
        rowLayoutElement.preferredHeight = 38f;

        CultureBookButton vanillaPrefab = Resources.Load<CultureBookButton>("ui/PrefabBook")
                                          ?? throw new System.InvalidOperationException("SectScriptureElement 找不到 ui/PrefabBook 预制体");
        GameObject bookObject = Object.Instantiate(vanillaPrefab.gameObject, rowObject.transform, false);
        bookObject.name = "Book";
        CultureBookButton vanillaButton = bookObject.GetComponent<CultureBookButton>();
        Image cover = vanillaButton.cover;
        Image icon = vanillaButton.icon;
        TipButton bookTipButton = bookObject.GetComponent<TipButton>() ?? bookObject.AddComponent<TipButton>();
        Object.DestroyImmediate(vanillaButton);

        LayoutElement bookLayout = bookObject.GetComponent<LayoutElement>() ?? bookObject.AddComponent<LayoutElement>();
        bookLayout.preferredWidth = 24f;
        bookLayout.preferredHeight = 32f;
        bookLayout.flexibleWidth = 0f;

        Transform textRoot = CreateTextRoot(rowObject.transform);
        Text title = CreateText(textRoot, "Title", 7, Color.white, 16f);
        Text description = CreateText(textRoot, "Description", 6, new Color(0.78f, 0.78f, 0.78f), 14f);

        _rowPrefab = rowObject.AddComponent<SectScriptureBookRow>();
        _rowPrefab.Initialize(cover, icon, title, description, bookTipButton);
    }

    private static Transform CreateTextRoot(Transform parent)
    {
        GameObject root = new("Text", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        root.transform.SetParent(parent, false);
        root.transform.localScale = Vector3.one;

        VerticalLayoutGroup layout = root.GetComponent<VerticalLayoutGroup>();
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = false;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.spacing = 0f;

        LayoutElement layoutElement = root.GetComponent<LayoutElement>();
        layoutElement.preferredWidth = 176f;
        layoutElement.preferredHeight = 32f;
        return root.transform;
    }

    private static Text CreateText(Transform parent, string name, int fontSize, Color color, float height)
    {
        GameObject textObject = new(name, typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        textObject.transform.SetParent(parent, false);
        textObject.transform.localScale = Vector3.one;

        Text text = textObject.GetComponent<Text>();
        text.font = GetCurrentFont();
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleLeft;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;

        LayoutElement layout = textObject.GetComponent<LayoutElement>();
        layout.preferredWidth = 176f;
        layout.preferredHeight = height;
        return text;
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

    private static string GetBookDescription(Book book, BookTypeAsset bookType)
    {
        BookExtend bookExtend = book.GetExtend();
        if (bookType == BookTypes.Cultibook)
        {
            return GetCultibookDescription(bookExtend);
        }

        if (bookType == BookTypes.Skillbook)
        {
            return GetSkillbookDescription(bookExtend);
        }

        if (bookType == BookTypes.Elixirbook)
        {
            return GetElixirRecipeDescription(bookExtend);
        }

        return book.data.book_type;
    }

    private static string GetCultibookDescription(BookExtend bookExtend)
    {
        string typeName = "Cultiway.Sect.Scripture.Cultibooks".Localize();
        if (!bookExtend.HasComponent<Cultibook>()) return typeName;

        CultibookAsset cultibook = bookExtend.GetComponent<Cultibook>().Asset;
        return cultibook == null ? typeName : $"{typeName} · {cultibook.Level.GetName()}";
    }

    private static string GetSkillbookDescription(BookExtend bookExtend)
    {
        string typeName = "Cultiway.Sect.Scripture.Skills".Localize();
        if (!bookExtend.HasComponent<Skillbook>()) return typeName;

        Entity skill = bookExtend.GetComponent<Skillbook>().SkillContainer;
        if (skill.IsNull || !skill.HasComponent<SkillContainer>()) return typeName;

        SkillContainer container = skill.GetComponent<SkillContainer>();
        string skillName = skill.HasName ? skill.Name.value : container.SkillEntityAssetID.Localize();
        return string.IsNullOrEmpty(skillName) ? typeName : $"{typeName} · {skillName}";
    }

    private static string GetElixirRecipeDescription(BookExtend bookExtend)
    {
        string typeName = "Cultiway.Sect.Scripture.ElixirRecipes".Localize();
        if (!bookExtend.HasComponent<Elixirbook>()) return typeName;

        ElixirAsset elixir = bookExtend.GetComponent<Elixirbook>().Asset;
        return elixir == null ? typeName : $"{typeName} · {elixir.GetName()}";
    }

    private static Font GetCurrentFont()
    {
        return WorldboxGame.I?.CurrentFont ?? LocalizedTextManager.current_font ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
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
        private Text _description;
        [SerializeField]
        private TipButton _tipButton;
        [SerializeField]
        private TipButton _bookTipButton;
        private Book _book;

        internal void Initialize(Image cover, Image icon, Text title, Text description, TipButton bookTipButton)
        {
            _cover = cover;
            _icon = icon;
            _title = title;
            _description = description;
            _tipButton = GetComponent<TipButton>() ?? gameObject.AddComponent<TipButton>();
            _bookTipButton = bookTipButton;
            SetupTipButtons();
        }

        internal void Setup(Book book, BookTypeAsset bookType)
        {
            _book = book;
            BookTypeAsset actualBookType = book.getAsset();
            SetupTipButtons();
            _cover.sprite = SpriteTextureLoader.getSprite("books/book_covers/" + book.data.path_cover);
            _icon.sprite = SpriteTextureLoader.getSprite("books/book_icons/" + actualBookType.path_icons + book.data.path_icon);
            _title.text = book.data.name;
            _description.text = GetBookDescription(book, bookType);
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
