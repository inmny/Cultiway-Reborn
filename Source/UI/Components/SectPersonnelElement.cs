using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
using Cultiway.Core;
using Cultiway.Core.Libraries;
using Cultiway.Utils.Extension;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Cultiway.UI.Components;

internal class SectPersonnelElement : WindowMetaElement<Sect, SectData>
{
    private const float ShowStepTime = 0.025f;
    private const float TabTitleWidth = 214f;
    private const string PersonnelTitleKey = "Cultiway.Sect.Personnel";
    private const string PersonnelIconPath = "ui/icons/iconInterestingPeople";

    private readonly List<RoleSection> _sections = new();
    private UiUnitAvatarElement _avatarPrefab;
    private bool _initialized;

    internal void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        SetupRootLayout();
        SetupTitle();
        SetupAvatarPrefab();
        SetupRankSections();
    }

    public override IEnumerator showContent()
    {
        Initialize();

        Sect sect = meta_object;
        if (sect == null || sect.isRekt()) yield break;

        List<Actor> members = sect.GetLivingMembers();
        members.Sort((left, right) => CompareMembers(sect, left, right));

        for (int i = 0; i < _sections.Count; i++)
        {
            RoleSection section = _sections[i];
            List<Actor> roleMembers = members.Where(actor => IsInSection(actor, section.Role)).ToList();
            section.Root.SetActive(roleMembers.Count > 0);

            foreach (Actor actor in roleMembers)
            {
                if (actor.isRekt()) continue;

                track_objects.Add(actor);
                UiUnitAvatarElement avatar = section.Pool.getNext();
                avatar.show(actor);
                yield return new WaitForSecondsRealtime(ShowStepTime);
            }
        }
    }

    public override void clear()
    {
        for (int i = 0; i < _sections.Count; i++)
        {
            _sections[i].Clear();
        }

        base.clear();
    }

    private void SetupRootLayout()
    {
        VerticalLayoutGroup layout = GetComponent<VerticalLayoutGroup>() ?? gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childControlHeight = true;
        layout.childControlWidth = false;
        layout.childForceExpandHeight = true;
        layout.childForceExpandWidth = false;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.spacing = 6f;
    }

    private void SetupTitle()
    {
        Transform title = transform.Find("tab_title_container_unit") ?? transform.Find("tab_title_container_sect_personnel");
        if (title == null) return;

        title.name = "tab_title_container_sect_personnel";
        title.gameObject.SetActive(true);
        title.Find("title_tab")?.GetComponent<LocalizedText>()?.setKeyAndUpdate(PersonnelTitleKey);

        Sprite icon = SpriteTextureLoader.getSprite(PersonnelIconPath);
        foreach (Image image in title.GetComponentsInChildren<Image>(true))
        {
            if (image.name.ToLowerInvariant().Contains("icon"))
            {
                image.sprite = icon;
            }
        }

        ConfigureTitleWidth(title);
    }

    private static void ConfigureTitleWidth(Transform title)
    {
        RectTransform rect = title.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.sizeDelta = new Vector2(TabTitleWidth, rect.sizeDelta.y);
        }

        LayoutElement layout = title.GetComponent<LayoutElement>() ?? title.gameObject.AddComponent<LayoutElement>();
        layout.minWidth = TabTitleWidth;
        layout.preferredWidth = TabTitleWidth;
        layout.flexibleWidth = 0f;
        layout.layoutPriority = 1;
    }

    private void SetupAvatarPrefab()
    {
        _avatarPrefab = GetComponentInChildren<UiUnitAvatarElement>(true)
                        ?? throw new System.InvalidOperationException("SectPersonnelElement 缺少原版 UiUnitAvatarElement 预制体");
        _avatarPrefab.gameObject.SetActive(false);
    }

    private void SetupRankSections()
    {
        List<Transform> backgroundSections = new();
        foreach (Transform child in transform)
        {
            if (child.name.StartsWith("bg_"))
            {
                backgroundSections.Add(child);
            }
        }

        if (backgroundSections.Count == 0)
        {
            throw new System.InvalidOperationException("SectPersonnelElement 缺少原版家谱 bg_ 分组");
        }

        Transform prefabSection = backgroundSections[0];
        for (int i = 1; i < backgroundSections.Count; i++)
        {
            Object.DestroyImmediate(backgroundSections[i].gameObject);
        }

        _sections.Clear();
        List<SectRoleAsset> roles = ModClass.L.SectRoleLibrary.GetPersonnelDisplayOrder();
        for (int i = 0; i < roles.Count; i++)
        {
            SectRoleAsset role = roles[i];
            Transform section = i == 0 ? prefabSection : Object.Instantiate(prefabSection, prefabSection.parent, false);
            section.name = $"bg_sect_{role.id.Split('.').Last().ToLowerInvariant()}";
            section.localScale = Vector3.one;
            section.gameObject.SetActive(true);
            SetupSectionTitle(section, role);

            Transform grid = FindGrid(section)
                             ?? throw new System.InvalidOperationException($"SectPersonnelElement {section.name} 缺少 Grid");
            _sections.Add(new RoleSection(role, section.gameObject, new ObjectPoolGenericMono<UiUnitAvatarElement>(_avatarPrefab, grid)));
        }
    }

    private static void SetupSectionTitle(Transform section, SectRoleAsset role)
    {
        LocalizedText localizedTitle = section.GetComponentInChildren<LocalizedText>(true);
        if (localizedTitle != null)
        {
            localizedTitle.setKeyAndUpdate(role.nameKey);
            return;
        }

        Text title = section.GetComponentInChildren<Text>(true);
        if (title != null)
        {
            title.text = role.GetName();
        }
    }

    private static Transform FindGrid(Transform section)
    {
        Transform grid = section.Find("Grid");
        if (grid != null) return grid;

        return section.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(child => child.name == "Grid");
    }

    private static int CompareMembers(Sect sect, Actor left, Actor right)
    {
        int roleCompare = GetDisplayOrder(right).CompareTo(GetDisplayOrder(left));
        if (roleCompare != 0) return roleCompare;

        int scoreCompare = sect.GetPersonnelScore(right).Total.CompareTo(sect.GetPersonnelScore(left).Total);
        if (scoreCompare != 0) return scoreCompare;

        return left.data.id.CompareTo(right.data.id);
    }

    private static bool IsInSection(Actor actor, SectRoleAsset role)
    {
        SectRoleAsset display = actor.GetSectDisplayRole();
        return display != null && role != null && display.id == role.id;
    }

    private static int GetDisplayOrder(Actor actor)
    {
        return actor.GetSectDisplayRole()?.order ?? 0;
    }

    private sealed class RoleSection
    {
        internal readonly SectRoleAsset Role;
        internal readonly GameObject Root;
        internal readonly ObjectPoolGenericMono<UiUnitAvatarElement> Pool;

        internal RoleSection(SectRoleAsset role, GameObject root, ObjectPoolGenericMono<UiUnitAvatarElement> pool)
        {
            Role = role;
            Root = root;
            Pool = pool;
        }

        internal void Clear()
        {
            Root.SetActive(false);
            Pool.clear();
        }
    }
}
