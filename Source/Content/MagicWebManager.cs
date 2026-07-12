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

/// <summary>
/// 管理当前世界的魔网实体、公开法术去重索引和语义标签倒排索引。
/// 被收录的法术容器视为不可变对象；改进法术必须先克隆，再上传新容器。
/// </summary>
[Dependency(typeof(SkillEntities), typeof(SkillCastResources))]
public sealed class MagicWebManager : ICanInit, ICanReload
{
    private sealed class Entry
    {
        public Entity Container;
        public string Signature;
        public HashSet<string> Tags;
        public double LastAccessWorldTime;
        public bool IsDefault;
    }

    private readonly Dictionary<string, Entity> _containersBySignature = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<Entity>> _containersByTag = new(StringComparer.Ordinal);
    private readonly Dictionary<int, Entry> _entriesByEntityId = new();

    private Entity _magicWebEntity;
    private bool _defaultsSeeded;
    private double _nextSweepWorldTime;

    public static MagicWebManager Instance { get; private set; }

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
    public MagicWebPublishResult TryPublish(Entity container)
    {
        if (!EnsureReady()) return new MagicWebPublishResult(MagicWebPublishStatus.Unavailable);
        return Register(container, false);
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
    /// 获取同时拥有全部指定语义标签的法术。候选查询本身不计为访问。
    /// </summary>
    public IReadOnlyList<Entity> QueryByTags(IEnumerable<string> requiredTags, int maxResults = int.MaxValue)
    {
        if (maxResults <= 0) return Array.Empty<Entity>();

        var tags = requiredTags?
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? Array.Empty<string>();

        if (tags.Length == 0)
        {
            return _entriesByEntityId.Values
                .Select(entry => entry.Container)
                .OrderBy(container => container.Id)
                .Take(maxResults)
                .ToArray();
        }

        HashSet<Entity> candidates = null;
        foreach (var tag in tags)
        {
            if (!_containersByTag.TryGetValue(tag, out var tagged)) return Array.Empty<Entity>();
            if (candidates == null || tagged.Count < candidates.Count) candidates = tagged;
        }

        var result = new List<Entity>(Math.Min(candidates.Count, maxResults));
        foreach (var container in candidates.OrderBy(entity => entity.Id))
        {
            if (!TryGetEntry(container, out var entry)) continue;
            if (!tags.All(entry.Tags.Contains)) continue;
            result.Add(container);
            if (result.Count >= maxResults) break;
        }

        return result;
    }

    /// <summary>
    /// 返回法术在魔网中的分类标签副本。
    /// </summary>
    public IReadOnlyCollection<string> GetTags(Entity container)
    {
        return TryGetEntry(container, out var entry)
            ? entry.Tags.OrderBy(tag => tag, StringComparer.Ordinal).ToArray()
            : Array.Empty<string>();
    }

    /// <summary>
    /// 记录一次对魔网法术的明确访问。候选扫描和普通施法不应调用此方法。
    /// </summary>
    public bool Touch(Entity container)
    {
        if (!TryGetEntry(container, out var entry)) return false;
        entry.LastAccessWorldTime = GetWorldTime();
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
            if (!asset.EditorSelectable || asset.Animations.Count == 0) continue;

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
        ModClass.LogInfo($"魔网已生成 {added} 个默认 mana 法术");
    }

    private MagicWebPublishResult Register(Entity container, bool isDefault)
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

        var semanticTags = SkillSemanticTags.NewSet();
        SkillSemanticTags.CollectAssetTags(skill.Asset, semanticTags);
        SkillSemanticTags.CollectModifierTags(container, semanticTags);
        SkillSemanticTags.CollectTrajectoryTags(skill.Asset, container, semanticTags);

        if (container.Tags.Has<TagOccupied>()) container.RemoveTag<TagOccupied>();
        if (container.Tags.Has<TagRecycle>()) container.RemoveTag<TagRecycle>();

        _magicWebEntity.AddRelation(new SkillMasterRelation { SkillContainer = container });
        var entry = new Entry
        {
            Container = container,
            Signature = signature,
            Tags = semanticTags,
            LastAccessWorldTime = GetWorldTime(),
            IsDefault = isDefault
        };
        _entriesByEntityId[container.Id] = entry;
        _containersBySignature[signature] = container;
        foreach (var tag in semanticTags)
        {
            if (!_containersByTag.TryGetValue(tag, out var tagged))
            {
                tagged = new HashSet<Entity>();
                _containersByTag[tag] = tagged;
            }
            tagged.Add(container);
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
    }

    private void RemoveEntry(Entry entry)
    {
        _entriesByEntityId.Remove(entry.Container.Id);
        _containersBySignature.Remove(entry.Signature);
        foreach (var tag in entry.Tags)
        {
            if (!_containersByTag.TryGetValue(tag, out var tagged)) continue;
            tagged.Remove(entry.Container);
            if (tagged.Count == 0) _containersByTag.Remove(tag);
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
        _containersByTag.Clear();
        if (!_magicWebEntity.IsNull) _magicWebEntity.DeleteEntity();
        _magicWebEntity = default;
        _defaultsSeeded = false;
        _nextSweepWorldTime = 0d;
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
