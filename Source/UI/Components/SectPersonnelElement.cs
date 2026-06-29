using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
using Cultiway.Core;
using Cultiway.Utils.Extension;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI.Components;

internal class SectPersonnelElement : WindowMetaElement<Sect, SectData>
{
    private const float ShowStepTime = 0.025f;

    private static readonly SectRank[] RankOrder =
    {
        SectRank.Leader,
        SectRank.Successor,
        SectRank.Elder,
        SectRank.DirectDisciple,
        SectRank.InnerDisciple,
        SectRank.OuterDisciple
    };

    private readonly List<RankSection> _sections = new();
    private UiUnitAvatarElement _avatarPrefab;
    private bool _initialized;

    internal void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        _avatarPrefab = Resources.Load<UiUnitAvatarElement>("ui/UnitAvatarElement")
                        ?? throw new System.InvalidOperationException("SectPersonnelElement 找不到 ui/UnitAvatarElement 预制体");

        SetupLayout();
        for (int i = 0; i < RankOrder.Length; i++)
        {
            _sections.Add(CreateRankSection(RankOrder[i]));
        }
    }

    public override IEnumerator showContent()
    {
        Initialize();

        Sect sect = meta_object;
        if (sect == null || sect.isRekt()) yield break;

        List<Actor> members = sect.GetLivingMembers();
        members.Sort(CompareMembers);

        for (int i = 0; i < _sections.Count; i++)
        {
            RankSection section = _sections[i];
            List<Actor> rankMembers = members.Where(actor => actor.GetSectRank() == section.Rank).ToList();
            section.SetCount(rankMembers.Count);
            section.Root.SetActive(rankMembers.Count > 0);

            foreach (Actor actor in rankMembers)
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

    private RankSection CreateRankSection(SectRank rank)
    {
        GameObject root = new($"sect_personnel_{rank}", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement));
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
        rootLayoutElement.preferredWidth = 192f;

        Text title = CreateSectionTitle(root.transform, rank);
        Transform grid = CreateAvatarGrid(root.transform);
        ObjectPoolGenericMono<UiUnitAvatarElement> pool = new(_avatarPrefab, grid);

        return new RankSection(rank, root, title, pool);
    }

    private static Text CreateSectionTitle(Transform parent, SectRank rank)
    {
        GameObject titleObject = new($"title_{rank}", typeof(RectTransform), typeof(Text), typeof(Shadow), typeof(LayoutElement));
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
        layout.preferredWidth = 192f;
        layout.preferredHeight = 12f;

        title.text = GetRankName(rank);
        return title;
    }

    private static Transform CreateAvatarGrid(Transform parent)
    {
        GameObject gridObject = new("Grid", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement));
        gridObject.transform.SetParent(parent, false);
        gridObject.transform.localScale = Vector3.one;

        GridLayoutGroup grid = gridObject.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(24f, 24f);
        grid.spacing = new Vector2(2f, 2f);
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 7;
        grid.childAlignment = TextAnchor.UpperCenter;

        ContentSizeFitter fitter = gridObject.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        LayoutElement layout = gridObject.GetComponent<LayoutElement>();
        layout.preferredWidth = 192f;

        return gridObject.transform;
    }

    private static int CompareMembers(Actor left, Actor right)
    {
        int rankCompare = right.GetSectRank().CompareTo(left.GetSectRank());
        if (rankCompare != 0) return rankCompare;

        int levelCompare = GetCultivationLevel(right).CompareTo(GetCultivationLevel(left));
        if (levelCompare != 0) return levelCompare;

        int masteryCompare = right.GetExtend().GetMainCultibookMastery().CompareTo(left.GetExtend().GetMainCultibookMastery());
        if (masteryCompare != 0) return masteryCompare;

        return left.data.id.CompareTo(right.data.id);
    }

    private static int GetCultivationLevel(Actor actor)
    {
        ActorExtend actorExtend = actor.GetExtend();
        return actorExtend.HasCultisys<Xian>() ? actorExtend.GetCultisys<Xian>().CurrLevel : -1;
    }

    private static string GetRankName(SectRank rank)
    {
        return rank switch
        {
            SectRank.Leader => "Cultiway.Sect.Rank.Leader".Localize(),
            SectRank.Successor => "Cultiway.Sect.Rank.Successor".Localize(),
            SectRank.Elder => "Cultiway.Sect.Rank.Elder".Localize(),
            SectRank.DirectDisciple => "Cultiway.Sect.Rank.DirectDisciple".Localize(),
            SectRank.InnerDisciple => "Cultiway.Sect.Rank.InnerDisciple".Localize(),
            SectRank.OuterDisciple => "Cultiway.Sect.Rank.OuterDisciple".Localize(),
            _ => "Cultiway.Sect.Rank.None".Localize()
        };
    }

    private static Font GetCurrentFont()
    {
        return WorldboxGame.I?.CurrentFont ?? LocalizedTextManager.current_font ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    private sealed class RankSection
    {
        internal readonly SectRank Rank;
        internal readonly GameObject Root;
        internal readonly Text Title;
        internal readonly ObjectPoolGenericMono<UiUnitAvatarElement> Pool;

        internal RankSection(SectRank rank, GameObject root, Text title, ObjectPoolGenericMono<UiUnitAvatarElement> pool)
        {
            Rank = rank;
            Root = root;
            Title = title;
            Pool = pool;
        }

        internal void SetCount(int count)
        {
            Title.text = $"{GetRankName(Rank)} ({count})";
        }

        internal void Clear()
        {
            Root.SetActive(false);
            Pool.clear();
        }
    }
}
