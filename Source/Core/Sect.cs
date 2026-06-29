using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content;
using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
using Cultiway.Content.Libraries;
using Cultiway.Core.Libraries;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core;

public class Sect : MetaObject<SectData>
{
    public override MetaType meta_type => MetaTypeExtend.Sect.Back();

    public void Setup(Actor founder)
    {
        generateNewMetaObject();
        data.ScriptureBookIDs = new List<long>();
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
            CreateDoctrineBook(founder, doctrineCultibook, founder.GetExtend().GetMainCultibookMastery());
        }

        JoinSect(founder, SectRank.Leader);
        JoinFounderApprentices(founder.GetExtend());
    }

    private void JoinFounderApprentices(ActorExtend founder)
    {
        foreach (ActorExtend apprentice in founder.GetApprentices())
        {
            if (apprentice.Base == null || apprentice.Base.isRekt()) continue;
            if (apprentice.sect != null) continue;

            JoinSect(apprentice.Base, MasterApprenticeTools.GetSectRankForRelation(apprentice.GetRelationType()));
        }
    }

    public bool AddScriptureBook(Book book)
    {
        if (book == null || book.isRekt()) return false;
        if (HasScriptureBook(book)) return false;

        bool added = false;
        if (!data.ScriptureBookIDs.Contains(book.id))
        {
            data.ScriptureBookIDs.Add(book.id);
            added = true;
        }

        RefreshScriptureStats();
        return added;
    }

    public bool HasScriptureBook(Book book)
    {
        if (book == null || book.isRekt()) return false;

        BookExtend bookExtend = book.GetExtend();
        if (book.getAsset() == BookTypes.Cultibook && bookExtend.HasComponent<Cultibook>())
        {
            return HasScriptureCultibook(bookExtend.GetComponent<Cultibook>().Asset);
        }

        if (book.getAsset() == BookTypes.Elixirbook && bookExtend.HasComponent<Elixirbook>())
        {
            return HasScriptureElixirRecipe(bookExtend.GetComponent<Elixirbook>().Asset);
        }

        if (book.getAsset() == BookTypes.Skillbook && bookExtend.HasComponent<Skillbook>())
        {
            return HasScriptureSkillbook(bookExtend.GetComponent<Skillbook>().SkillContainer);
        }

        return data.ScriptureBookIDs.Contains(book.id);
    }

    public bool HasScriptureCultibook(CultibookAsset cultibook)
    {
        if (cultibook == null) return false;

        for (int i = 0; i < data.ScriptureBookIDs.Count; i++)
        {
            Book book = World.world.books.get(data.ScriptureBookIDs[i]);
            if (book == null || book.isRekt() || book.getAsset() != BookTypes.Cultibook) continue;

            BookExtend bookExtend = book.GetExtend();
            if (!bookExtend.HasComponent<Cultibook>()) continue;
            CultibookAsset existing = bookExtend.GetComponent<Cultibook>().Asset;
            if (existing != null && existing.id == cultibook.id) return true;
        }

        return false;
    }

    public bool HasScriptureElixirRecipe(ElixirAsset elixir)
    {
        if (elixir == null) return false;

        for (int i = 0; i < data.ScriptureBookIDs.Count; i++)
        {
            Book book = World.world.books.get(data.ScriptureBookIDs[i]);
            if (book == null || book.isRekt() || book.getAsset() != BookTypes.Elixirbook) continue;

            BookExtend bookExtend = book.GetExtend();
            if (!bookExtend.HasComponent<Elixirbook>()) continue;
            ElixirAsset existing = bookExtend.GetComponent<Elixirbook>().Asset;
            if (existing != null && existing.id == elixir.id) return true;
        }

        return false;
    }

    public bool HasScriptureSkillbook(Entity skillContainer)
    {
        if (skillContainer.IsNull) return false;

        for (int i = 0; i < data.ScriptureBookIDs.Count; i++)
        {
            Book book = World.world.books.get(data.ScriptureBookIDs[i]);
            if (book == null || book.isRekt() || book.getAsset() != BookTypes.Skillbook) continue;

            BookExtend bookExtend = book.GetExtend();
            if (!bookExtend.HasComponent<Skillbook>()) continue;
            if (SkillContainerUtils.IsSimilar(bookExtend.GetComponent<Skillbook>().SkillContainer, skillContainer)) return true;
        }

        return false;
    }

    public IReadOnlyList<long> GetScriptureBookIds()
    {
        return data.ScriptureBookIDs;
    }

    public List<Book> GetScriptureBooks(BookTypeAsset bookType)
    {
        List<Book> result = new();
        for (int i = 0; i < data.ScriptureBookIDs.Count; i++)
        {
            Book book = World.world.books.get(data.ScriptureBookIDs[i]);
            if (book == null || book.isRekt()) continue;
            if (book.getAsset() == bookType)
            {
                result.Add(book);
            }
        }

        result.Sort(CompareScriptureBooks);
        return result;
    }

    public void SetDoctrineCultibook(CultibookAsset cultibook)
    {
        if (cultibook == null) return;

        data.DoctrineCultibookId = cultibook.id;
        data.DoctrineCultibookName = cultibook.Name;
    }

    public CultibookAsset GetDoctrineCultibook()
    {
        if (string.IsNullOrEmpty(data.DoctrineCultibookId)) return null;

        var cultibook = Cultiway.Content.Libraries.Manager.CultibookLibrary.get(data.DoctrineCultibookId);
        if (cultibook == null) return null;

        return cultibook;
    }

    private void CreateDoctrineBook(Actor founder, CultibookAsset cultibook, float mastery)
    {
        if (founder == null || founder.isRekt() || founder.language == null) return;

        Book book = World.world.books.NewBook(founder, BookTypes.Cultibook);
        if (book == null) return;

        BookExtend bookExtend = book.GetExtend();
        bookExtend.AddComponent(new Cultibook(cultibook.id));
        bookExtend.AddComponent(cultibook.Level);
        bookExtend.Master(cultibook, Mathf.Max(1f, mastery));
        book.data.name = cultibook.Name;
        AddScriptureBook(book);
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
        actor.SetSectJoinTime((float)World.world.getCurWorldTime());
        actor.ClearSectContribution();

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
        actor.ClearSectJoinTime();
        actor.ClearSectContribution();

        if (wasLeader)
        {
            data.LeaderActorID = -1;
            data.LeaderActorName = null;
            TrySuccession();
        }

        return true;
    }

    public bool AddContribution(Actor actor, int contribution)
    {
        if (actor == null || actor.isRekt()) return false;
        if (contribution <= 0) return false;
        if (actor.GetExtend().sect != this) return false;

        actor.AddSectContribution(contribution);
        EvaluateMemberRank(actor);
        return true;
    }

    public SectPersonnelScore GetPersonnelScore(Actor actor)
    {
        return SectPersonnelEvaluator.EvaluateScore(this, actor);
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

    public bool EvaluateMemberRank(Actor actor)
    {
        SectRank targetRank = SectPersonnelEvaluator.EvaluatePromotionTarget(this, actor);
        return targetRank != SectRank.None && PromoteMember(actor, targetRank);
    }

    public void EvaluateAllMemberRanks()
    {
        List<Actor> members = GetLivingMembers();
        members.Sort((left, right) => GetPersonnelScore(right).Total.CompareTo(GetPersonnelScore(left).Total));
        for (int i = 0; i < members.Count; i++)
        {
            EvaluateMemberRank(members[i]);
        }
    }

    public bool TryRecruitExternalMember(Actor recruiter, Actor candidate)
    {
        return SectPersonnelEvaluator.TryRecruitExternalMember(this, recruiter, candidate);
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

    public City GetHomeCity()
    {
        if (data.HomeCityID <= 0) return null;

        City city = World.world.cities.get(data.HomeCityID);
        return city == null || city.isRekt() ? null : city;
    }

    public int GetTerritoryCount()
    {
        City city = GetHomeCity();
        return city?.zones?.Count ?? 0;
    }

    public override ActorAsset getActorAsset()
    {
        if (data != null)
        {
            Actor leader = GetLeaderActor();
            if (!leader.isRekt()) return leader.getActorAsset();

            if (data.FounderActorID > 0)
            {
                Actor founder = World.world.units.get(data.FounderActorID);
                if (!founder.isRekt()) return founder.getActorAsset();
            }

            foreach (Actor member in GetLivingMembers())
            {
                if (!member.isRekt()) return member.getActorAsset();
            }
        }

        return AssetManager.actor_library.get("human");
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

        members.Sort((left, right) => SectPersonnelEvaluator.CompareSuccessionCandidates(this, left, right));
        return members[0];
    }

    public void RefreshScriptureStats()
    {
        int cultibooks = 0;
        int elixirRecipes = 0;
        int skillbooks = 0;
        for (int i = 0; i < data.ScriptureBookIDs.Count; i++)
        {
            Book book = World.world.books.get(data.ScriptureBookIDs[i]);
            if (book == null || book.isRekt()) continue;

            if (book.getAsset() == BookTypes.Cultibook)
            {
                cultibooks++;
            }
            else if (book.getAsset() == BookTypes.Elixirbook)
            {
                elixirRecipes++;
            }
            else if (book.getAsset() == BookTypes.Skillbook)
            {
                skillbooks++;
            }
        }

        data.CultibookCount = cultibooks;
        data.ElixirRecipeCount = elixirRecipes;
        data.SkillbookCount = skillbooks;
    }

    private static int CompareScriptureBooks(Book left, Book right)
    {
        int typeCompare = string.Compare(left.data.book_type, right.data.book_type, System.StringComparison.Ordinal);
        if (typeCompare != 0) return typeCompare;

        int nameCompare = string.Compare(left.data.name, right.data.name, System.StringComparison.CurrentCulture);
        if (nameCompare != 0) return nameCompare;

        return left.id.CompareTo(right.id);
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
