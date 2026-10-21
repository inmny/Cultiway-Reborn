using System;
using System.Collections.Generic;
using System.Globalization;

namespace Cultiway.Core.SkillLibV3.Blueprints;

public enum SkillBlueprintNameMode
{
    Rule,
    Custom
}

public enum SkillBlueprintOriginKind
{
    Created,
    ActorImport,
    BlueprintCopy
}

[Serializable]
public sealed class SkillBlueprintOriginData
{
    public SkillBlueprintOriginKind Kind;
    public string SourceBlueprintId;
    public int SourceRevision;
    public long SourceActorId = -1L;
    public string SourceSignature;

    public SkillBlueprintOriginData DeepClone()
    {
        return new SkillBlueprintOriginData
        {
            Kind = Kind,
            SourceBlueprintId = SourceBlueprintId,
            SourceRevision = SourceRevision,
            SourceActorId = SourceActorId,
            SourceSignature = SourceSignature
        };
    }
}

[Serializable]
public sealed class SkillModifierSpec
{
    public string AssetId;
    public Dictionary<string, string> Parameters = new(StringComparer.Ordinal);

    public string Get(string key)
    {
        return Parameters[key];
    }

    public float GetFloat(string key)
    {
        return float.Parse(Get(key), NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    public int GetInt(string key)
    {
        return int.Parse(Get(key), NumberStyles.Integer, CultureInfo.InvariantCulture);
    }

    public bool GetBool(string key)
    {
        return bool.Parse(Get(key));
    }

    public SkillModifierSpec DeepClone()
    {
        return new SkillModifierSpec
        {
            AssetId = AssetId,
            Parameters = new Dictionary<string, string>(Parameters, StringComparer.Ordinal)
        };
    }
}

[Serializable]
public sealed class SkillBlueprint
{
    public const int CurrentSchemaVersion = 5;

    public string Id = Guid.NewGuid().ToString("N");
    public int Revision = 1;
    public int SchemaVersion = CurrentSchemaVersion;
    /// <summary>覆盖模板的元素性质，空值沿用模板。</summary>
    public string NatureId;
    /// <summary>实际作用范围倍率。</summary>
    public float AreaMultiplier = 1f;
    /// <summary>实际效果强度倍率。</summary>
    public float PowerMultiplier = 1f;
    /// <summary>零沿用模板寿命，正数指定持续秒数。</summary>
    public float Duration;
    /// <summary>该持续区域是否可以传导雷法。</summary>
    public bool ConductiveWater;
    /// <summary>准备或环绕阶段可关闭真实对象接触。</summary>
    public bool ContactEnabled = true;
    public string EntityAssetId;
    public int AnimationIndex;
    public SkillCastResourceRequirement CastResourceRequirement = new();
    public string TrajectoryAssetId;
    public List<SkillModifierSpec> Modifiers = new();
    public SkillBlueprintNameMode NameMode;
    public string CustomName;
    public string RuleName;
    public string GeneratedName;
    public bool AiNamingEnabled = true;
    public SkillBlueprintOriginData Origin = new();
    public long CreatedAtUtcTicks = DateTime.UtcNow.Ticks;
    public long UpdatedAtUtcTicks = DateTime.UtcNow.Ticks;
    public bool Favorite;
    public string Category;
    public int SortOrder;

    public SkillBlueprint DeepClone()
    {
        var clone = new SkillBlueprint
        {
            NatureId = NatureId, AreaMultiplier = AreaMultiplier, PowerMultiplier = PowerMultiplier,
            Duration = Duration, ConductiveWater = ConductiveWater, ContactEnabled = ContactEnabled,
            Id = Id,
            Revision = Revision,
            SchemaVersion = SchemaVersion,
            EntityAssetId = EntityAssetId,
            AnimationIndex = AnimationIndex,
            CastResourceRequirement = CastResourceRequirement.DeepClone(),
            TrajectoryAssetId = TrajectoryAssetId,
            NameMode = NameMode,
            CustomName = CustomName,
            RuleName = RuleName,
            GeneratedName = GeneratedName,
            AiNamingEnabled = AiNamingEnabled,
            Origin = Origin.DeepClone(),
            CreatedAtUtcTicks = CreatedAtUtcTicks,
            UpdatedAtUtcTicks = UpdatedAtUtcTicks,
            Favorite = Favorite,
            Category = Category,
            SortOrder = SortOrder
        };
        foreach (var modifier in Modifiers)
        {
            clone.Modifiers.Add(modifier.DeepClone());
        }

        return clone;
    }

    public SkillBlueprint CreateCopy()
    {
        var copy = DeepClone();
        copy.Id = Guid.NewGuid().ToString("N");
        copy.Revision = 1;
        copy.Origin = new SkillBlueprintOriginData
        {
            Kind = SkillBlueprintOriginKind.BlueprintCopy,
            SourceBlueprintId = Id,
            SourceRevision = Revision,
            SourceSignature = SkillBlueprintSignature.Build(this)
        };
        copy.CreatedAtUtcTicks = DateTime.UtcNow.Ticks;
        copy.UpdatedAtUtcTicks = copy.CreatedAtUtcTicks;
        return copy;
    }
}
