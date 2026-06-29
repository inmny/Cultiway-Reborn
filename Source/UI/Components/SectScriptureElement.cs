using Cultiway.Utils;
using System.Collections;
using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Content;
using Cultiway.Core;
using Cultiway.UI.Prefab;
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
    private const string CultibookIconPath = "books/book_icons/cultibook/02";
    private const string SkillIconPath = "books/book_icons/cultibook/32";
    private const string ElixirIconPath = "books/book_icons/cultibook/12";

    private Transform _typeTabsContainer;
    private Transform _listRoot;
    private MonoObjPool<SectScriptureBookRow> _rowPool;
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

        _listRoot = CreateBookList(transform);
        _rowPool = new MonoObjPool<SectScriptureBookRow>(SectScriptureBookRow.Prefab, _listRoot);
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
            SectScriptureBookRow row = _rowPool.GetNext();
            row.Setup(books[i], _selectedBookType);
            yield return new WaitForSecondsRealtime(ShowStepTime);
        }
    }

    public override void clear()
    {
        _rowPool?.Clear();
        _listRoot.SetActiveIfPresent(false);
        if (_emptyMessage != null) _emptyMessage.SetActive(false);
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
        if (target == null) return;

        Object.DestroyImmediate(target.gameObject);
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
        if (image == null) return;

        image.sprite = icon;
    }

    private static GameObject CreateEmptyMessage(Transform parent)
    {
        GameObject messageObject = new("content_sect_scripture_empty", typeof(RectTransform), typeof(Text), typeof(Shadow), typeof(LayoutElement));
        messageObject.transform.SetParent(parent, false);
        messageObject.transform.localScale = Vector3.one;

        Text text = messageObject.GetComponent<Text>();
        text.font = UIUtils.GetCurrentFont();
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

}
