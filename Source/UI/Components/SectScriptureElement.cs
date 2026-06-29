using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Content;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Cultiway.UI.Components;

internal class SectScriptureElement : WindowMetaElement<Sect, SectData>
{
    private const float ShowStepTime = 0.025f;
    private const string CultibookCoverPath = "books/custom_book_covers/cultibook/01";
    private const string SkillCoverPath = "books/custom_book_covers/cultibook/31";
    private const string ElixirCoverPath = "books/custom_book_covers/cultibook/51";
    private const string CultibookIconPath = "books/book_icons/cultibook/02";
    private const string SkillIconPath = "books/book_icons/cultibook/32";
    private const string ElixirIconPath = "books/book_icons/cultibook/12";

    private readonly List<BookSection> _sections = new();
    private SectScriptureBookButton _bookPrefab;
    private GameObject _emptyMessage;
    private bool _initialized;

    internal void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        SetupLayout();
        RemoveVanillaBooksElements();
        SetupTitle();
        CreateBookPrefab();

        _sections.Add(CreateSection("Cultiway.Sect.Scripture.Cultibooks"));
        _sections.Add(CreateSection("Cultiway.Sect.Scripture.Skills"));
        _sections.Add(CreateSection("Cultiway.Sect.Scripture.ElixirRecipes"));
        _emptyMessage = CreateEmptyMessage(transform);
    }

    public override IEnumerator showContent()
    {
        Initialize();

        Sect sect = meta_object;
        if (sect == null || sect.isRekt()) yield break;

        List<ScriptureBookEntry> cultibooks = BuildCultibookEntries(sect);
        List<ScriptureBookEntry> skills = BuildSkillEntries(sect);
        List<ScriptureBookEntry> elixirs = BuildElixirEntries(sect);

        int totalCount = cultibooks.Count + skills.Count + elixirs.Count;
        _emptyMessage.SetActive(totalCount == 0);

        yield return ShowSection(_sections[0], cultibooks);
        yield return ShowSection(_sections[1], skills);
        yield return ShowSection(_sections[2], elixirs);
    }

    public override void clear()
    {
        for (int i = 0; i < _sections.Count; i++)
        {
            _sections[i].Clear();
        }

        if (_emptyMessage != null)
        {
            _emptyMessage.SetActive(false);
        }

        base.clear();
    }

    private IEnumerator ShowSection(BookSection section, IReadOnlyList<ScriptureBookEntry> entries)
    {
        section.SetCount(entries.Count);
        section.Root.SetActive(entries.Count > 0);

        for (int i = 0; i < entries.Count; i++)
        {
            SectScriptureBookButton button = section.Pool.getNext();
            button.Setup(entries[i]);
            yield return new WaitForSecondsRealtime(ShowStepTime);
        }
    }

    private void SetupLayout()
    {
        VerticalLayoutGroup layout = GetComponent<VerticalLayoutGroup>() ?? gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = false;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.spacing = 6f;

        ContentSizeFitter fitter = GetComponent<ContentSizeFitter>() ?? gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
    }

    private void RemoveVanillaBooksElements()
    {
        Transform booksGrid = transform.FindRecursive("Books Grid");
        if (booksGrid != null)
        {
            Object.DestroyImmediate(booksGrid.gameObject);
        }

        Transform noItems = transform.FindRecursive("content_books_no_items");
        if (noItems != null)
        {
            Object.DestroyImmediate(noItems.gameObject);
        }
    }

    private void SetupTitle()
    {
        Transform titleContainer = transform.FindRecursive("tab_title_container_books");
        if (titleContainer == null) return;

        LocalizedText title = titleContainer.Find("title_tab")?.GetComponent<LocalizedText>();
        title?.setKeyAndUpdate("Cultiway.Sect.ScripturePavilion");

        Sprite icon = SpriteTextureLoader.getSprite("ui/icons/iconBooks");
        SetTitleIcon(titleContainer, "icon_left", icon);
        SetTitleIcon(titleContainer, "icon_right", icon);
    }

    private void CreateBookPrefab()
    {
        CultureBookButton vanillaPrefab = Resources.Load<CultureBookButton>("ui/PrefabBook")
                                          ?? throw new InvalidOperationException("SectScriptureElement 找不到 ui/PrefabBook 预制体");
        GameObject prefabObject = Object.Instantiate(vanillaPrefab.gameObject, transform, false);
        prefabObject.name = "SectScriptureBookButtonPrefab";
        prefabObject.SetActive(false);

        CultureBookButton vanillaButton = prefabObject.GetComponent<CultureBookButton>();
        Image cover = vanillaButton.cover;
        Image icon = vanillaButton.icon;
        Object.DestroyImmediate(vanillaButton);

        _bookPrefab = prefabObject.AddComponent<SectScriptureBookButton>();
        _bookPrefab.Initialize(cover, icon);
    }

    private BookSection CreateSection(string titleKey)
    {
        GameObject root = new(titleKey, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement));
        root.transform.SetParent(transform, false);
        root.transform.localScale = Vector3.one;

        VerticalLayoutGroup rootLayout = root.GetComponent<VerticalLayoutGroup>();
        rootLayout.childControlHeight = true;
        rootLayout.childControlWidth = true;
        rootLayout.childForceExpandHeight = false;
        rootLayout.childForceExpandWidth = false;
        rootLayout.childAlignment = TextAnchor.UpperCenter;
        rootLayout.spacing = 2f;

        ContentSizeFitter rootFitter = root.GetComponent<ContentSizeFitter>();
        rootFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        LayoutElement rootLayoutElement = root.GetComponent<LayoutElement>();
        rootLayoutElement.preferredWidth = 214f;

        Text title = CreateSectionTitle(root.transform, titleKey);
        Transform grid = CreateBookGrid(root.transform);
        ObjectPoolGenericMono<SectScriptureBookButton> pool = new(_bookPrefab, grid);

        return new BookSection(root, title, titleKey, pool);
    }

    private static Text CreateSectionTitle(Transform parent, string titleKey)
    {
        GameObject titleObject = new($"title_{titleKey}", typeof(RectTransform), typeof(Text), typeof(Shadow), typeof(LayoutElement));
        titleObject.transform.SetParent(parent, false);
        titleObject.transform.localScale = Vector3.one;

        Text title = titleObject.GetComponent<Text>();
        title.font = GetCurrentFont();
        title.fontSize = 7;
        title.alignment = TextAnchor.MiddleCenter;
        title.color = Color.white;
        title.raycastTarget = false;

        Shadow shadow = titleObject.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
        shadow.effectDistance = new Vector2(0.5f, -0.5f);

        LayoutElement layout = titleObject.GetComponent<LayoutElement>();
        layout.preferredWidth = 214f;
        layout.preferredHeight = 12f;

        title.text = titleKey.Localize();
        return title;
    }

    private static Transform CreateBookGrid(Transform parent)
    {
        GameObject gridObject = new("Books Grid", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement));
        gridObject.transform.SetParent(parent, false);
        gridObject.transform.localScale = Vector3.one;

        GridLayoutGroup grid = gridObject.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(24f, 32f);
        grid.spacing = new Vector2(3f, 3f);
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 7;
        grid.childAlignment = TextAnchor.UpperCenter;

        ContentSizeFitter fitter = gridObject.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        LayoutElement layout = gridObject.GetComponent<LayoutElement>();
        layout.preferredWidth = 214f;

        return gridObject.transform;
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
        layout.preferredWidth = 214f;
        layout.preferredHeight = 20f;

        return messageObject;
    }

    private List<ScriptureBookEntry> BuildCultibookEntries(Sect sect)
    {
        return sect.GetAllMaster<CultibookAsset>()
            .Where(item => item.Item1 != null)
            .OrderByDescending(item => item.Item1.Level.Stage)
            .ThenBy(item => item.Item1.Name)
            .Select(item => CreateCultibookEntry(item.Item1, item.Item2))
            .ToList();
    }

    private static ScriptureBookEntry CreateCultibookEntry(CultibookAsset cultibook, float mastery)
    {
        List<string> lines = new()
        {
            $"{GetCategoryName("Cultiway.Sect.Scripture.Cultibooks")} · {mastery:F0}%",
            $"{ "Cultiway.Sect.Scripture.Level".Localize() }: {cultibook.Level.GetName()}"
        };

        if (!string.IsNullOrEmpty(cultibook.CultivateMethodId))
        {
            CultivateMethodAsset method = cultibook.GetCultivateMethod();
            if (method != null)
            {
                lines.Add($"{ "Cultiway.Sect.Scripture.CultivateMethod".Localize() }: {method.id.Localize()}");
            }
        }

        int skillCount = cultibook.SkillPool?.Count ?? 0;
        if (skillCount > 0)
        {
            lines.Add($"{ "Cultiway.Sect.Scripture.SkillPool".Localize() }: {skillCount}");
        }

        return new ScriptureBookEntry(
            $"cultibook_{cultibook.id}",
            $"《{cultibook.Name}》",
            string.Join("\n", lines),
            CultibookCoverPath,
            CultibookIconPath);
    }

    private static List<ScriptureBookEntry> BuildSkillEntries(Sect sect)
    {
        Dictionary<string, ScriptureBookEntry> entries = new();
        foreach ((CultibookAsset cultibook, _) in sect.GetAllMaster<CultibookAsset>())
        {
            if (cultibook?.SkillPool == null) continue;

            foreach (SkillPoolEntry skillEntry in cultibook.SkillPool)
            {
                if (skillEntry.SkillContainer.IsNull || !skillEntry.SkillContainer.HasComponent<SkillContainer>()) continue;

                Entity skill = skillEntry.SkillContainer;
                SkillContainer container = skill.GetComponent<SkillContainer>();
                if (string.IsNullOrEmpty(container.SkillEntityAssetID)) continue;

                string title = GetSkillName(skill, container);
                string key = $"{container.SkillEntityAssetID}:{title}";
                if (entries.ContainsKey(key)) continue;

                List<string> lines = new()
                {
                    $"{GetCategoryName("Cultiway.Sect.Scripture.Skills")} · {cultibook.Name}"
                };

                if (skillEntry.MasteryThreshold > 0)
                {
                    lines.Add($"{ "Cultiway.Sect.Scripture.MasteryRequirement".Localize() }: {skillEntry.MasteryThreshold:F0}%");
                }

                if (skillEntry.LevelRequirement > 0)
                {
                    lines.Add($"{ "Cultiway.Sect.Scripture.LevelRequirement".Localize() }: {Cultisyses.Xian.GetLevelName(skillEntry.LevelRequirement)}");
                }

                entries.Add(key, new ScriptureBookEntry(
                    $"skill_{key}",
                    title,
                    string.Join("\n", lines),
                    SkillCoverPath,
                    SkillIconPath));
            }
        }

        return entries.Values.OrderBy(entry => entry.Title).ToList();
    }

    private static string GetSkillName(Entity skill, SkillContainer container)
    {
        if (skill.HasName) return skill.Name.value;

        if (!string.IsNullOrEmpty(container.SkillEntityAssetID))
        {
            return container.SkillEntityAssetID.Localize();
        }

        return "Cultiway.Sect.Scripture.UnknownSkill".Localize();
    }

    private List<ScriptureBookEntry> BuildElixirEntries(Sect sect)
    {
        return sect.GetAllMaster<ElixirAsset>()
            .Where(item => item.Item1 != null)
            .OrderBy(item => item.Item1.GetName())
            .Select(item => CreateElixirEntry(item.Item1, item.Item2))
            .ToList();
    }

    private static ScriptureBookEntry CreateElixirEntry(ElixirAsset elixir, float mastery)
    {
        List<string> lines = new()
        {
            $"{GetCategoryName("Cultiway.Sect.Scripture.ElixirRecipes")} · {mastery:F0}%"
        };

        if (!string.IsNullOrEmpty(elixir.description_key))
        {
            lines.Add(elixir.description_key.Localize());
        }

        if (elixir.ingredients != null && elixir.ingredients.Length > 0)
        {
            string ingredients = string.Join("、", elixir.ingredients.Select(ingredient => ingredient.GetName()).Where(name => !string.IsNullOrEmpty(name)));
            if (!string.IsNullOrEmpty(ingredients))
            {
                lines.Add($"{ "Cultiway.Sect.Scripture.Ingredients".Localize() }: {ingredients}");
            }
        }

        string name = elixir.GetName();
        return new ScriptureBookEntry(
            $"elixir_{elixir.id}",
            name.EndsWith("丹方", StringComparison.Ordinal) ? name : $"{name}丹方",
            string.Join("\n", lines),
            ElixirCoverPath,
            ElixirIconPath);
    }

    private static string GetCategoryName(string key)
    {
        return key.Localize();
    }

    private static Font GetCurrentFont()
    {
        return WorldboxGame.I?.CurrentFont ?? LocalizedTextManager.current_font ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    private readonly struct ScriptureBookEntry
    {
        internal readonly string Id;
        internal readonly string Title;
        internal readonly string Description;
        internal readonly string CoverPath;
        internal readonly string IconPath;

        internal ScriptureBookEntry(string id, string title, string description, string coverPath, string iconPath)
        {
            Id = id;
            Title = title;
            Description = description;
            CoverPath = coverPath;
            IconPath = iconPath;
        }
    }

    private sealed class BookSection
    {
        internal readonly GameObject Root;
        internal readonly Text Title;
        internal readonly string TitleKey;
        internal readonly ObjectPoolGenericMono<SectScriptureBookButton> Pool;

        internal BookSection(GameObject root, Text title, string titleKey, ObjectPoolGenericMono<SectScriptureBookButton> pool)
        {
            Root = root;
            Title = title;
            TitleKey = titleKey;
            Pool = pool;
        }

        internal void SetCount(int count)
        {
            Title.text = $"{TitleKey.Localize()} ({count})";
        }

        internal void Clear()
        {
            Root.SetActive(false);
            Pool.clear();
        }
    }

    private class SectScriptureBookButton : MonoBehaviour
    {
        private Image _cover;
        private Image _icon;
        private TipButton _tipButton;

        internal void Initialize(Image cover, Image icon)
        {
            _cover = cover;
            _icon = icon;
            _tipButton = GetComponent<TipButton>() ?? gameObject.AddComponent<TipButton>();
            SetupTipButton();
        }

        internal void Setup(ScriptureBookEntry entry)
        {
            if (_cover == null)
            {
                _cover = GetComponent<Image>();
            }

            if (_icon == null)
            {
                _icon = transform.Find("Icon")?.GetComponent<Image>();
            }

            if (_cover == null || _icon == null)
            {
                CultureBookButton vanillaButton = GetComponent<CultureBookButton>();
                if (vanillaButton != null)
                {
                    _cover = vanillaButton.cover;
                    _icon = vanillaButton.icon;
                    Object.Destroy(vanillaButton);
                }
            }

            if (_cover == null || _icon == null)
            {
                gameObject.SetActive(false);
                return;
            }

            _cover.sprite = SpriteTextureLoader.getSprite(entry.CoverPath);
            _icon.sprite = SpriteTextureLoader.getSprite(entry.IconPath);
            gameObject.name = entry.Id;

            _tipButton ??= GetComponent<TipButton>() ?? gameObject.AddComponent<TipButton>();
            SetupTipButton();
            _tipButton.textOnClick = entry.Title;
            _tipButton.textOnClickDescription = entry.Description;
            _tipButton.text_description_2 = string.Empty;
        }

        private void SetupTipButton()
        {
            _tipButton.type = "tip";
            _tipButton.hoverAction = null;
            _tipButton.setHoverAction(new TooltipAction(_tipButton.showTooltipDefault), true);
        }
    }
}
