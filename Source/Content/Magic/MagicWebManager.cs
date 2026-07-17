using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Blueprints;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Core.Semantics;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Content;

public enum MagicWebPublishStatus
{
    Added,
    Duplicate,
    Invalid,
    NotMana,
    Unavailable
}

public readonly struct MagicWebPublishResult
{
    public MagicWebPublishStatus Status { get; }
    public Entity Container { get; }
    public bool Success => Status is MagicWebPublishStatus.Added or MagicWebPublishStatus.Duplicate;

    public MagicWebPublishResult(MagicWebPublishStatus status, Entity container = default)
    {
        Status = status;
        Container = container;
    }
}

public sealed class MagicWebQuery
{
    public HashSet<SemanticAsset> RequiredSemantics { get; } = new();
    public HashSet<SemanticAsset> AnySemantics { get; } = new();
    public HashSet<SemanticAsset> ExcludedSemantics { get; } = new();
    public int MaxRing = 12;
    public int MaxResults = MagicSetting.MagicStudyQueryLimit;
    public int SelectionSeed;
}

public sealed class MagicWebEntryView
{
    public Entity Container { get; internal set; }
    public MagicSpellProfile Profile { get; internal set; }
    public IReadOnlyCollection<SemanticAsset> Semantics { get; internal set; }
    public bool IsDefault { get; internal set; }
    public string Signature { get; internal set; }
    public long PublisherActorId { get; internal set; }
    public string PublisherName { get; internal set; }
    public double PublishedWorldTime { get; internal set; }
    public double LastAccessWorldTime { get; internal set; }
}

/// <summary>
/// 管理当前世界的魔网实体、公开法术去重索引和语义倒排索引。
/// 被收录的法术容器视为不可变对象；改进法术必须先克隆，再上传新容器。
/// </summary>
[Dependency(typeof(SkillEntities), typeof(SkillCastResources))]
public sealed class MagicWebManager : ICanInit, ICanReload
{
    private sealed class Entry
    {
        public Entity Container;
        public string Signature;
        public HashSet<SemanticAsset> Semantics;
        public MagicSpellProfile Profile;
        public double LastAccessWorldTime;
        public double PublishedWorldTime;
        public long PublisherActorId;
        public string PublisherName;
        public bool IsDefault;
    }

    private readonly Dictionary<string, Entity> _containersBySignature = new(StringComparer.Ordinal);
    private readonly Dictionary<SemanticAsset, HashSet<Entity>> _containersBySemantic = new();
    private readonly Dictionary<int, Entry> _entriesByEntityId = new();
    private readonly HashSet<Entity>[] _containersByRing = Enumerable.Range(0, 13)
        .Select(_ => new HashSet<Entity>()).ToArray();

    private Entity _magicWebEntity;
    private bool _defaultsSeeded;
    private double _nextSweepWorldTime;

    public static MagicWebManager Instance { get; private set; }

    /// <summary>魔网条目集合或条目寿命发生变化时触发。</summary>
    public event Action Changed;

    public Entity MagicWebEntity => _magicWebEntity;
    public int Count => _entriesByEntityId.Count;
    public int DefaultCount => _entriesByEntityId.Values.Count(entry => entry.IsDefault);

    public void Init()
    {
        Instance = this;
        Cultiway.Patch.PatchMapBox.RegisterActionOnClearWorld(ClearWorldState);
        ModClass.I.GeneralLogicSystems.Add(new MagicWebUpdateSystem(this));
        Tick();
    }

    public void OnReload()
    {
        Clear();
    }

    /// <summary>
    /// 上传一个资源需求中包含 mana 的法术。重复结构返回魔网中的规范容器，不接管上传容器。
    /// </summary>
    public MagicWebPublishResult TryPublish(Entity container, ActorExtend publisher = null)
    {
        if (!EnsureReady()) return new MagicWebPublishResult(MagicWebPublishStatus.Unavailable);
        var result = Register(container, false, publisher);
        if (result.Success) Changed?.Invoke();
        return result;
    }

    /// <summary>
    /// 按结构签名查找公开法术，不更新访问时间。
    /// </summary>
    public bool TryFindBySignature(string signature, out Entity container)
    {
        if (string.IsNullOrEmpty(signature))
        {
            container = default;
            return false;
        }

        return _containersBySignature.TryGetValue(signature, out container);
    }

    /// <summary>
    /// 按结构签名访问公开法术，并更新其最后访问时间。
    /// </summary>
    public bool TryAccessBySignature(string signature, out Entity container)
    {
        if (!TryFindBySignature(signature, out container)) return false;
        Touch(container);
        return true;
    }

    /// <summary>
    /// 判断容器当前是否仍是魔网中的公开条目。
    /// </summary>
    public bool Contains(Entity container)
    {
        return TryGetEntry(container, out _);
    }

    /// <summary>
    /// 获取魔网收录时计算的法术档案。
    /// </summary>
    public bool TryGetProfile(Entity container, out MagicSpellProfile profile)
    {
        if (TryGetEntry(container, out var entry))
        {
            profile = entry.Profile;
            return true;
        }

        profile = null;
        return false;
    }

    /// <summary>
    /// 按环级以及必选、任选、排除语义执行有界查询，查询本身不刷新访问时间。
    /// </summary>
    public IReadOnlyList<MagicWebEntryView> Query(MagicWebQuery query)
    {
        if (query == null || query.MaxResults <= 0) return Array.Empty<MagicWebEntryView>();

        var maxRing = Math.Clamp(query.MaxRing, 0, 12);
        HashSet<Entity> candidates = null;
        foreach (var semantic in query.RequiredSemantics)
        {
            if (!_containersBySemantic.TryGetValue(semantic, out var matched))
                return Array.Empty<MagicWebEntryView>();
            if (candidates == null || matched.Count < candidates.Count) candidates = matched;
        }

        HashSet<Entity> ownedCandidates = null;
        if (candidates == null && query.AnySemantics.Count > 0)
        {
            ownedCandidates = new HashSet<Entity>();
            foreach (var semantic in query.AnySemantics)
            {
                if (_containersBySemantic.TryGetValue(semantic, out var matched)) ownedCandidates.UnionWith(matched);
            }
            candidates = ownedCandidates;
        }

        if (candidates == null)
        {
            ownedCandidates = new HashSet<Entity>();
            for (var ring = 0; ring <= maxRing; ring++) ownedCandidates.UnionWith(_containersByRing[ring]);
            candidates = ownedCandidates;
        }

        var result = new List<MagicWebEntryView>(Math.Min(candidates.Count, query.MaxResults));
        foreach (var container in candidates
                     .OrderBy(entity => ResolveQueryOrder(entity.Id, query.SelectionSeed))
                     .ThenBy(entity => entity.Id))
        {
            if (!TryGetEntry(container, out var entry)) continue;
            if (entry.Profile.Ring > maxRing) continue;
            if (query.RequiredSemantics.Any(semantic => !entry.Semantics.Contains(semantic))) continue;
            if (query.AnySemantics.Count > 0 && !query.AnySemantics.Any(entry.Semantics.Contains)) continue;
            if (query.ExcludedSemantics.Any(entry.Semantics.Contains)) continue;

            result.Add(new MagicWebEntryView
            {
                Container = container,
                Profile = entry.Profile,
                Semantics = entry.Semantics.OrderBy(semantic => semantic.id, StringComparer.Ordinal).ToArray(),
                IsDefault = entry.IsDefault,
                Signature = entry.Signature,
                PublisherActorId = entry.PublisherActorId,
                PublisherName = entry.PublisherName,
                PublishedWorldTime = entry.PublishedWorldTime,
                LastAccessWorldTime = entry.LastAccessWorldTime
            });
            if (result.Count >= query.MaxResults) break;
        }

        return result;
    }

    private static int ResolveQueryOrder(int entityId, int selectionSeed)
    {
        unchecked
        {
            var value = (uint)(entityId ^ selectionSeed);
            value ^= value >> 16;
            value *= 0x7feb352d;
            value ^= value >> 15;
            value *= 0x846ca68b;
            value ^= value >> 16;
            return (int)(value & 0x7fffffff);
        }
    }

    /// <summary>
    /// 获取同时拥有全部指定语义的法术。候选查询本身不计为访问。
    /// </summary>
    public IReadOnlyList<Entity> QueryBySemantics(
        IEnumerable<SemanticAsset> requiredSemantics,
        int maxResults = int.MaxValue)
    {
        var query = new MagicWebQuery { MaxResults = maxResults };
        if (requiredSemantics != null)
        {
            query.RequiredSemantics.UnionWith(requiredSemantics);
        }
        return Query(query).Select(entry => entry.Container).ToArray();
    }

    /// <summary>
    /// 返回法术在魔网中的语义副本。
    /// </summary>
    public IReadOnlyCollection<SemanticAsset> GetSemantics(Entity container)
    {
        return TryGetEntry(container, out var entry)
            ? entry.Semantics.OrderBy(semantic => semantic.id, StringComparer.Ordinal).ToArray()
            : Array.Empty<SemanticAsset>();
    }

    /// <summary>
    /// 记录一次对魔网法术的明确访问。候选扫描和普通施法不应调用此方法。
    /// </summary>
    public bool Touch(Entity container)
    {
        if (!TryGetEntry(container, out var entry)) return false;
        entry.LastAccessWorldTime = GetWorldTime();
        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// 让魔法体系单位直接学习魔网中的规范容器，并记录访问时间。
    /// </summary>
    public SkillOwnershipResult Learn(ActorExtend learner, Entity container)
    {
        if (learner == null || !learner.HasCultisys<Magic>()) return SkillOwnershipResult.Invalid;
        if (!TryGetEntry(container, out _)) return SkillOwnershipResult.Invalid;

        var result = SkillOwnershipService.Learn(learner, container);
        if (result is SkillOwnershipResult.Added or SkillOwnershipResult.Duplicate) Touch(container);
        return result;
    }

    /// <summary>
    /// 清除当前世界的魔网根实体和全部运行时索引。
    /// </summary>
    public static void ClearWorldState()
    {
        Instance?.Clear();
    }

    internal void Tick()
    {
        if (!EnsureReady()) return;

        var now = GetWorldTime();
        if (now < _nextSweepWorldTime) return;
        _nextSweepWorldTime = now + MagicSetting.MagicWebSweepIntervalYears * TimeScales.SecPerYear;
        SweepExpired(now);
    }

    private bool EnsureReady()
    {
        if (World.world?.map_stats == null) return false;

        if (_magicWebEntity.IsNull)
        {
            _magicWebEntity = ModClass.I.W.CreateEntity(new MagicWeb());
            _defaultsSeeded = false;
            _nextSweepWorldTime = GetWorldTime();
        }

        if (!_defaultsSeeded)
        {
            SeedDefaults();
        }

        return true;
    }

    private void SeedDefaults()
    {
        if (SkillCastResources.Mana == null) return;

        var added = 0;
        foreach (var asset in ModClass.I.SkillV3.SkillLib.list)
        {
            if (!asset.CanBeLearned || !asset.EditorSelectable || asset.Animations.Count == 0) continue;

            for (var animationIndex = 0; animationIndex < asset.Animations.Count; animationIndex++)
            {
                Entity container = default;
                try
                {
                    container = new SkillContainerBuilder(asset)
                        .UseAnimation(animationIndex)
                        .UseCastResources(SkillCastResourceRequirement.Single(SkillCastResources.Mana))
                        .Build(SkillContainerBuildMode.RuleOnly);
                    var result = Register(container, true);
                    if (result.Status == MagicWebPublishStatus.Added)
                    {
                        added++;
                    }
                    else
                    {
                        SkillBlueprintCompiler.Recycle(container);
                    }
                }
                catch (Exception exception)
                {
                    if (!container.IsNull) SkillBlueprintCompiler.Recycle(container);
                    ModClass.LogError($"魔网默认法术生成失败: {asset.id}[{animationIndex}]\n{exception}");
                }
            }
        }

        _defaultsSeeded = true;
        if (added > 0) Changed?.Invoke();
        ModClass.LogInfo($"魔网已生成 {added} 个默认 mana 法术");
    }

    private MagicWebPublishResult Register(Entity container, bool isDefault, ActorExtend publisher = null)
    {
        if (container.IsNull || !container.HasComponent<SkillContainer>())
        {
            return new MagicWebPublishResult(MagicWebPublishStatus.Invalid);
        }

        ref var skill = ref container.GetComponent<SkillContainer>();
        if (skill.CastResourceRequirement?.ResourceAssetIds == null ||
            !skill.CastResourceRequirement.ResourceAssetIds.Contains(SkillCastResources.Mana.id,
                StringComparer.Ordinal))
        {
            return new MagicWebPublishResult(MagicWebPublishStatus.NotMana);
        }

        if (skill.Asset == null) return new MagicWebPublishResult(MagicWebPublishStatus.Invalid);

        var signature = SkillContainerSignature.Build(container);
        if (string.IsNullOrEmpty(signature)) return new MagicWebPublishResult(MagicWebPublishStatus.Invalid);

        if (_containersBySignature.TryGetValue(signature, out var duplicate))
        {
            if (TryGetEntry(duplicate, out var duplicateEntry))
            {
                duplicateEntry.IsDefault |= isDefault;
                duplicateEntry.LastAccessWorldTime = GetWorldTime();
            }
            return new MagicWebPublishResult(MagicWebPublishStatus.Duplicate, duplicate);
        }

        var semantics = SkillSemanticCollector.NewSet();
        SkillSemanticCollector.CollectAssetSemantics(skill.Asset, semantics);
        SkillSemanticCollector.CollectModifierSemantics(container, semantics);
        SkillSemanticCollector.CollectTrajectorySemantics(skill.Asset, container, semantics);
        var profile = MagicSpellProfile.Evaluate(container);
        if (profile == null || string.IsNullOrEmpty(profile.FamilySignature))
            return new MagicWebPublishResult(MagicWebPublishStatus.Invalid);

        if (container.Tags.Has<TagOccupied>()) container.RemoveTag<TagOccupied>();
        if (container.Tags.Has<TagRecycle>()) container.RemoveTag<TagRecycle>();

        _magicWebEntity.AddRelation(new SkillMasterRelation { SkillContainer = container });
        var now = GetWorldTime();
        var publisherActor = publisher?.Base;
        var entry = new Entry
        {
            Container = container,
            Signature = signature,
            Semantics = semantics,
            Profile = profile,
            LastAccessWorldTime = now,
            PublishedWorldTime = now,
            PublisherActorId = !isDefault && !publisherActor.isRekt() ? publisherActor.data.id : -1L,
            PublisherName = !isDefault && !publisherActor.isRekt() ? publisherActor.getName() : string.Empty,
            IsDefault = isDefault
        };
        _entriesByEntityId[container.Id] = entry;
        _containersBySignature[signature] = container;
        _containersByRing[profile.Ring].Add(container);
        foreach (var semantic in semantics)
        {
            if (!_containersBySemantic.TryGetValue(semantic, out var matched))
            {
                matched = new HashSet<Entity>();
                _containersBySemantic[semantic] = matched;
            }
            matched.Add(container);
        }

        return new MagicWebPublishResult(MagicWebPublishStatus.Added, container);
    }

    private void SweepExpired(double now)
    {
        var expiration = MagicSetting.MagicWebExpirationYears * TimeScales.SecPerYear;
        var expired = _entriesByEntityId.Values
            .Where(entry => !entry.IsDefault && now - entry.LastAccessWorldTime >= expiration)
            .ToArray();

        foreach (var entry in expired)
        {
            RemoveEntry(entry);
        }
        if (expired.Length > 0) Changed?.Invoke();
    }

    private void RemoveEntry(Entry entry)
    {
        _entriesByEntityId.Remove(entry.Container.Id);
        _containersBySignature.Remove(entry.Signature);
        _containersByRing[entry.Profile.Ring].Remove(entry.Container);
        foreach (var semantic in entry.Semantics)
        {
            if (!_containersBySemantic.TryGetValue(semantic, out var matched)) continue;
            matched.Remove(entry.Container);
            if (matched.Count == 0) _containersBySemantic.Remove(semantic);
        }

        if (entry.Container.IsNull) return;
        if (!_magicWebEntity.IsNull) _magicWebEntity.RemoveRelation<SkillMasterRelation>(entry.Container);
    }

    private bool TryGetEntry(Entity container, out Entry entry)
    {
        if (!container.IsNull && _entriesByEntityId.TryGetValue(container.Id, out entry) &&
            entry.Container == container)
        {
            return true;
        }

        entry = null;
        return false;
    }

    private void Clear()
    {
        foreach (var entry in _entriesByEntityId.Values.ToArray())
        {
            RemoveEntry(entry);
        }

        _entriesByEntityId.Clear();
        _containersBySignature.Clear();
        _containersBySemantic.Clear();
        foreach (var ring in _containersByRing) ring.Clear();
        if (!_magicWebEntity.IsNull) _magicWebEntity.DeleteEntity();
        _magicWebEntity = default;
        _defaultsSeeded = false;
        _nextSweepWorldTime = 0d;
        Changed?.Invoke();
    }

    private static double GetWorldTime()
    {
        return World.world?.map_stats?.world_time ?? 0d;
    }
}

internal sealed class MagicWebUpdateSystem : BaseSystem
{
    private readonly MagicWebManager _manager;

    public MagicWebUpdateSystem(MagicWebManager manager)
    {
        _manager = manager;
    }

    protected override void OnUpdateGroup()
    {
        base.OnUpdateGroup();
        _manager.Tick();
    }
}
