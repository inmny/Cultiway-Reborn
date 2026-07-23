using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Core.ActorFiltering;
using Cultiway.Core.AIGCLib;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Blueprints;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Editor;
using Cultiway.Core.SkillLibV3.Visuals;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3.Wanfa;

public enum WanfaPavilionSaveStatus
{
    Saved,
    Duplicate,
    Missing,
    Invalid
}

public sealed class WanfaPavilionSaveResult
{
    public WanfaPavilionSaveStatus Status;
    public SkillBlueprint Blueprint;
    public SkillCompatibilityResult Validation;
}

public sealed class WanfaGrantConflictPrompt
{
    private Action<bool> _resolve;

    public string ActorName { get; }
    public int Revision { get; }

    public WanfaGrantConflictPrompt(string actorName, int revision, Action<bool> resolve)
    {
        ActorName = actorName;
        Revision = revision;
        _resolve = resolve;
    }

    public void Resolve(bool overwrite)
    {
        var resolve = _resolve;
        _resolve = null;
        resolve?.Invoke(overwrite);
    }
}

public sealed class WanfaPavilionService
{
    private const float AiPreviewLifetime = 60f;

    private sealed class PendingAiName
    {
        public string BlueprintId;
        public int Revision;
        public string Signature;
        public string Name;
        public Entity PreviewContainer;
    }

    private sealed class PendingAiLease
    {
        public Entity Container;
        public float ExpiresAt;
    }

    private sealed class BlueprintPreviewMetadata
    {
        public SkillVfxElementAsset VfxElement;
        public ItemLevel ItemLevel;
    }

    private WanfaPavilionLibraryStore _library;
    private readonly SkillBlueprintValidator _validator = new();
    private readonly SkillBlueprintExporter _exporter = new();
    private readonly SkillBlueprintCompiler _compiler = new();
    private readonly ConcurrentQueue<PendingAiName> _pendingAiNames = new();
    private readonly List<Entity> _testContainers = new();
    private readonly List<PendingAiLease> _aiPreviewLeases = new();
    private readonly Dictionary<string, BlueprintPreviewMetadata> _previewMetadataCache = new(StringComparer.Ordinal);
    private readonly HashSet<string> _selectedBlueprintIds = new(StringComparer.Ordinal);

    public static WanfaPavilionService Instance { get; private set; }
    public event Action Changed;
    public event Action<SkillBlueprint> TestCastRequested;
    public event Action<WanfaGrantConflictPrompt> GrantConflictRequested;
    public event Action GrantConflictsCleared;
    public event Action TestCastCompleted;
    public event Action WorldStateClearing;
    public IReadOnlyList<SkillBlueprint> Blueprints => _library.Blueprints;
    public int SelectedBlueprintCount => _selectedBlueprintIds.Count;
    /// <summary>赐法世界工具使用的角色目标筛选配置。</summary>
    public ActorFilterSettings GrantTargetFilter { get; } = new();
    public WanfaPavilionPolicyAsset ActivePolicy { get; private set; }

    public void Init()
    {
        ActorFilterCatalog.Initialize();
        ActivePolicy = WanfaPavilionPolicyLibrary.Free;
        _library = new WanfaPavilionLibraryStore(ModClass.I.Persistence);
        _selectedBlueprintIds.UnionWith(_library.SelectedBlueprintIds);
        ModClass.I.GeneralLogicSystems.Add(new WanfaPavilionUpdateSystem(this));
        Instance = this;
        WanfaGrantSession.Initialize(this);
    }

    public void RequestTestCast(SkillBlueprint draft)
    {
        TestCastRequested?.Invoke(draft);
    }

    public void RequestGrantConflict(string actorName, int revision, Action<bool> resolve)
    {
        GrantConflictRequested?.Invoke(new WanfaGrantConflictPrompt(actorName, revision, resolve));
    }

    public void ClearGrantConflicts()
    {
        GrantConflictsCleared?.Invoke();
    }

    public void CompleteTestCast()
    {
        TestCastCompleted?.Invoke();
    }

    public SkillBlueprint Get(string id)
    {
        return _library.Get(id);
    }

    public bool IsSelected(string id)
    {
        return _selectedBlueprintIds.Contains(id);
    }

    public IReadOnlyList<SkillBlueprint> GetSelectedBlueprints()
    {
        return _library.Blueprints.Where(blueprint => _selectedBlueprintIds.Contains(blueprint.Id)).ToList();
    }

    public void ToggleSelected(string id)
    {
        if (_library.Get(id) == null) return;
        if (!_selectedBlueprintIds.Add(id)) _selectedBlueprintIds.Remove(id);
        _library.SaveSelection(_selectedBlueprintIds);
        Changed?.Invoke();
    }

    public SkillCompatibilityResult Validate(SkillBlueprint blueprint)
    {
        var result = _validator.Validate(blueprint);
        if (result.IsCompatible) result.Merge(ActivePolicy.ValidateBlueprint(blueprint));
        return result;
    }

    public void UsePolicy(string policyId)
    {
        var policy = ModClass.I.SkillV3.WanfaPolicyLib.get(policyId);
        if (policy == null)
        {
            throw new InvalidOperationException(string.Format(
                "Cultiway.Wanfa.Exception.PolicyMissing".Localize(), policyId));
        }
        ActivePolicy = policy;
        Changed?.Invoke();
    }

    public SkillCompatibilityResult ValidateGrant(Actor actor, SkillBlueprint blueprint)
    {
        var result = _validator.Validate(blueprint);
        if (result.IsCompatible) result.Merge(ActivePolicy.ValidateGrant(actor, blueprint));
        return result;
    }

    public void CompleteGrant(Actor actor, SkillBlueprint blueprint)
    {
        ActivePolicy.CompleteGrant(actor, blueprint);
    }

    public SkillVfxElementAsset ResolveVfxElement(SkillBlueprint blueprint)
    {
        return TryResolvePreviewMetadata(blueprint, out var metadata) ? metadata.VfxElement : null;
    }

    public bool TryResolveItemLevel(SkillBlueprint blueprint, out ItemLevel itemLevel)
    {
        if (!TryResolvePreviewMetadata(blueprint, out var metadata))
        {
            itemLevel = default;
            return false;
        }

        itemLevel = metadata.ItemLevel;
        return true;
    }

    private bool TryResolvePreviewMetadata(SkillBlueprint blueprint, out BlueprintPreviewMetadata metadata)
    {
        metadata = null;
        var validation = Validate(blueprint);
        if (!validation.IsCompatible) return false;

        var signature = SkillBlueprintSignature.Build(blueprint);
        if (_previewMetadataCache.TryGetValue(signature, out metadata)) return true;

        var compiled = _compiler.Compile(blueprint, SkillBlueprintCompileMode.Preview);
        if (!compiled.Success) return false;
        metadata = CreatePreviewMetadata(compiled.Container);
        SkillBlueprintCompiler.Recycle(compiled.Container);
        _previewMetadataCache[signature] = metadata;
        return true;
    }

    private static BlueprintPreviewMetadata CreatePreviewMetadata(Entity container)
    {
        return new BlueprintPreviewMetadata
        {
            VfxElement = container.GetComponent<SkillContainer>().VfxElement,
            ItemLevel = container.GetComponent<ItemLevel>()
        };
    }

    public SkillBlueprint CreateDraft()
    {
        var entity = ModClass.I.SkillV3.SkillLib.list
            .Where(item => item.CanBeLearned && item.EditorSelectable && ActivePolicy.IsEntityAvailable(item.id))
            .OrderBy(item => item.EditorSortOrder)
            .FirstOrDefault();
        if (entity == null)
        {
            return new SkillBlueprint
            {
                Origin = new SkillBlueprintOriginData { Kind = SkillBlueprintOriginKind.Created }
            };
        }
        var trajectoryId = ResolveAvailableTrajectoryId(entity);
        return new SkillBlueprint
        {
            EntityAssetId = entity.id,
            CastResourceRequirement = entity.DefaultCastResourceRequirement.DeepClone(),
            TrajectoryAssetId = trajectoryId,
            Origin = new SkillBlueprintOriginData { Kind = SkillBlueprintOriginKind.Created }
        };
    }

    public string ResolveAvailableTrajectoryId(SkillEntityAsset entity, string preferredId = null)
    {
        if (!string.IsNullOrWhiteSpace(preferredId) && IsTrajectoryAvailableForEntity(entity, preferredId))
        {
            return preferredId;
        }

        var defaultId = SkillBlueprintTrajectory.ResolveDefaultId(entity);
        if (IsTrajectoryAvailableForEntity(entity, defaultId)) return defaultId;

        foreach (var trajectory in ModClass.I.SkillV3.TrajLib.list.OrderBy(item => item.EditorSortOrder))
        {
            if (!trajectory.EditorSelectable || !ActivePolicy.IsTrajectoryAvailable(trajectory.id)) continue;
            if (SkillTrajectoryCompatibility.IsCompatible(entity, trajectory))
            {
                return trajectory.id;
            }
        }
        return null;
    }

    private bool IsTrajectoryAvailableForEntity(SkillEntityAsset entity, string trajectoryId)
    {
        if (string.IsNullOrWhiteSpace(trajectoryId) || !ActivePolicy.IsTrajectoryAvailable(trajectoryId))
        {
            return false;
        }
        var trajectory = ModClass.I.SkillV3.TrajLib.get(trajectoryId);
        return trajectory != null &&
               !string.IsNullOrEmpty(trajectory.EditorDescriptionKey) &&
               (trajectory.EditorSelectable || trajectory.EditorPersistWhenHidden) &&
               SkillTrajectoryCompatibility.IsCompatible(entity, trajectory);
    }

    public WanfaPavilionSaveResult SaveNew(SkillBlueprint draft)
    {
        var blueprint = draft.DeepClone();
        blueprint.Id = Guid.NewGuid().ToString("N");
        blueprint.Revision = 1;
        blueprint.SchemaVersion = SkillBlueprint.CurrentSchemaVersion;
        blueprint.CreatedAtUtcTicks = DateTime.UtcNow.Ticks;
        blueprint.UpdatedAtUtcTicks = blueprint.CreatedAtUtcTicks;
        return SavePrepared(blueprint, false, true);
    }

    public WanfaPavilionSaveResult Update(SkillBlueprint draft)
    {
        var existing = _library.Get(draft.Id);
        if (existing == null)
        {
            return new WanfaPavilionSaveResult { Status = WanfaPavilionSaveStatus.Missing };
        }

        var blueprint = draft.DeepClone();
        blueprint.Revision = existing.Revision + 1;
        blueprint.CreatedAtUtcTicks = existing.CreatedAtUtcTicks;
        blueprint.UpdatedAtUtcTicks = DateTime.UtcNow.Ticks;
        var structureChanged = SkillBlueprintSignature.Build(existing) != SkillBlueprintSignature.Build(blueprint);
        if (structureChanged)
        {
            blueprint.RuleName = null;
            blueprint.GeneratedName = null;
        }
        return SavePrepared(blueprint, true, structureChanged || string.IsNullOrWhiteSpace(blueprint.GeneratedName));
    }

    public WanfaPavilionSaveResult Import(Actor actor, Entity container)
    {
        var exported = _exporter.Export(container, new SkillBlueprintExportOptions
        {
            PreserveContainerNameAsCustom = true,
            OriginKind = SkillBlueprintOriginKind.ActorImport,
            SourceActorId = actor.data.id
        });
        if (!exported.Success)
        {
            return new WanfaPavilionSaveResult
            {
                Status = WanfaPavilionSaveStatus.Invalid,
                Validation = exported.Validation
            };
        }
        return SaveNew(exported.Blueprint);
    }

    public bool ContainsSignature(string signature)
    {
        return _library.FindBySignature(signature) != null;
    }

    public SkillBlueprint FindBySignature(string signature)
    {
        return _library.FindBySignature(signature);
    }

    public bool Delete(string id)
    {
        var removed = _library.Remove(id);
        if (removed)
        {
            if (_selectedBlueprintIds.Remove(id)) _library.SaveSelection(_selectedBlueprintIds);
            Changed?.Invoke();
        }
        return removed;
    }

    public void SaveOrder(IReadOnlyList<string> ids)
    {
        _library.SaveOrder(ids);
        Changed?.Invoke();
    }

    public void Move(string id, int offset)
    {
        var ordered = _library.Blueprints.OrderBy(item => item.SortOrder).Select(item => item.Id).ToList();
        var index = ordered.IndexOf(id);
        var target = index + offset;
        if (index < 0 || target < 0 || target >= ordered.Count) return;
        (ordered[index], ordered[target]) = (ordered[target], ordered[index]);
        SaveOrder(ordered);
    }

    public void SetFavorite(string id, bool favorite)
    {
        var blueprint = _library.Get(id);
        if (blueprint == null) return;
        blueprint.Favorite = favorite;
        blueprint.UpdatedAtUtcTicks = DateTime.UtcNow.Ticks;
        _library.Replace(blueprint);
        Changed?.Invoke();
    }

    public string GetDisplayName(SkillBlueprint blueprint)
    {
        if (blueprint.NameMode == SkillBlueprintNameMode.Custom &&
            !string.IsNullOrWhiteSpace(blueprint.CustomName)) return blueprint.CustomName;
        if (!string.IsNullOrWhiteSpace(blueprint.GeneratedName)) return blueprint.GeneratedName;
        if (!string.IsNullOrWhiteSpace(blueprint.RuleName)) return blueprint.RuleName;
        return string.IsNullOrWhiteSpace(blueprint.EntityAssetId)
            ? "Cultiway.Wanfa.UI.UnnamedSkill".Localize()
            : blueprint.EntityAssetId.Localize();
    }

    public void TrackTestContainer(Entity container)
    {
        _testContainers.Add(container);
    }

    public static void ClearWorldState()
    {
        if (Instance == null) return;
        Instance.GrantTargetFilter.ClearWorldExpression();
        Instance.WorldStateClearing?.Invoke();
        Instance.GrantConflictsCleared?.Invoke();
        foreach (var container in Instance._testContainers)
        {
            SkillBlueprintCompiler.Recycle(container);
        }
        Instance._testContainers.Clear();
        foreach (var lease in Instance._aiPreviewLeases)
        {
            SkillBlueprintCompiler.Recycle(lease.Container);
        }
        Instance._aiPreviewLeases.Clear();
    }

    internal void Tick()
    {
        while (_pendingAiNames.TryDequeue(out var pending))
        {
            var blueprint = _library.Get(pending.BlueprintId);
            if (blueprint != null && blueprint.Revision == pending.Revision &&
                SkillBlueprintSignature.Build(blueprint) == pending.Signature &&
                !string.IsNullOrWhiteSpace(pending.Name))
            {
                blueprint.GeneratedName = pending.Name;
                _library.Replace(blueprint);
                Changed?.Invoke();
            }
            SkillBlueprintCompiler.Recycle(pending.PreviewContainer);
            _aiPreviewLeases.RemoveAll(lease => lease.Container == pending.PreviewContainer);
        }

        for (var i = _aiPreviewLeases.Count - 1; i >= 0; i--)
        {
            var lease = _aiPreviewLeases[i];
            if (lease.ExpiresAt >= Time.realtimeSinceStartup) continue;
            SkillBlueprintCompiler.Recycle(lease.Container);
            _aiPreviewLeases.RemoveAt(i);
        }

        for (var i = _testContainers.Count - 1; i >= 0; i--)
        {
            var container = _testContainers[i];
            if (!container.IsNull && container.GetIncomingLinks<SkillMasterRelation>().Count > 0) continue;
            SkillBlueprintCompiler.Recycle(container);
            _testContainers.RemoveAt(i);
        }

    }

    private WanfaPavilionSaveResult SavePrepared(SkillBlueprint blueprint, bool replace, bool requestAiName)
    {
        var validation = Validate(blueprint);
        if (!validation.IsCompatible)
        {
            return new WanfaPavilionSaveResult
            {
                Status = WanfaPavilionSaveStatus.Invalid,
                Blueprint = blueprint,
                Validation = validation
            };
        }

        var signature = SkillBlueprintSignature.Build(blueprint);
        var duplicate = _library.FindBySignature(signature, replace ? blueprint.Id : null);
        if (duplicate != null)
        {
            return new WanfaPavilionSaveResult
            {
                Status = WanfaPavilionSaveStatus.Duplicate,
                Blueprint = duplicate,
                Validation = validation
            };
        }

        var namingBlueprint = blueprint.DeepClone();
        namingBlueprint.NameMode = SkillBlueprintNameMode.Rule;
        namingBlueprint.CustomName = null;
        namingBlueprint.RuleName = null;
        namingBlueprint.GeneratedName = null;
        namingBlueprint.AiNamingEnabled = false;
        var preview = _compiler.Compile(namingBlueprint, SkillBlueprintCompileMode.Preview);
        validation.Merge(preview.Validation);
        if (!preview.Success)
        {
            return new WanfaPavilionSaveResult
            {
                Status = WanfaPavilionSaveStatus.Invalid,
                Blueprint = blueprint,
                Validation = validation
            };
        }

        blueprint.RuleName = preview.Container.Name.value;
        _previewMetadataCache[signature] = CreatePreviewMetadata(preview.Container);
        if (replace) _library.Replace(blueprint);
        else _library.Add(blueprint);
        Changed?.Invoke();

        if (requestAiName && blueprint.NameMode == SkillBlueprintNameMode.Rule && blueprint.AiNamingEnabled)
        {
            var expectedId = blueprint.Id;
            var expectedRevision = blueprint.Revision;
            var expectedSignature = SkillBlueprintSignature.Build(blueprint);
            _aiPreviewLeases.Add(new PendingAiLease
            {
                Container = preview.Container,
                ExpiresAt = Time.realtimeSinceStartup + AiPreviewLifetime
            });
            SkillNameGenerator.Instance.RequestAiNameFor(preview.Container, name =>
            {
                _pendingAiNames.Enqueue(new PendingAiName
                {
                    BlueprintId = expectedId,
                    Revision = expectedRevision,
                    Signature = expectedSignature,
                    Name = name,
                    PreviewContainer = preview.Container
                });
            });
        }
        else
        {
            SkillBlueprintCompiler.Recycle(preview.Container);
        }

        return new WanfaPavilionSaveResult
        {
            Status = WanfaPavilionSaveStatus.Saved,
            Blueprint = blueprint,
            Validation = validation
        };
    }
}

internal sealed class WanfaPavilionUpdateSystem : BaseSystem
{
    private readonly WanfaPavilionService _service;

    public WanfaPavilionUpdateSystem(WanfaPavilionService service)
    {
        _service = service;
    }

    protected override void OnUpdateGroup()
    {
        base.OnUpdateGroup();
        _service.Tick();
    }
}
