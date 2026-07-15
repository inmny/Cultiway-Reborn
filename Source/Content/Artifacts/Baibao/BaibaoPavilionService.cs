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

    /// <summary>用编辑后的固有结构更新目录中的炼制蓝图，并保留其目录元数据。</summary>
    public BaibaoSaveResult Update(ArtifactBlueprint draft)
    {
        ArtifactBlueprint current = _store.Get(draft.Id);
        if (current == null)
        {
            return Invalid(draft, "百宝阁中不存在要更新的法宝蓝图");
        }

        ArtifactBlueprint blueprint = draft.DeepClone();
        blueprint.SchemaVersion = ArtifactBlueprint.CurrentSchemaVersion;
        blueprint.Id = current.Id;
        blueprint.Favorite = current.Favorite;
        blueprint.SortOrder = current.SortOrder;
        blueprint.CreatedAtUtcTicks = current.CreatedAtUtcTicks;
        blueprint.UpdatedAtUtcTicks = DateTime.UtcNow.Ticks;
        blueprint.OriginKind = current.OriginKind;
        blueprint.SourceActorId = current.SourceActorId;
        blueprint.SourceActorName = current.SourceActorName;

        string error = Validate(blueprint);
        if (error != null) return Invalid(blueprint, error);
        ArtifactBlueprint duplicate = _store.FindBySignature(ArtifactBlueprintSignature.Build(blueprint), blueprint.Id);
        if (duplicate != null) return Duplicate(duplicate);

        _store.Replace(blueprint);
        Changed?.Invoke();
        return Saved(blueprint);
    }

    /// <summary>保存新的炼制设计，仍执行结构去重。</summary>
    public BaibaoSaveResult SaveNew(ArtifactBlueprint draft)
    {
        return AddPrepared(PrepareCopy(draft));
    }

    /// <summary>按用户的明确操作另存目录副本，允许同构蓝图拥有独立目录身份。</summary>
    public BaibaoSaveResult SaveCopy(ArtifactBlueprint draft)
    {
        return AddPrepared(PrepareCopy(draft), true);
    }

    private static ArtifactBlueprint PrepareCopy(ArtifactBlueprint draft)
    {
        ArtifactBlueprint copy = draft.DeepClone();
        copy.Id = null;
        copy.OriginKind = ArtifactBlueprintOriginKind.Forged;
        copy.SourceActorId = 0;
        copy.SourceActorName = null;
        copy.Favorite = false;
        copy.SortOrder = 0;
        copy.CreatedAtUtcTicks = 0;
        copy.UpdatedAtUtcTicks = 0;
        return copy;
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
        string signature = $"icon|{ArtifactBlueprintSignature.Build(blueprint)}";
        if (_iconCache.TryGetValue(signature, out Sprite sprite)) return sprite;
        sprite = RenderSprite(blueprint, false);
        if (sprite != null) _iconCache[signature] = sprite;
        return sprite;
    }

    /// <summary>取得法宝在世界中使用的正向贴图，专属器形可以通过器形接口覆盖。</summary>
    public Sprite GetWorldSprite(ArtifactBlueprint blueprint)
    {
        string signature = $"world|{ArtifactBlueprintSignature.Build(blueprint)}";
        if (_iconCache.TryGetValue(signature, out Sprite sprite)) return sprite;
        sprite = RenderSprite(blueprint, true);
        if (sprite != null) _iconCache[signature] = sprite;
        return sprite;
    }

    /// <summary>生成临时设计的预览图标，不将尚未保存的随机结果留在目录缓存中。</summary>
    public Sprite GetPreviewIcon(ArtifactBlueprint blueprint)
    {
        return RenderSprite(blueprint, false);
    }

    /// <summary>生成临时设计的世界贴图预览。</summary>
    public Sprite GetPreviewWorldSprite(ArtifactBlueprint blueprint)
    {
        return RenderSprite(blueprint, true);
    }

    private Sprite RenderSprite(ArtifactBlueprint blueprint, bool world)
    {
        if (!TryMaterialize(blueprint, "百宝阁预览", out Entity preview)) return null;
        ref SpecialItem specialItem = ref preview.GetComponent<SpecialItem>();
        ArtifactShapeAsset shape = (ArtifactShapeAsset)specialItem.Shape.Type;
        Sprite sprite = world && shape.GetWorldSprite != null
            ? shape.GetWorldSprite(preview)
            : specialItem.GetSprite();
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

        ArtifactAtomEntry[] atoms = blueprint.AtomData.entries ?? [];
        HashSet<string> atomIds = new(StringComparer.Ordinal);
        for (int i = 0; i < atoms.Length; i++)
        {
            ArtifactAtomEntry atom = atoms[i];
            if (string.IsNullOrWhiteSpace(atom.atom_id) || !atomIds.Add(atom.atom_id))
                return $"法宝 atom 重复或缺少 ID: {atom.atom_id}";
            if (InvalidNumber(atom.strength) || atom.strength <= 0f) return $"法宝 atom 强度无效: {atom.atom_id}";
            if (Libraries.Manager.ArtifactAtomLibrary.get(atom.atom_id) == null)
                return $"法宝 atom 不存在: {atom.atom_id}";
        }

        ArtifactMaterialData materialData = blueprint.MaterialData;
        if (materialData.ingredient_count <= 0) return "法宝材料数量无效";
        if (InvalidNumber(materialData.quality_budget) || materialData.quality_budget < 0f)
            return "法宝材料品质预算无效";
        if (InvalidNumber(materialData.stability) || materialData.stability is < 0f or > 1f)
            return "法宝材料稳定度无效";
        if (InvalidNumber(materialData.complexity) || materialData.complexity < 0f)
            return "法宝材料复杂度无效";
        ArtifactMaterialRecord[] materials = materialData.materials ?? [];
        HashSet<string> materialKeys = new(StringComparer.Ordinal);
        for (int i = 0; i < materials.Length; i++)
        {
            ArtifactMaterialRecord material = materials[i];
            if (material.count <= 0 || material.quality is < 0 or > 35 ||
                ModClass.L.ItemShapeLibrary.get(material.shape_id) == null)
            {
                return $"法宝材料记录无效: {material.shape_id}";
            }
            if (!materialKeys.Add(material.GetIdentityKey()))
                return $"法宝材料记录重复: {material.shape_id}";
            if (InvalidMaterialNumbers(material))
                return $"法宝材料数值无效: {material.shape_id}";
        }
        ArtifactMaterialTrait[] traits = materialData.traits ?? [];
        HashSet<string> traitKeys = new(StringComparer.Ordinal);
        for (int i = 0; i < traits.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(traits[i].key) || !traitKeys.Add(traits[i].key) ||
                InvalidNumber(traits[i].value))
            {
                return $"法宝材料 trait 无效: {traits[i].key}";
            }
        }

        string appearanceError = ValidateAppearance(blueprint,
            (ArtifactShapeAsset)ModClass.L.ItemShapeLibrary.get(blueprint.ShapeId));
        if (appearanceError != null) return appearanceError;

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

    private static bool InvalidNumber(float value)
    {
        return float.IsNaN(value) || float.IsInfinity(value);
    }

    private static bool InvalidMaterialNumbers(ArtifactMaterialRecord material)
    {
        return InvalidNumber(material.iron) || InvalidNumber(material.wood) ||
               InvalidNumber(material.water) || InvalidNumber(material.fire) ||
               InvalidNumber(material.earth) || InvalidNumber(material.neg) ||
               InvalidNumber(material.pos) || InvalidNumber(material.entropy) ||
               InvalidNumber(material.jing) || InvalidNumber(material.qi) ||
               InvalidNumber(material.shen) || InvalidNumber(material.jindan_strength);
    }

    private static string ValidateAppearance(ArtifactBlueprint blueprint, ArtifactShapeAsset shape)
    {
        ArtifactAppearance appearance = blueprint.Appearance;
        if (string.IsNullOrEmpty(appearance.template_key)) return null;

        ArtifactAppearanceCatalog catalog = ArtifactAppearanceCatalogLoader.Current;
        if (!catalog.Templates.TryGetValue(appearance.template_key, out ArtifactAppearanceTemplateDef template))
            return $"法宝外观模板不存在: {appearance.template_key}";
        if (template.Shape != shape.appearance_family)
            return $"法宝外观模板与器形不匹配: {appearance.template_key}";

        ArtifactAppearancePart[] parts = appearance.parts ?? [];
        if (parts.Length != template.Placements.Length) return "法宝外观槽位数量与模板不匹配";
        HashSet<string> slots = new(StringComparer.Ordinal);
        for (int i = 0; i < template.Placements.Length; i++)
        {
            ArtifactAppearancePlacementDef placement = template.Placements[i];
            int partIndex = Array.FindIndex(parts, part => part.slot == placement.Slot);
            if (partIndex < 0 || !slots.Add(placement.Slot)) return $"法宝外观缺少或重复槽位: {placement.Slot}";
            ArtifactAppearancePart part = parts[partIndex];
            if (part.module != placement.Module) return $"法宝外观槽位模块不匹配: {placement.Slot}";
            ArtifactAppearanceModuleDef module = catalog.Modules[placement.Module];
            ArtifactAppearanceVariantDef variant = module.GetVariant(part.variant);
            if (variant == null || variant.GetAnchor(placement.Anchor) == null)
                return $"法宝外观变体无法放入槽位: {placement.Slot}";
            if (!string.IsNullOrEmpty(part.color_scheme) && !catalog.ColorSchemes.ContainsKey(part.color_scheme))
                return $"法宝外观配色不存在: {part.color_scheme}";

            ArtifactAppearanceColor[] colors = part.colors ?? [];
            HashSet<string> materials = new(StringComparer.Ordinal);
            for (int j = 0; j < colors.Length; j++)
            {
                if (string.IsNullOrWhiteSpace(colors[j].material) || !materials.Add(colors[j].material) ||
                    !ColorUtility.TryParseHtmlString(colors[j].color_hex, out _))
                {
                    return $"法宝外观自定义颜色无效: {placement.Slot}";
                }
            }
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

    public void ClearSelected()
    {
        if (_selectedBlueprintIds.Count == 0) return;
        _selectedBlueprintIds.Clear();
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

    private BaibaoSaveResult AddPrepared(ArtifactBlueprint blueprint, bool allowDuplicate = false)
    {
        blueprint.Id = Guid.NewGuid().ToString("N");
        blueprint.SchemaVersion = ArtifactBlueprint.CurrentSchemaVersion;
        blueprint.CreatedAtUtcTicks = DateTime.UtcNow.Ticks;
        blueprint.UpdatedAtUtcTicks = blueprint.CreatedAtUtcTicks;
        string error = Validate(blueprint);
        if (error != null)
        {
            return Invalid(blueprint, error);
        }

        ArtifactBlueprint duplicate = allowDuplicate
            ? null
            : _store.FindBySignature(ArtifactBlueprintSignature.Build(blueprint));
        if (duplicate != null)
        {
            return Duplicate(duplicate);
        }

        _store.Add(blueprint);
        _selectedBlueprintIds.Add(blueprint.Id);
        _store.SaveSelection(_selectedBlueprintIds);
        Changed?.Invoke();
        return Saved(blueprint);
    }

    private static BaibaoSaveResult Saved(ArtifactBlueprint blueprint)
    {
        return new BaibaoSaveResult { Status = BaibaoSaveStatus.Saved, Blueprint = blueprint };
    }

    private static BaibaoSaveResult Duplicate(ArtifactBlueprint blueprint)
    {
        return new BaibaoSaveResult { Status = BaibaoSaveStatus.Duplicate, Blueprint = blueprint };
    }

    private static BaibaoSaveResult Invalid(ArtifactBlueprint blueprint, string error)
    {
        return new BaibaoSaveResult
        {
            Status = BaibaoSaveStatus.Invalid,
            Blueprint = blueprint,
            Error = error,
        };
    }
}
