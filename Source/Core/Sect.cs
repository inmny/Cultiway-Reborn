using System.Collections.Generic;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
using Cultiway.Content.Libraries;
using Cultiway.Core.Libraries;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Core;

public class Sect : MetaObject<SectData>
{
    public override MetaType meta_type => MetaTypeExtend.Sect.Back();

    private Dictionary<System.Type, Dictionary<IDeleteWhenUnknown, float>> _masterItems = new();

    public override void setDefaultValues()
    {
        base.setDefaultValues();
        _masterItems = new Dictionary<System.Type, Dictionary<IDeleteWhenUnknown, float>>();
    }

    public void Setup(Actor founder)
    {
        generateNewMetaObject();
        data.FounderActorName = founder.getName();
        data.FounderActorID = founder.data.id;
        data.FoundedTime = (float)World.world.getCurWorldTime();
        data.name = founder.generateName(meta_type, getID());

        if (founder.hasCity())
        {
            data.HomeCityName = founder.city.name;
            data.HomeCityID = founder.city.data.id;
        }

        var doctrineCultibook = founder.GetExtend().GetMainCultibook();
        if (doctrineCultibook != null)
        {
            SetDoctrineCultibook(doctrineCultibook);
        }

        JoinSect(founder, SectRank.Leader);
    }

    public void Master<T>(T item, float value) where T : Asset, IDeleteWhenUnknown
    {
        if (item == null) return;
        if (!_masterItems.TryGetValue(typeof(T), out var dict))
        {
            dict = new Dictionary<IDeleteWhenUnknown, float>();
            _masterItems.Add(typeof(T), dict);
        }

        if (!dict.ContainsKey(item))
        {
            item.Current++;
        }

        dict[item] = value;
    }

    public void DeMaster<T>(T item) where T : Asset, IDeleteWhenUnknown
    {
        if (item == null) return;
        if (!_masterItems.TryGetValue(typeof(T), out var dict)) return;

        if (dict.ContainsKey(item))
        {
            item.Current--;
            dict.Remove(item);
        }
    }

    public bool HasMaster<T>() where T : Asset, IDeleteWhenUnknown
    {
        return _masterItems.TryGetValue(typeof(T), out var dict) && dict.Count > 0;
    }

    public float GetMaster<T>(T item) where T : Asset, IDeleteWhenUnknown
    {
        return _masterItems.TryGetValue(typeof(T), out var dict) ? (dict.TryGetValue(item, out var value) ? value : 0) : 0;
    }

    public IEnumerable<(T, float)> GetAllMaster<T>() where T : Asset, IDeleteWhenUnknown
    {
        return _masterItems.TryGetValue(typeof(T), out var dict) ? dict.Select(x => ((T)x.Key, x.Value)) : System.Array.Empty<(T, float)>();
    }

    public void SetDoctrineCultibook(CultibookAsset cultibook)
    {
        if (cultibook == null) return;

        var oldCultibook = GetDoctrineCultibook();
        if (oldCultibook != null && oldCultibook != cultibook)
        {
            DeMaster(oldCultibook);
        }

        data.DoctrineCultibookId = cultibook.id;
        data.DoctrineCultibookName = cultibook.Name;
        Master(cultibook, 100);
        data.CultibookCount = Mathf.Max(data.CultibookCount, CountMastered<CultibookAsset>());
    }

    public CultibookAsset GetDoctrineCultibook()
    {
        if (string.IsNullOrEmpty(data.DoctrineCultibookId)) return null;

        var cultibook = Cultiway.Content.Libraries.Manager.CultibookLibrary.get(data.DoctrineCultibookId);
        if (cultibook == null) return null;

        if (GetMaster(cultibook) <= 0)
        {
            Master(cultibook, 100);
        }

        return cultibook;
    }

    public bool JoinSect(Actor actor, SectRank rank = SectRank.OuterDisciple)
    {
        if (actor == null || actor.isRekt()) return false;

        var ae = actor.GetExtend();
        if (ae.sect == this)
        {
            if (rank == SectRank.Leader)
            {
                SetLeader(actor);
            }
            else if (rank > actor.GetSectRank())
            {
                SetMemberRank(actor, rank);
            }

            return true;
        }

        if (ae.sect != null)
        {
            ae.sect.LeaveSect(actor);
        }

        ae.SetSect(this);
        SetMemberRank(actor, rank);

        if (rank == SectRank.Leader)
        {
            SetLeader(actor);
        }

        return true;
    }

    public bool LeaveSect(Actor actor)
    {
        if (actor == null) return false;

        var ae = actor.GetExtend();
        if (ae.sect != this) return false;

        bool wasLeader = data.LeaderActorID == actor.data.id;
        ae.SetSect(null);
        actor.ClearSectRank();

        if (wasLeader)
        {
            data.LeaderActorID = -1;
            data.LeaderActorName = null;
            TrySuccession();
        }

        return true;
    }

    public bool PromoteMember(Actor actor, SectRank rank)
    {
        if (actor == null || actor.isRekt()) return false;
        if (actor.GetExtend().sect != this) return false;

        if (rank == SectRank.Leader)
        {
            SetLeader(actor);
            return true;
        }

        if (rank <= actor.GetSectRank()) return true;

        SetMemberRank(actor, rank);
        return true;
    }

    public bool TrySuccession()
    {
        Actor currentLeader = GetLeaderActor();
        if (currentLeader != null && currentLeader.GetExtend().sect == this)
        {
            return true;
        }

        Actor nextLeader = FindSuccessionCandidate();
        if (nextLeader == null)
        {
            data.LeaderActorID = -1;
            data.LeaderActorName = null;
            return false;
        }

        SetLeader(nextLeader);
        return true;
    }

    public Actor GetLeaderActor()
    {
        if (data.LeaderActorID <= 0) return null;
        Actor actor = World.world.units.get(data.LeaderActorID);
        if (actor == null || actor.isRekt()) return null;
        return actor;
    }

    public List<Actor> GetLivingMembers()
    {
        var result = new List<Actor>();
        List<Actor> actors = World.world.units.units_only_alive;
        for (int i = 0; i < actors.Count; i++)
        {
            Actor actor = actors[i];
            if (actor.GetExtend().sect == this)
            {
                result.Add(actor);
            }
        }

        return result;
    }

    private void SetLeader(Actor actor)
    {
        if (actor == null || actor.isRekt()) return;

        Actor oldLeader = GetLeaderActor();
        if (oldLeader != null && oldLeader != actor && oldLeader.GetExtend().sect == this)
        {
            SetMemberRank(oldLeader, SectRank.Elder);
        }

        data.LeaderActorID = actor.data.id;
        data.LeaderActorName = actor.getName();
        SetMemberRank(actor, SectRank.Leader);
    }

    private static void SetMemberRank(Actor actor, SectRank rank)
    {
        actor.SetSectRank(rank);
    }

    private Actor FindSuccessionCandidate()
    {
        List<Actor> members = GetLivingMembers();
        if (members.Count == 0) return null;

        members.Sort(CompareSuccessionCandidate);
        return members[0];
    }

    private static int CompareSuccessionCandidate(Actor left, Actor right)
    {
        int rankCompare = right.GetSectRank().CompareTo(left.GetSectRank());
        if (rankCompare != 0) return rankCompare;

        int levelCompare = GetCultivationLevel(right).CompareTo(GetCultivationLevel(left));
        if (levelCompare != 0) return levelCompare;

        int masteryCompare = GetMainCultibookMastery(right).CompareTo(GetMainCultibookMastery(left));
        if (masteryCompare != 0) return masteryCompare;

        return left.data.id.CompareTo(right.data.id);
    }

    private static int GetCultivationLevel(Actor actor)
    {
        var ae = actor.GetExtend();
        return ae.HasCultisys<Xian>() ? ae.GetCultisys<Xian>().CurrLevel : -1;
    }

    private static float GetMainCultibookMastery(Actor actor)
    {
        return actor.GetExtend().GetMainCultibookMastery();
    }

    public override void Dispose()
    {
        ReleaseMasterItems();
        base.Dispose();
    }

    private int CountMastered<T>() where T : Asset, IDeleteWhenUnknown
    {
        return _masterItems.TryGetValue(typeof(T), out var dict) ? dict.Count : 0;
    }

    private void ReleaseMasterItems()
    {
        if (_masterItems == null) return;

        foreach (var items in _masterItems.Values)
        {
            if (items == null) continue;

            foreach (var item in items.Keys)
            {
                item.Current--;
            }
        }

        _masterItems = null;
    }

    public override void generateBanner()
    {
        data.BannerBackgroundIndex = ModClass.L.SectBannerLibrary.getNewIndexBackground();
        data.BannerIconIndex = ModClass.L.SectBannerLibrary.getNewIndexIcon();
    }

    public Sprite getBannerBackground()
    {
        return ModClass.L.SectBannerLibrary.getSpriteBackground(data.BannerBackgroundIndex);
    }

    public Sprite getBannerIcon()
    {
        return ModClass.L.SectBannerLibrary.getSpriteIcon(data.BannerIconIndex);
    }

    public override ColorLibrary getColorLibrary()
    {
        // TODO: 添加颜色库
        return AssetManager.families_colors_library;
    }
}    
