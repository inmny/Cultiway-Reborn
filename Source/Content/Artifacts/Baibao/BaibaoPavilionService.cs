using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Artifacts.Baibao;

public enum BaibaoSaveStatus
{
    Saved,
    Duplicate,
    Invalid,
}

public sealed class BaibaoSaveResult
{
    public BaibaoSaveStatus Status;
    public ArtifactBlueprint Blueprint;
    public string Error;
}

/// <summary>
/// 百宝阁的全局目录服务，负责炼制、收录、筛选状态和按蓝图制造独立法宝。
/// </summary>
public sealed class BaibaoPavilionService
{
    private BaibaoPavilionStore _store;
    private readonly HashSet<string> _selectedBlueprintIds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Sprite> _iconCache = new(StringComparer.Ordinal);

    public static BaibaoPavilionService Instance { get; private set; }
    public event Action Changed;
    public event Action<Actor> ArchiveRequested;
    public IReadOnlyList<ArtifactBlueprint> Blueprints => _store.Blueprints;
    public int SelectedBlueprintCount => _selectedBlueprintIds.Count;

    public void Init()
    {
        _store = new BaibaoPavilionStore(ModClass.I.Persistence);
        _selectedBlueprintIds.UnionWith(_store.SelectedBlueprintIds);
        Instance = this;
        BaibaoWorldToolSession.Initialize();
    }

    public ArtifactBlueprint Get(string id)
    {
        return _store.Get(id);
    }

    public bool IsSelected(string id)
    {
        return _selectedBlueprintIds.Contains(id);
    }

    public IReadOnlyList<ArtifactBlueprint> GetSelectedBlueprints()
    {
        return _store.Blueprints
            .Where(blueprint => _selectedBlueprintIds.Contains(blueprint.Id))
            .ToList();
    }

    public BaibaoSaveResult Forge(ArtifactDesignRequest design)
    {
        ArtifactComposeResult result = ArtifactComposer.ComposeDesign(design);
        return AddPrepared(ArtifactBlueprintCodec.FromComposeResult(result));
    }

    public BaibaoSaveResult Archive(Entity artifact, Actor sourceActor)
    {
        return AddPrepared(ArtifactBlueprintCodec.Capture(artifact, sourceActor));
    }

    public bool IsArchived(Entity artifact, Actor sourceActor)
    {
        ArtifactBlueprint snapshot = ArtifactBlueprintCodec.Capture(artifact, sourceActor);
        return _store.FindBySignature(ArtifactBlueprintSignature.Build(snapshot)) != null;
    }

    /// <summary>
    /// 返回角色库存中当前可被百宝阁收录的完整法宝实体。收录只制作蓝图，不转移原物。
    /// </summary>
    public List<Entity> GetArchivableArtifacts(Actor actor)
    {
        return actor.GetExtend().GetItems()
            .Where(item => item.IsAvailable() && item.HasComponent<Artifact>() && item.HasComponent<SpecialItem>())
            .OrderBy(item => item.Id)
            .ToList();
    }

    public void RequestArchive(Actor actor)
    {
        ArchiveRequested?.Invoke(actor);
    }

    public bool TryMaterialize(ArtifactBlueprint blueprint, string creatorName, out Entity artifact)
    {
        string error = Validate(blueprint);
        if (error != null)
        {
            artifact = default;
            return false;
        }
        artifact = ArtifactBlueprintCodec.Materialize(blueprint, creatorName);
        return true;
    }

    public Sprite GetIcon(ArtifactBlueprint blueprint)
    {
        string signature = ArtifactBlueprintSignature.Build(blueprint);
        if (_iconCache.TryGetValue(signature, out Sprite sprite)) return sprite;
        sprite = RenderIcon(blueprint);
        if (sprite != null) _iconCache[signature] = sprite;
        return sprite;
    }

    /// <summary>生成临时设计的预览图标，不将尚未保存的随机结果留在目录缓存中。</summary>
    public Sprite GetPreviewIcon(ArtifactBlueprint blueprint)
    {
        return RenderIcon(blueprint);
    }

    private Sprite RenderIcon(ArtifactBlueprint blueprint)
    {
        if (!TryMaterialize(blueprint, "百宝阁预览", out Entity preview)) return null;
        Sprite sprite = preview.GetComponent<SpecialItem>().GetSprite();
        preview.DeleteEntity();
        return sprite;
    }

    public string Validate(ArtifactBlueprint blueprint)
    {
        if (blueprint == null) return "法宝蓝图为空";
        if (blueprint.SchemaVersion != ArtifactBlueprint.CurrentSchemaVersion) return "法宝蓝图版本不兼容";
        if (string.IsNullOrWhiteSpace(blueprint.Name)) return "法宝缺少名称";
        if (ModClass.L.ItemShapeLibrary.get(blueprint.ShapeId) is not ArtifactShapeAsset) return "法宝器形不存在";
        if (blueprint.Level.Stage is < 0 or > 3 || blueprint.Level.Level is < 0 or > 8)
            return "法宝品阶超出范围";
        ArtifactControlProfile control = blueprint.ControlProfile;
        if (float.IsNaN(control.complexity) || float.IsInfinity(control.complexity) || control.complexity <= 0f)
            return "法宝操控复杂度无效";
        if (float.IsNaN(control.prepared_load_ratio) || float.IsInfinity(control.prepared_load_ratio) ||
            control.prepared_load_ratio < 0f)
        {
            return "法宝准备负荷比例无效";
        }
        if (control.thread_cost < 0) return "法宝分念消耗无效";

        string[] atomIds = blueprint.AtomData.atom_ids ?? [];
        for (int i = 0; i < atomIds.Length; i++)
        {
            if (Libraries.Manager.ArtifactAtomLibrary.get(atomIds[i]) == null) return $"法宝 atom 不存在: {atomIds[i]}";
        }

        if (!string.IsNullOrEmpty(blueprint.Appearance.template_key) &&
            !ArtifactAppearanceCatalogLoader.Current.Templates.ContainsKey(blueprint.Appearance.template_key))
        {
            return $"法宝外观模板不存在: {blueprint.Appearance.template_key}";
        }

        ArtifactAbilityInstance[] abilities = blueprint.AbilitySet.abilities ?? [];
        HashSet<string> instanceIds = new(StringComparer.Ordinal);
        for (int i = 0; i < abilities.Length; i++)
        {
            ArtifactAbilityInstance ability = abilities[i];
            if (string.IsNullOrWhiteSpace(ability.instance_id)) return "法宝能力实例缺少 ID";
            if (string.IsNullOrWhiteSpace(ability.ability_id)) return "法宝能力缺少类型 ID";
            if (!instanceIds.Add(ability.instance_id)) return $"法宝能力实例重复: {ability.instance_id}";
            ArtifactAbilityAsset asset = Libraries.Manager.ArtifactAbilityLibrary.get(ability.ability_id);
            if (asset == null) return $"法宝能力不存在: {ability.ability_id}";
            try
            {
                asset.ValidateInstance(ability);
            }
            catch (InvalidOperationException error)
            {
                return error.Message;
            }
        }

        List<ArtifactBlueprintExtensionData> extensions = blueprint.Extensions ?? new List<ArtifactBlueprintExtensionData>();
        for (int i = 0; i < extensions.Count; i++)
        {
            ArtifactBlueprintExtensionData data = extensions[i];
            ArtifactBlueprintExtensionAsset extension =
                Libraries.Manager.ArtifactBlueprintExtensionLibrary.get(data.ExtensionId);
            if (extension == null) return $"法宝蓝图扩展不存在: {data.ExtensionId}";
            string error = extension.Validate(data.Data);
            if (!string.IsNullOrEmpty(error)) return error;
        }
        return null;
    }

    public string GetShapeName(ArtifactBlueprint blueprint)
    {
        if (ModClass.L.ItemShapeLibrary.get(blueprint.ShapeId) is not ArtifactShapeAsset shape)
            return blueprint.ShapeId;
        return shape.ingredient_name_candidates.FirstOrDefault() ?? shape.id.Localize();
    }

    public void ToggleSelected(string id)
    {
        ArtifactBlueprint blueprint = _store.Get(id);
        if (blueprint == null || Validate(blueprint) != null) return;
        if (!_selectedBlueprintIds.Add(id)) _selectedBlueprintIds.Remove(id);
        _store.SaveSelection(_selectedBlueprintIds);
        Changed?.Invoke();
    }

    public void SetFavorite(string id, bool favorite)
    {
        ArtifactBlueprint blueprint = _store.Get(id);
        if (blueprint == null) return;
        blueprint.Favorite = favorite;
        blueprint.UpdatedAtUtcTicks = DateTime.UtcNow.Ticks;
        _store.Replace(blueprint);
        Changed?.Invoke();
    }

    public bool Delete(string id)
    {
        bool removed = _store.Remove(id);
        if (!removed) return false;
        if (_selectedBlueprintIds.Remove(id)) _store.SaveSelection(_selectedBlueprintIds);
        Changed?.Invoke();
        return true;
    }

    public void Move(string id, int offset)
    {
        List<string> ordered = _store.Blueprints
            .OrderBy(blueprint => blueprint.SortOrder)
            .Select(blueprint => blueprint.Id)
            .ToList();
        int index = ordered.IndexOf(id);
        int target = index + offset;
        if (index < 0 || target < 0 || target >= ordered.Count) return;
        (ordered[index], ordered[target]) = (ordered[target], ordered[index]);
        _store.SaveOrder(ordered);
        Changed?.Invoke();
    }

    private BaibaoSaveResult AddPrepared(ArtifactBlueprint blueprint)
    {
        blueprint.Id = Guid.NewGuid().ToString("N");
        blueprint.SchemaVersion = ArtifactBlueprint.CurrentSchemaVersion;
        blueprint.CreatedAtUtcTicks = DateTime.UtcNow.Ticks;
        blueprint.UpdatedAtUtcTicks = blueprint.CreatedAtUtcTicks;
        string error = Validate(blueprint);
        if (error != null)
        {
            return new BaibaoSaveResult
            {
                Status = BaibaoSaveStatus.Invalid,
                Blueprint = blueprint,
                Error = error,
            };
        }

        ArtifactBlueprint duplicate = _store.FindBySignature(ArtifactBlueprintSignature.Build(blueprint));
        if (duplicate != null)
        {
            return new BaibaoSaveResult
            {
                Status = BaibaoSaveStatus.Duplicate,
                Blueprint = duplicate,
            };
        }

        _store.Add(blueprint);
        _selectedBlueprintIds.Add(blueprint.Id);
        _store.SaveSelection(_selectedBlueprintIds);
        Changed?.Invoke();
        return new BaibaoSaveResult
        {
            Status = BaibaoSaveStatus.Saved,
            Blueprint = blueprint,
        };
    }
}
