using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content;
using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
using Cultiway.Content.Libraries;
using Cultiway.Core.Components;
using Cultiway.Core.Libraries;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Debug;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using HarmonyLib;
using UnityEngine;

namespace Cultiway.Core;

public class Sect : MetaObjectWithTraits<SectData, SectTrait>, IHasInventory
{
    public readonly List<Building> buildings = new();
    public readonly Dictionary<string, List<Building>> buildings_dict_type = new();
    public readonly Dictionary<string, List<Building>> buildings_dict_id = new();
    public readonly SectJobsContainer jobs = new();

    internal Building under_construction_building;
    private SectTrait _residence_strategy;
    private Entity _treasureEntity;

    public override MetaType meta_type => MetaTypeExtend.Sect.Back();
    public override AssetLibrary<SectTrait> trait_library => ModClass.L.SectTraitLibrary;
    public override List<string> saved_traits => data.saved_traits;

    public override int countUnits()
    {
        return getUnits().CountValidUnits();
    }

    public override int countAdults()
    {
        return getUnits().CountValidAdults();
    }

    public override int countChildren()
    {
        return getUnits().CountValidChildren();
    }

    public override void triggerOnRemoveObject()
    {
        ReleaseMembers();
        SectTreasureRules.ReleaseTreasures(this);
        DisposeTreasureInventory();
        base.triggerOnRemoveObject();
        AbandonBuildings();
        WorldboxGame.I?.Sects?.setDirtyResidenceZones();
    }

    private void AbandonBuildings()
    {
        foreach (Building building in buildings)
        {
            building.data.removeLong(BuildingDataKeys.SectID_Long);
            building.makeAbandoned();
        }

        ClearBuildingList();
        SectVerifyLog.Log("SectBuildingsAbandoned", $"sect={SectVerifyLog.Sect(this)}");
    }

    public void Setup(Actor founder)
    {
        generateNewMetaObject();
        InitializeTreasureInventory();
        data.ScriptureBookIDs = new List<long>();
        data.ResidenceZones = new List<ZoneData>();
        ClearBuildingList();
        data.FounderActorName = founder.getName();
        data.FounderActorID = founder.data.id;
        data.FoundedTime = (float)World.world.getCurWorldTime();
        data.name = founder.generateName(meta_type, getID());
        AddFoundingResidenceStrategy(founder);
        AddFoundingPolicies();

        if (founder.hasCity())
        {
            data.HomeCityName = founder.city.name;
            data.HomeCityID = founder.city.data.id;
        }
        SetupResidence(founder);

        var doctrineCultibook = founder.GetExtend().GetMainCultibook();
        if (doctrineCultibook != null)
        {
            SetDoctrineCultibook(doctrineCultibook);
            CreateDoctrineBook(founder, doctrineCultibook, founder.GetExtend().GetMainCultibookMastery());
        }

        JoinSect(founder, new SectJoinProfile(SectRoles.NoGrade, SectRoles.Leader, SectRoles.NoTitle));
        JoinFounderApprentices(founder.GetExtend());
        SectVerifyLog.Log(
            "BuildSect",
            $"created sect={SectVerifyLog.Sect(this)} founder={SectVerifyLog.Actor(founder)} doctrine={data.DoctrineCultibookId ?? "null"} members={countUnits()} scriptures={data.ScriptureBookIDs.Count}");
        WorldLogUtils.LogSectFounded(this, founder);
    }

    public override void loadData(SectData pData)
    {
        base.loadData(pData);
        InitializeTreasureInventory();
    }

    /// <summary>
    /// 获取承载宗门运行期库藏关系的实体。
    /// </summary>
    public Entity TreasureEntity => _treasureEntity;

    /// <summary>
    /// 将特殊物品移入宗门运行期库存。
    /// </summary>
    public void AddSpecialItem(Entity item)
    {
        InventoryLifecycle.NotifyBeforeItemAdded(this, item);
        item.GetIncomingLinks<InventoryRelation>().Entities.Do(owner =>
        {
            owner.RemoveRelation<InventoryRelation>(item);
        });
        _treasureEntity.AddRelation(new InventoryRelation { item = item });
        InventoryLifecycle.NotifyAfterItemAdded(this, item);
    }

    /// <summary>
    /// 将特殊物品移出宗门运行期库存。
    /// </summary>
    public void ExtractSpecialItem(Entity item)
    {
        InventoryLifecycle.NotifyBeforeItemExtracted(this, item);
        _treasureEntity.RemoveRelation<InventoryRelation>(item);
        InventoryLifecycle.NotifyAfterItemExtracted(this, item);
    }

    /// <summary>
    /// 枚举当前实际存放在藏宝阁中的特殊物品。
    /// </summary>
    public IEnumerable<Entity> GetItems()
    {
        var relations = _treasureEntity.GetRelations<InventoryRelation>();
        for (int i = 0; i < relations.Length; i++)
        {
            yield return relations[i].item;
        }
    }

    /// <summary>
    /// 枚举宗门拥有的全部特殊物品，包含已经外借的法宝。
    /// </summary>
    public IEnumerable<Entity> GetTreasures()
    {
        var relations = _treasureEntity.GetRelations<SectTreasureRelation>();
        for (int i = 0; i < relations.Length; i++)
        {
            yield return relations[i].item;
        }
    }

    /// <summary>
    /// 判断指定特殊物品的所有权是否属于宗门。
    /// </summary>
    public bool OwnsTreasure(Entity item)
    {
        var relations = _treasureEntity.GetRelations<SectTreasureRelation>();
        for (int i = 0; i < relations.Length; i++)
        {
            if (relations[i].item == item) return true;
        }

        return false;
    }

    /// <summary>
    /// 判断指定宗门所有物是否仍存放在藏宝阁内。
    /// </summary>
    public bool IsTreasureStored(Entity item)
    {
        var relations = _treasureEntity.GetRelations<InventoryRelation>();
        for (int i = 0; i < relations.Length; i++)
        {
            if (relations[i].item == item) return true;
        }

        return false;
    }

    /// <summary>
    /// 为特殊物品登记宗门所有权。
    /// </summary>
    public void AddTreasureOwnership(Entity item)
    {
        _treasureEntity.AddRelation(new SectTreasureRelation { item = item });
    }

    /// <summary>
    /// 解除特殊物品的宗门所有权。
    /// </summary>
    public void RemoveTreasureOwnership(Entity item)
    {
        _treasureEntity.RemoveRelation<SectTreasureRelation>(item);
    }

    private void InitializeTreasureInventory()
    {
        if (!_treasureEntity.IsNull)
        {
            _treasureEntity.AddTag<TagRecycle>();
        }

        _treasureEntity = ModClass.I.W.CreateEntity(new SectInventoryBinder(getID()));
    }

    private void DisposeTreasureInventory()
    {
        if (_treasureEntity.IsNull) return;
        _treasureEntity.AddTag<TagRecycle>();
        _treasureEntity = default;
    }

    private void AddFoundingResidenceStrategy(Actor founder)
    {
        if (SectTraits.TryPickFoundingResidenceStrategy(founder, out SectTrait trait))
        {
            addTrait(trait, true);
            SectVerifyLog.Log("SectTrait", $"sect={SectVerifyLog.Sect(this)} trait={trait.id} reason=founding_residence_strategy");
        }
    }

    private void AddFoundingPolicies()
    {
        List<SectTrait> policies = SectTraits.PickFoundingPolicies();
        for (int i = 0; i < policies.Count; i++)
        {
            SectTrait policy = policies[i];
            addTrait(policy, true);
            SectVerifyLog.Log("SectTrait", $"sect={SectVerifyLog.Sect(this)} trait={policy.id} group={policy.group_id} reason=founding_policy");
        }
    }

    private void SetupResidence(Actor founder)
    {
        if (SectResidencePlanner.TryFindFoundingSite(this, founder, out SectResidencePlan plan))
        {
            SectResidencePlanner.ApplyResidencePlan(this, founder, plan);
        }
    }

    private void JoinFounderApprentices(ActorExtend founder)
    {
        foreach (ActorExtend apprentice in founder.GetApprentices())
        {
            if (apprentice.Base == null || apprentice.Base.isRekt()) continue;
            if (apprentice.sect != null) continue;

            JoinSect(apprentice.Base, MasterApprenticeTools.GetSectJoinProfileForRelation(apprentice.GetRelationType()));
            SectVerifyLog.Log("FounderApprenticeJoin", $"sect={SectVerifyLog.Sect(this)} apprentice={SectVerifyLog.Actor(apprentice.Base)} relation={SectVerifyLog.Relation(apprentice.GetRelationType())}");
        }
    }

    public bool AddScriptureBook(Book book)
    {
        if (book == null || book.isRekt()) return false;
        if (HasScriptureBook(book))
        {
            SectVerifyLog.Log("ScriptureAddSkip", $"sect={SectVerifyLog.Sect(this)} book={SectVerifyLog.Book(book)} reason=duplicate");
            return false;
        }

        bool added = false;
        if (!data.ScriptureBookIDs.Contains(book.id))
        {
            data.ScriptureBookIDs.Add(book.id);
            added = true;
        }

        RefreshScriptureStats();
        SectVerifyLog.Log("ScriptureAdd", $"sect={SectVerifyLog.Sect(this)} book={SectVerifyLog.Book(book)} added={added} cultibooks={data.CultibookCount} elixirs={data.ElixirRecipeCount} skills={data.SkillbookCount}");
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
        SectVerifyLog.Log("DoctrineBook", $"sect={SectVerifyLog.Sect(this)} founder={SectVerifyLog.Actor(founder)} book={SectVerifyLog.Book(book)} cultibook={cultibook.id} mastery={mastery:F1}");
    }

    public bool JoinSect(Actor actor)
    {
        return JoinSect(actor, new SectJoinProfile(SectRoles.OuterDisciple, SectRoles.NoOffice, SectRoles.NoTitle));
    }

    public bool JoinSect(Actor actor, SectJoinProfile profile)
    {
        if (actor == null || actor.isRekt()) return false;

        var ae = actor.GetExtend();
        if (ae.sect == this)
        {
            ApplyJoinProfile(actor, profile);
            SectVerifyLog.Log("JoinSectRefresh", $"sect={SectVerifyLog.Sect(this)} actor={SectVerifyLog.Actor(actor)} profile={DescribeJoinProfile(profile)} roles={actor.GetSectRoleSummary()}");
            return true;
        }

        if (ae.sect != null)
        {
            ae.sect.LeaveSect(actor);
        }

        ae.SetSect(this);
        actor.SetDefaultSectRoles();
        actor.SetSectJoinTime((float)World.world.getCurWorldTime());
        actor.ClearSectContribution();
        ApplyJoinProfile(actor, profile);
        SectVerifyLog.Log("JoinSect", $"sect={SectVerifyLog.Sect(this)} actor={SectVerifyLog.Actor(actor)} profile={DescribeJoinProfile(profile)} roles={actor.GetSectRoleSummary()}");
        if (profile.Office != SectRoles.Leader)
        {
            WorldLogUtils.LogSectJoined(this, actor);
        }

        return true;
    }

    public bool LeaveSect(Actor actor)
    {
        if (actor == null) return false;

        var ae = actor.GetExtend();
        if (ae.sect != this) return false;

        bool wasLeader = data.LeaderActorID == actor.data.id;
        DetachMember(actor);
        SectVerifyLog.Log("LeaveSect", $"sect={SectVerifyLog.Sect(this)} actor={SectVerifyLog.Actor(actor)} wasLeader={wasLeader}");

        if (wasLeader)
        {
            data.LeaderActorID = -1;
            data.LeaderActorName = null;
            TrySuccession();
        }

        return true;
    }

    private void ReleaseMembers()
    {
        List<Actor> members = GetLivingMembers();
        for (int i = 0; i < members.Count; i++)
        {
            DetachMember(members[i]);
        }

        SectVerifyLog.Log("SectMembersReleased", $"sect={SectVerifyLog.Sect(this)} count={members.Count}");
    }

    private void DetachMember(Actor actor)
    {
        SectTreasureRules.ReturnBorrowedTreasures(actor, this);
        SectJobRules.ReleaseActorJob(actor);
        actor.GetExtend().SetSect(null);
        actor.ClearSectRoles();
        actor.ClearSectJoinTime();
        actor.ClearSectContribution();
    }

    public bool AddContribution(Actor actor, int contribution)
    {
        if (actor == null || actor.isRekt()) return false;
        if (contribution <= 0) return false;
        if (actor.GetExtend().sect != this) return false;

        actor.AddSectContribution(contribution);
        EvaluateMemberRoles(actor);
        SectVerifyLog.Log("Contribution", $"sect={SectVerifyLog.Sect(this)} actor={SectVerifyLog.Actor(actor)} add={contribution} total={actor.GetSectContribution()}");
        return true;
    }

    public SectPersonnelScore GetPersonnelScore(Actor actor)
    {
        return SectPersonnelEvaluator.EvaluateScore(this, actor);
    }

    public bool PromoteMember(Actor actor, SectRoleAsset role)
    {
        if (actor == null || actor.isRekt()) return false;
        if (actor.GetExtend().sect != this) return false;
        if (role == null) return false;

        if (role == SectRoles.Leader)
        {
            SetLeader(actor);
            return true;
        }

        SectRoleAsset current = actor.GetSectRole(role.slot);
        if (current != null && current.order >= role.order) return true;
        if (!SectPersonnelEvaluator.CanMeetConfiguredRolePrerequisites(actor, role))
        {
            SectVerifyLog.Log("PromoteBlocked", $"sect={SectVerifyLog.Sect(this)} actor={SectVerifyLog.Actor(actor)} role={SectVerifyLog.Role(role)} reason=role_prerequisite");
            return false;
        }

        if (!actor.EnsureSectRoleMasterRequirement(this, role))
        {
            SectVerifyLog.Log("PromoteBlocked", $"sect={SectVerifyLog.Sect(this)} actor={SectVerifyLog.Actor(actor)} role={SectVerifyLog.Role(role)} reason=master_requirement");
            return false;
        }

        actor.SetSectRole(role);
        ClearGradeForSeniorOffice(actor, role);
        SectVerifyLog.Log("Promote", $"sect={SectVerifyLog.Sect(this)} actor={SectVerifyLog.Actor(actor)} from={SectVerifyLog.Role(current)} to={SectVerifyLog.Role(role)} roles={actor.GetSectRoleSummary()}");
        WorldLogUtils.LogSectPromoted(this, actor, role);
        return true;
    }

    public bool TryPromoteMember(Actor manager, Actor actor, SectRoleAsset role)
    {
        bool result = manager.CanPromoteSectMember(this, actor, role)
                      && PromoteMember(actor, role);
        SectVerifyLog.Log("TryPromote", $"sect={SectVerifyLog.Sect(this)} manager={SectVerifyLog.Actor(manager)} actor={SectVerifyLog.Actor(actor)} role={SectVerifyLog.Role(role)} result={result}");
        return result;
    }

    public bool EvaluateMemberRoles(Actor actor)
    {
        SectPersonnelEvaluation evaluation = SectPersonnelEvaluator.EvaluatePromotionTarget(this, actor);
        if (!evaluation.HasTarget) return false;

        SectVerifyLog.Log("EvaluateMember", $"sect={SectVerifyLog.Sect(this)} actor={SectVerifyLog.Actor(actor)} score={GetPersonnelScore(actor).Total} grade={SectVerifyLog.Role(evaluation.Grade)} office={SectVerifyLog.Role(evaluation.Office)} title={SectVerifyLog.Role(evaluation.Title)}");
        if (evaluation.Grade != null) PromoteMember(actor, evaluation.Grade);
        if (evaluation.Office != null) PromoteMember(actor, evaluation.Office);
        if (evaluation.Title != null) PromoteMember(actor, evaluation.Title);
        return true;
    }

    public void EvaluateAllMemberRoles()
    {
        List<Actor> members = GetLivingMembers();
        members.Sort((left, right) => GetPersonnelScore(right).Total.CompareTo(GetPersonnelScore(left).Total));
        for (int i = 0; i < members.Count; i++)
        {
            EvaluateMemberRoles(members[i]);
        }
    }

    public bool EvaluateAllMemberRoles(Actor evaluator)
    {
        if (!evaluator.CanEvaluateSectPersonnel(this)) return false;

        SectVerifyLog.Log("EvaluateAllStart", $"sect={SectVerifyLog.Sect(this)} evaluator={SectVerifyLog.Actor(evaluator)} members={GetLivingMembers().Count}");
        EvaluateAllMemberRoles();
        SectVerifyLog.Log("EvaluateAllDone", $"sect={SectVerifyLog.Sect(this)} evaluator={SectVerifyLog.Actor(evaluator)}");
        return true;
    }

    public bool TryRecruitExternalMember(Actor recruiter, Actor candidate)
    {
        bool result = SectPersonnelEvaluator.TryRecruitExternalMember(this, recruiter, candidate);
        SectVerifyLog.Log("RecruitExternal", $"sect={SectVerifyLog.Sect(this)} recruiter={SectVerifyLog.Actor(recruiter)} candidate={SectVerifyLog.Actor(candidate)} result={result} roles={(result ? candidate.GetSectRoleSummary() : "none")}");
        return result;
    }

    public bool TrySuccession()
    {
        Actor currentLeader = GetLeaderActor();
        if (currentLeader != null && currentLeader.GetExtend().sect == this)
        {
            SectVerifyLog.Log("SuccessionSkip", $"sect={SectVerifyLog.Sect(this)} leader={SectVerifyLog.Actor(currentLeader)} reason=current_alive");
            return true;
        }

        Actor nextLeader = FindSuccessionCandidate();
        if (nextLeader == null)
        {
            data.LeaderActorID = -1;
            data.LeaderActorName = null;
            SectVerifyLog.Log("SuccessionFailed", $"sect={SectVerifyLog.Sect(this)} reason=no_candidate");
            return false;
        }

        SetLeader(nextLeader);
        SectVerifyLog.Log("Succession", $"sect={SectVerifyLog.Sect(this)} leader={SectVerifyLog.Actor(nextLeader)}");
        WorldLogUtils.LogSectSuccession(this, nextLeader);
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

    public WorldTile GetResidenceTile()
    {
        if (data.ResidenceTileID >= 0 && data.ResidenceTileID < World.world.tiles_list.Length)
        {
            WorldTile tile = World.world.tiles_list[data.ResidenceTileID];
            if (tile != null) return tile;
        }

        return GetHomeCity()?.getTile();
    }

    public string GetResidenceName()
    {
        if (!string.IsNullOrEmpty(data.ResidenceName)) return data.ResidenceName;

        City city = GetHomeCity();
        if (city != null) return city.name;

        WorldTile tile = GetResidenceTile();
        return tile == null ? null : $"{tile.x}, {tile.y}";
    }

    public TileZone GetResidenceZone()
    {
        return GetResidenceTile()?.zone;
    }

    public List<TileZone> GetResidenceZones()
    {
        return SectResidencePlanner.GetResidenceZones(this);
    }

    public WorldTile GetRandomResidenceTile(Actor actor)
    {
        List<TileZone> zones = GetResidenceZones();
        for (int i = 0; i < SectConst.ResidenceRandomTileAttempts; i++)
        {
            if (zones.Count == 0) break;

            TileZone zone = zones.GetRandom();
            if (zone == null || zone.tiles.Length == 0) continue;

            WorldTile tile = zone.tiles.GetRandom();
            if (tile != null)
            {
                return tile;
            }
        }

        return GetResidenceTile();
    }

    public int GetTerritoryCount()
    {
        return GetResidenceZones().Count;
    }

    public void ClearBuildingList()
    {
        buildings.Clear();
        foreach (List<Building> value in buildings_dict_type.Values)
        {
            value.Clear();
        }
        foreach (List<Building> value in buildings_dict_id.Values)
        {
            value.Clear();
        }
        buildings_dict_type.Clear();
        buildings_dict_id.Clear();
        under_construction_building = null;
    }

    public void RefreshSectJobs()
    {
        SectJobRules.RefreshJobs(this);
    }

    public void ListBuilding(Building building)
    {
        if (!CanListBuilding(building)) return;

        buildings.Add(building);
        SetBuildingDictType(building);
        SetBuildingDictID(building);
        if (building.isUnderConstruction())
        {
            under_construction_building = building;
        }
    }

    public List<Building> GetBuildings()
    {
        return buildings;
    }

    public int CountBuildings()
    {
        return buildings.Count;
    }

    public int CountBuildingsOfID(string buildingId, bool countOnlyFinished = true)
    {
        if (string.IsNullOrEmpty(buildingId)) return 0;
        List<Building> list = GetBuildingListOfID(buildingId);
        if (list == null) return 0;

        if (!countOnlyFinished) return list.Count;

        int result = 0;
        for (int i = 0; i < list.Count; i++)
        {
            if (CanCountBuilding(list[i]))
            {
                result++;
            }
        }

        return result;
    }

    public int CountBuildingsType(string buildingType, bool countOnlyFinished = true)
    {
        if (string.IsNullOrEmpty(buildingType)) return 0;
        List<Building> list = GetBuildingListOfType(buildingType);
        if (list == null) return 0;

        if (!countOnlyFinished) return list.Count;

        int result = 0;
        for (int i = 0; i < list.Count; i++)
        {
            if (CanCountBuilding(list[i]))
            {
                result++;
            }
        }

        return result;
    }

    public bool HasBuildingType(string buildingType, bool countOnlyFinished = true)
    {
        List<Building> list = GetBuildingListOfType(buildingType);
        if (list == null || list.Count == 0) return false;

        if (!countOnlyFinished) return true;

        for (int i = 0; i < list.Count; i++)
        {
            if (CanCountBuilding(list[i]))
            {
                return true;
            }
        }

        return false;
    }

    public List<Building> GetBuildingListOfID(string buildingId)
    {
        buildings_dict_id.TryGetValue(buildingId, out List<Building> value);
        return value;
    }

    public List<Building> GetBuildingListOfType(string buildingType)
    {
        buildings_dict_type.TryGetValue(buildingType, out List<Building> value);
        return value;
    }

    public Building GetBuildingToBuild()
    {
        if (under_construction_building != null && (!under_construction_building.isAlive() || !under_construction_building.isUnderConstruction()))
        {
            under_construction_building = null;
        }

        return under_construction_building;
    }

    public bool HasBuildingToBuild()
    {
        return GetBuildingToBuild() != null;
    }

    public bool TryBuild(SectBuildOrder order, out Building building)
    {
        return SectBuildRules.TryBuildFromOrder(this, order, out building);
    }

    private static bool CanListBuilding(Building building)
    {
        return building != null
               && !building.isRekt()
               && building.isUsable()
               && building.data != null
               && building.asset != null
               && building.asset.IsSectBuilding();
    }

    private static bool CanCountBuilding(Building building)
    {
        return building != null && building.isUsable() && !building.isUnderConstruction();
    }

    private void SetBuildingDictType(Building building)
    {
        if (!buildings_dict_type.TryGetValue(building.asset.type, out List<Building> value))
        {
            value = new List<Building>();
            buildings_dict_type.Add(building.asset.type, value);
        }

        value.Add(building);
    }

    private void SetBuildingDictID(Building building)
    {
        if (!buildings_dict_id.TryGetValue(building.asset.id, out List<Building> value))
        {
            value = new List<Building>();
            buildings_dict_id.Add(building.asset.id, value);
        }

        value.Add(building);
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

    public override void save()
    {
        SyncSavedTraits();
        base.save();
    }

    public override bool addTrait(SectTrait pTrait, bool pRemoveOpposites = false)
    {
        bool result = base.addTrait(pTrait, pRemoveOpposites);
        if (result)
        {
            OnSectTraitsChanged();
        }

        return result;
    }

    public override bool removeTrait(SectTrait pTrait)
    {
        bool result = base.removeTrait(pTrait);
        if (result)
        {
            OnSectTraitsChanged();
        }

        return result;
    }

    public override void traitModifiedEvent()
    {
        base.traitModifiedEvent();
        OnSectTraitsChanged();
    }

    public override void recalcBaseStats()
    {
        base.recalcBaseStats();
        base_stats[SectStats.TreasureCapacity.id] += SectConst.TreasureBaseCapacity;
        MergeBuildingStats();
        _residence_strategy = null;
        foreach (SectTrait trait in getTraits())
        {
            if (!trait.isResidenceStrategy) continue;
            _residence_strategy = trait;
            break;
        }
    }

    private void MergeBuildingStats()
    {
        for (int i = 0; i < buildings.Count; i++)
        {
            Building building = buildings[i];
            if (!CanCountBuilding(building)) continue;

            List<BaseStatsContainer> stats = building.asset.base_stats.getList();
            for (int j = 0; j < stats.Count; j++)
            {
                BaseStatsContainer stat = stats[j];
                if (SectStats.IsSectStat(stat.id))
                {
                    base_stats[stat.id] += stat.value;
                }
            }
        }
    }

    public SectTrait GetResidenceStrategy()
    {
        return _residence_strategy;
    }

    public bool AllowsCityResidenceZones()
    {
        return _residence_strategy?.allowCityResidenceZones == true;
    }

    public bool PrefersCityProximity()
    {
        return _residence_strategy?.preferCityProximity == true;
    }

    public float GetResidenceWakanScoreWeight()
    {
        return _residence_strategy?.residenceWakanScoreWeight ?? 1f;
    }

    public float GetResidenceTerrainScoreWeight()
    {
        return _residence_strategy?.residenceTerrainScoreWeight ?? 1f;
    }

    public float GetResidenceCityDistanceScoreWeight()
    {
        return _residence_strategy?.residenceCityDistanceScoreWeight ?? 1f;
    }

    public float GetResidenceBuildSpaceScoreWeight()
    {
        return _residence_strategy?.residenceBuildSpaceScoreWeight ?? 1f;
    }

    public float GetResidenceSectDistanceScoreWeight()
    {
        return _residence_strategy?.residenceSectDistanceScoreWeight ?? 1f;
    }

    private void OnSectTraitsChanged()
    {
        SyncSavedTraits();
        WorldboxGame.I?.Sects?.setDirtyResidenceZones();
    }

    private void SyncSavedTraits()
    {
        if (data != null)
        {
            data.saved_traits = getTraitsAsStrings();
        }
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
            oldLeader.SetSectRole(SectRoles.Elder);
        }

        data.LeaderActorID = actor.data.id;
        data.LeaderActorName = actor.getName();
        actor.SetSectRole(SectRoles.Leader);
        actor.SetSectRole(SectRoles.NoGrade);
        if (actor.HasSectRole(SectRoles.Successor))
        {
            actor.SetSectRole(SectRoles.NoTitle);
        }
        SectVerifyLog.Log("SetLeader", $"sect={SectVerifyLog.Sect(this)} old={SectVerifyLog.Actor(oldLeader)} new={SectVerifyLog.Actor(actor)} roles={actor.GetSectRoleSummary()}");
    }

    private void ApplyJoinProfile(Actor actor, SectJoinProfile profile)
    {
        ApplyJoinRole(actor, profile.Grade);
        ApplyJoinRole(actor, profile.Office);
        ApplyJoinRole(actor, profile.Title);
    }

    private void ApplyJoinRole(Actor actor, SectRoleAsset role)
    {
        if (role == null) return;
        if (role == SectRoles.Leader)
        {
            SetLeader(actor);
            return;
        }

        SectRoleAsset current = actor.GetSectRole(role.slot);
        if (current == null || current.defaultForSlot || role.order > current.order)
        {
            if (!SectPersonnelEvaluator.CanMeetConfiguredRolePrerequisites(actor, role))
            {
                SectVerifyLog.Log("JoinRoleBlocked", $"sect={SectVerifyLog.Sect(this)} actor={SectVerifyLog.Actor(actor)} role={SectVerifyLog.Role(role)} reason=role_prerequisite");
                return;
            }

            if (!actor.EnsureSectRoleMasterRequirement(this, role))
            {
                SectVerifyLog.Log("JoinRoleBlocked", $"sect={SectVerifyLog.Sect(this)} actor={SectVerifyLog.Actor(actor)} role={SectVerifyLog.Role(role)} reason=master_requirement");
                return;
            }

            actor.SetSectRole(role);
            ClearGradeForSeniorOffice(actor, role);
            SectVerifyLog.Log("JoinRole", $"sect={SectVerifyLog.Sect(this)} actor={SectVerifyLog.Actor(actor)} from={SectVerifyLog.Role(current)} to={SectVerifyLog.Role(role)} roles={actor.GetSectRoleSummary()}");
        }
    }

    private static void ClearGradeForSeniorOffice(Actor actor, SectRoleAsset role)
    {
        if (role.clearsGrade)
        {
            actor.SetSectRole(SectRoles.NoGrade);
            SectVerifyLog.Log("ClearGrade", $"actor={SectVerifyLog.Actor(actor)} role={SectVerifyLog.Role(role)}");
        }
    }

    private static string DescribeJoinProfile(SectJoinProfile profile)
    {
        return $"grade={SectVerifyLog.Role(profile.Grade)},office={SectVerifyLog.Role(profile.Office)},title={SectVerifyLog.Role(profile.Title)}";
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
