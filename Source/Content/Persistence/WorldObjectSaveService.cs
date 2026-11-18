using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Content.Extensions;
using Cultiway.Content.KnightCombat;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.Persistence;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Blueprints;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Content.CreatureCompositions.ActiveAbilities;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ContentLibraries = Cultiway.Content.Libraries.Manager;

namespace Cultiway.Content.Persistence;

/// <summary>保存明确登记的长期对象，分开恢复实体、资料和连接，绝不保存内存编号。</summary>
internal static partial class WorldObjectSaveService
{
    /// <summary>组件与关系各自拥有版本。</summary>
    private static readonly SaveSectionRegistry<Entity> Parts = new();
    /// <summary>动态定义也沿用版本转换流程。</summary>
    private static readonly SaveSectionRegistry<MapBox> Definitions = new();
    /// <summary>当前采集的对象编号。</summary>
    private static readonly Dictionary<Entity, string> References = new();
    /// <summary>克隆物件时组件也会被复制，采集时为新物件重新分配重复编号。</summary>
    private static readonly HashSet<string> UsedIds = new(StringComparer.Ordinal);
    /// <summary>引用发现后等待采集的对象。</summary>
    private static readonly Queue<Entity> Pending = new();
    /// <summary>本次读取已创建的对象。</summary>
    private static readonly Dictionary<string, Entity> Loaded = new(StringComparer.Ordinal);
    /// <summary>本次读取尚待连接的人物、城市与宗门。</summary>
    private static readonly Dictionary<string, WorldObjectRecord> Records = new(StringComparer.Ordinal);
    /// <summary>同一次采集或读取共享的引用转换。</summary>
    private static readonly WorldObjectJson Json = new(Reference, Resolve);
    /// <summary>原版限制生命前需要的人物状态。</summary>
    private static readonly HashSet<string> EarlyParts = new()
    {
        "cultibook.practice", "link.status", "yuanshen.soul-carrier", "yuanshen.runtime", "yuanshen.bodyless",
        "yuanying.soul", "yuanying.seed"
    };
    /// <summary>等待原版对象全部就绪后连接的资料。</summary>
    private static WorldObjectSaveData pendingLoad;
    /// <summary>当前读取报告。</summary>
    private static SaveLoadReport Report => ModClass.I.WorldSaves.LastReport;
    /// <summary>发布版本只记作来源，不参与玩法版本判断。</summary>
    private static string ModVersion => ModClass.I.GetDeclaration().Version;

    /// <summary>登记全部组件及恢复顺序。</summary>
    internal static void Initialize()
    {
        RegisterParts();
        Definitions.Register(new SaveSection<MapBox, DynamicDefinitionSaveData>("dynamic", "功法丹方与血脉定义", 1,
            _ => CaptureDefinitions(), (_, data) => RestoreDefinitions(data)));
        ModClass.I.WorldSaves.ActorRestored += RestoreActor;
        ModClass.I.WorldSaves.WorldRestored += FinishLoad;
        ModClass.I.WorldSaves.WorldCleared += Clear;
    }

    /// <summary>从真实归属与长期对象开始采集，引用到的技能只保存一次。</summary>
    /// <param name="world">当前主世界。</param>
    internal static WorldObjectSaveData Capture(MapBox world)
    {
        References.Clear();
        UsedIds.Clear();
        Pending.Clear();
        var data = new WorldObjectSaveData();
        foreach (Entity entity in ModClass.I.W.Entities.ToEntityList())
            if (IsRoot(entity)) Reference(entity);
        data.MagicWeb = MagicWebManager.Instance.CaptureWorldSave(Reference);
        data.Definitions = Definitions.Capture(world, ModVersion);
        var exporter = new SkillBlueprintExporter();
        while (Pending.Count > 0)
        {
            Entity entity = Pending.Dequeue();
            var record = new WorldObjectRecord { Id = References[entity] };
            if (entity.HasComponent<SourceGrantedSkill>())
            {
                var skill = entity.GetComponent<SkillContainer>();
                record.GrantedSkillAsset = skill.SkillEntityAssetID;
                record.GrantedSkillAnimation = skill.AnimationIndex;
                record.OrganGrantedSkill = CreatureOrganSkillRegistry.TryGetContainer(skill.SkillEntityAssetID,
                    out Entity organSkill) && organSkill == entity;
            }
            else if (entity.HasComponent<SkillContainer>())
            {
                SkillBlueprintExportResult exported = exporter.Export(entity,
                    new SkillBlueprintExportOptions { PreserveContainerNameAsCustom = true });
                if (!exported.Success) throw new InvalidOperationException("无法保存法术：" +
                    string.Join("、", exported.Validation.Issues.Select(issue => issue.Message)));
                // 编辑器导出会生成新的草稿身份；世界快照使用物件身份并排除编辑时间。
                exported.Blueprint.Id = References[entity];
                exported.Blueprint.CreatedAtUtcTicks = 0;
                exported.Blueprint.UpdatedAtUtcTicks = 0;
                record.Skill = exported.Blueprint;
            }
            if (entity.TryGetComponent(out ActorBinder actor))
            {
                record.Knowledge = CaptureKnowledge(actor.AE);
                record.LearnedSkills = actor.AE.GetLearnedSkillsInOrder().Select(Reference).ToList();
            }
            else if (entity.TryGetComponent(out BookBinder book)) record.Knowledge = CaptureKnowledge(book.BE);
            record.Parts = record.GrantedSkillAsset == null ? Parts.Capture(entity, ModVersion) : new JObject
            {
                ["formatVersion"] = SaveSectionRegistry<Entity>.FormatVersion, ["sections"] = new JObject()
            }.ToString(Formatting.None);
            data.Objects.Add(record);
        }
        References.Clear();
        return data;
    }

    /// <summary>先检查编号，再编译技能和建立独立对象，原版人物尚未开始读取。</summary>
    /// <param name="world">当前主世界。</param>
    /// <param name="data">待保存或恢复的资料。</param>
    internal static void Prepare(MapBox world, WorldObjectSaveData data)
    {
        if (data.Objects == null || data.Objects.Any(record => record == null || string.IsNullOrEmpty(record.Id)) ||
            data.Objects.Select(record => record.Id).Distinct(StringComparer.Ordinal).Count() != data.Objects.Count)
            throw new InvalidDataException("世界物件编号为空或重复");
        foreach (WorldObjectRecord record in data.Objects)
            if (record.LoadWarning != null)
            {
                Report.Skip(record.Id, "组合配方", record.LoadWarning);
                record.LoadWarning = null;
            }
        pendingLoad = data;
        foreach (WorldObjectRecord record in data.Objects) Records.Add(record.Id, record);
        var compiler = new SkillBlueprintCompiler();
        CreatureOrganSkillRegistry.EnsureBuilt();
        foreach (WorldObjectRecord record in data.Objects)
        {
            if (!record.Id.StartsWith("object:", StringComparison.Ordinal)) continue;
            Attempt(record.Id, () =>
            {
                Entity entity;
                if (record.GrantedSkillAsset != null)
                {
                    entity = default;
                    if (record.OrganGrantedSkill)
                    {
                        CreatureOrganSkillRegistry.TryGetContainer(record.GrantedSkillAsset, out entity);
                    }
                    else
                        foreach (Entity candidate in ModClass.I.W.Query<SkillContainer, SourceGrantedSkill>().Entities)
                        {
                            SkillContainer skill = candidate.GetComponent<SkillContainer>();
                            if (!IsLive(candidate) || skill.SkillEntityAssetID != record.GrantedSkillAsset ||
                                skill.AnimationIndex != record.GrantedSkillAnimation) continue;
                            entity = candidate;
                            break;
                        }
                    if (entity.IsNull) throw new InvalidDataException("来源技能已移除：" + record.GrantedSkillAsset);
                }
                else if (record.Skill != null)
                {
                    if (!KnightTechniqueSkills.TryRestoreSavedContainer(record.Skill, out entity))
                    {
                        SkillBlueprintCompileResult compiled = compiler.Compile(record.Skill, SkillBlueprintCompileMode.Runtime);
                        if (!compiled.Success) throw new InvalidDataException(string.Join("、", compiled.Validation.Issues.Select(issue => issue.Message)));
                        entity = compiled.Container;
                    }
                    // 编译器生成的编辑来源由原记录决定，不把读取本身当作一次编辑。
                    entity.RemoveComponent<SkillBlueprintOrigin>();
                }
                else entity = ModClass.I.W.CreateEntity();
                entity.AddComponent(new WorldSaveIdentity { Id = record.Id.Substring(7) });
                Loaded.Add(record.Id, entity);
            });
        }
        // 功法定义里的法术池需要上面已建立的技能对象。
        Definitions.Restore(world, data.Definitions, Report, "世界定义");
        foreach (WorldObjectRecord record in data.Objects)
        {
            if (!Loaded.TryGetValue(record.Id, out Entity entity) || !HasPart(record, "status")) continue;
            int failures = Report.Skipped.Count;
            Parts.Restore(entity, record.Parts, Report, record.Id, section => !section.Id.StartsWith("link."), false);
            // 人物读取时就会计算持续状态属性，失效状态必须在接回关系前丢弃。
            if (Report.Skipped.Count > failures)
            {
                entity.DeleteEntity();
                Loaded.Remove(record.Id);
            }
        }
    }

    /// <summary>人物属性计算前接回知识、主修功法、技能和持续状态。</summary>
    /// <param name="actor">当前人物。</param>
    private static void RestoreActor(ActorExtend actor)
    {
        string id = "actor:" + actor.Base.data.id;
        if (!Records.TryGetValue(id, out WorldObjectRecord record)) return;
        Loaded[id] = actor.E;
        RestoreKnowledge(actor, record.Knowledge);
        foreach (string skill in record.LearnedSkills ?? new List<string>())
            Attempt(id, () => actor.LearnSkillV3(Resolve(skill)));
        // 状态先恢复数值，接回人物后才参与原版首次属性计算。
        Parts.Restore(actor.E, record.Parts, Report, id, section => EarlyParts.Contains(section.Id), false);
        actor.MarkCultiwayStatsDirty();
    }

    /// <summary>全部原版对象与宗门就绪后，恢复其余资料，再连接关系。</summary>
    private static void FinishLoad()
    {
        if (pendingLoad == null) return;
        foreach (WorldObjectRecord record in pendingLoad.Objects)
            if (!Loaded.ContainsKey(record.Id) && !record.Id.StartsWith("object:", StringComparison.Ordinal))
                Attempt(record.Id, () => Loaded.Add(record.Id, ResolveAnchor(record.Id)));
        foreach (WorldObjectRecord record in pendingLoad.Objects)
        {
            if (!Loaded.TryGetValue(record.Id, out Entity entity) || entity.IsNull) continue;
            int failures = Report.Skipped.Count;
            Parts.Restore(entity, record.Parts, Report, record.Id, section => !section.Id.StartsWith("link."));
            if (Report.Skipped.Count > failures && entity.TryGetComponent(out BookBinder invalidBook))
            {
                // 原版读书行为要求实际内容组件；移除无法恢复内容的书，不留下可点击的空壳。
                World.world.books.DisposeUncommittedBook(invalidBook.Book);
                entity.AddTag<TagRecycle>();
                Loaded.Remove(record.Id);
                continue;
            }
            if (record.Id.StartsWith("object:") && Report.Skipped.Count > failures)
            {
                entity.DeleteEntity();
                Loaded.Remove(record.Id);
                continue;
            }
            if (entity.HasComponent<SpecialItem>()) entity.GetComponent<SpecialItem>().self = entity;
            if (entity.TryGetComponent(out StatusComponent status) && status.SourceActorId == 0 && status.Source is Actor source)
                entity.GetComponent<StatusComponent>().SourceActorId = source.id;
            if (entity.TryGetComponent(out BookBinder book)) RestoreKnowledge(book.BE, record.Knowledge);
        }
        // 缺失定义的物件整件丢弃，关系不会接到半件法宝或失效丹药上。
        foreach (WorldObjectRecord record in pendingLoad.Objects)
        {
            if (!Loaded.TryGetValue(record.Id, out Entity entity) || !record.Id.StartsWith("object:")) continue;
            if (HasPart(record, "item") && (!entity.HasComponent<SpecialItem>() || !entity.HasComponent<ItemShape>()) ||
                HasPart(record, "elixir") && !entity.HasComponent<Elixir>() ||
                HasPart(record, "status") && !entity.HasComponent<StatusComponent>())
            {
                entity.DeleteEntity();
                Loaded.Remove(record.Id);
            }
        }
        foreach (WorldObjectRecord record in pendingLoad.Objects)
        {
            if (!Loaded.TryGetValue(record.Id, out Entity entity) || entity.IsNull) continue;
            Parts.Restore(entity, record.Parts, Report, record.Id, section => section.Id.StartsWith("link."), false);
            if (entity.TryGetComponent(out ActorBinder actor)) actor.AE.MarkCultiwayStatsDirty();
            if (entity.HasComponent<YuanshenNodeState>()) YuanshenNodeLockService.RegisterNode(entity);
        }
        MagicWebManager.Instance.RestoreWorldSave(pendingLoad.MagicWeb, Resolve, Report);
        RestoreRuntimeIndexes();
        RestoreCraftTasks();
        RestoreDependentStats();
        RestoreArtifactPresentationSizes();
        pendingLoad = null;
        Records.Clear();
        Loaded.Clear();
    }

    /// <summary>只从真实长期对象出发；技能执行、贴图、缓存和待回收对象不作为根。</summary>
    /// <param name="entity">当前运行中的对象。</param>
    private static bool IsRoot(Entity entity) => IsLive(entity) &&
        (entity.HasComponent<ActorBinder>() || entity.HasComponent<BookBinder>() || entity.HasComponent<CityBinder>() ||
         entity.HasComponent<SectInventoryBinder>() || entity.HasComponent<SpecialItem>() ||
         entity.HasComponent<YuanshenNodeState>() || entity.HasComponent<YuanshenAnchorState>());

    /// <summary>排除模板和待删除的对象；已投入炼制的材料仍属于未完成的长期进度。</summary>
    /// <param name="entity">当前运行中的对象。</param>
    private static bool IsLive(Entity entity) => !entity.IsNull && !entity.Tags.Has<TagPrefab>() &&
        !entity.Tags.Has<TagRecycle>() && !entity.Tags.Has<TagReadyRecycle>();

    /// <summary>登记稳定编号，后续遇到相同对象只写一份。</summary>
    /// <param name="entity">当前运行中的对象。</param>
    private static string Reference(Entity entity)
    {
        if (!IsLive(entity)) return null;
        if (References.TryGetValue(entity, out string existing)) return existing;
        string id;
        if (entity.TryGetComponent(out ActorBinder actor))
        {
            if (actor.Actor == null || !actor.Actor.isAlive() || actor.Actor.asset.skip_save) return null;
            id = "actor:" + actor.ID;
        }
        else if (entity.TryGetComponent(out BookBinder book)) id = "book:" + book.ID;
        else if (entity.TryGetComponent(out CityBinder city)) id = "city:" + city.ID;
        else if (entity.TryGetComponent(out SectInventoryBinder sect)) id = "sect:" + sect.ID;
        else
        {
            if (!entity.HasComponent<SpecialItem>() && !entity.HasComponent<SkillContainer>() &&
                !entity.HasComponent<StatusComponent>() && !entity.HasComponent<YuanshenNodeState>() &&
                !entity.HasComponent<YuanshenAnchorState>()) return null;
            if (!entity.HasComponent<WorldSaveIdentity>() || !UsedIds.Add(entity.GetComponent<WorldSaveIdentity>().Id))
            {
                entity.AddComponent(new WorldSaveIdentity { Id = Guid.NewGuid().ToString("N") });
                UsedIds.Add(entity.GetComponent<WorldSaveIdentity>().Id);
            }
            id = "object:" + entity.GetComponent<WorldSaveIdentity>().Id;
        }
        References.Add(entity, id);
        Pending.Enqueue(entity);
        return id;
    }

    /// <summary>只有明确不存在的外部引用才报告资料失效。</summary>
    /// <param name="id">长期保持稳定的编号。</param>
    private static Entity Resolve(string id)
    {
        if (id == null) return default;
        if (Loaded.TryGetValue(id, out Entity entity) && !entity.IsNull) return entity;
        throw new InvalidDataException($"引用对象未能恢复：{id}");
    }

    /// <summary>按原版稳定编号取得人物、城市、书籍或宗门库存。</summary>
    /// <param name="id">长期保持稳定的编号。</param>
    private static Entity ResolveAnchor(string id)
    {
        int split = id.IndexOf(':');
        if (split < 0 || !long.TryParse(id.Substring(split + 1), out long number)) throw new InvalidDataException("对象编号无效");
        Entity entity = id.Substring(0, split) switch
        {
            "actor" => World.world.units.get(number)?.GetExtend().E ?? default,
            "book" => World.world.books.get(number)?.GetExtend().E ?? default,
            "city" => World.world.cities.get(number)?.GetExtend().E ?? default,
            "sect" => WorldboxGame.I.Sects.get(number)?.Treasures.SaveEntity ?? default,
            _ => default
        };
        if (entity.IsNull) throw new InvalidDataException($"原版对象或宗门已不存在：{id}");
        return entity;
    }

    /// <summary>判断版本化记录是否声明了某项必要资料。</summary>
    /// <param name="record">当前对象的存档记录。</param>
    /// <param name="id">长期保持稳定的编号。</param>
    private static bool HasPart(WorldObjectRecord record, string id)
        => JObject.Parse(record.Parts)?["sections"]?[id] != null;

    /// <summary>资料边界独立处理一个条目，代码错误仍正常暴露。</summary>
    /// <param name="owner">资料所属对象的编号。</param>
    /// <param name="restore">只恢复当前条目的方法。</param>
    private static void Attempt(string owner, Action restore)
    {
        try { restore(); }
        catch (Exception error) when (error is JsonException or InvalidDataException or FormatException or OverflowException)
        { Report.Skip(owner, "世界物件", error.Message); }
    }

    /// <summary>换世界时释放待恢复的资料。</summary>
    private static void Clear()
    {
        pendingLoad = null;
        References.Clear();
        UsedIds.Clear();
        Pending.Clear();
        Loaded.Clear();
        Records.Clear();
    }

    /// <summary>普通组件只保存明确登记的公共稳定字段。</summary>
    /// <param name="id">长期保持稳定的编号。</param>
    /// <param name="validate">在改动对象之前检查资料的方法。</param>
    /// <param name="normalize">采集时整理稳定状态的方法。</param>
    private static void Component<T>(string id, Action<T> validate = null, Func<T, T> normalize = null) where T : struct, IComponent
        => Parts.Register(new SaveSection<Entity, ComponentSaveValue<T>>(id, id, 1,
            entity => entity.TryGetComponent(out T value) ? new ComponentSaveValue<T> { Value = normalize == null ? value : normalize(value) } : null,
            (entity, data) => entity.AddComponent(data.Value), data => validate?.Invoke(data.Value), settings: Json.Settings));

    /// <summary>每类关系拥有独立版本，目标编号通过同一转换器处理。</summary>
    /// <param name="id">长期保持稳定的编号。</param>
    private static void Link<T>(string id) where T : struct, ILinkRelation
        => Parts.Register(new SaveSection<Entity, RelationValues>("link." + id, id + "关系", 1,
            entity =>
            {
                var relations = entity.GetRelations<T>();
                if (relations.Length == 0 || typeof(T) == typeof(SkillMasterRelation) && entity.HasComponent<ActorBinder>()) return null;
                var data = new RelationValues();
                JsonSerializer serializer = JsonSerializer.Create(Json.Settings);
                for (int i = 0; i < relations.Length; i++) data.Values.Add(JObject.FromObject(relations[i], serializer));
                return data;
            },
            (entity, data) =>
            {
                foreach (JObject value in data.Values)
                    Attempt(id, () =>
                    {
                        T relation = value.ToObject<T>(JsonSerializer.Create(Json.Settings));
                        if (!relation.GetRelationKey().IsNull) entity.AddRelation(relation);
                    });
            }, settings: Json.Settings));

    /// <summary>只保存具有长期玩法含义的标记。</summary>
    /// <param name="id">长期保持稳定的编号。</param>
    private static void Tag<T>(string id) where T : struct, ITag
        => Parts.Register(new SaveSection<Entity, SavedTag>(id, id, 1,
            entity => entity.Tags.Has<T>() ? new SavedTag() : null, (entity, _) => entity.AddTag<T>()));

    /// <summary>同一类型关系的稳定列表。</summary>
    private sealed class RelationValues
    {
        /// <summary>按原有顺序保留关系。</summary>
        public List<JObject> Values = new();
    }

    /// <summary>有记录即存在该标记。</summary>
    private sealed class SavedTag { }
}
