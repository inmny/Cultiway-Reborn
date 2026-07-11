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
    public const int CurrentSchemaVersion = 3;

    public string Id = Guid.NewGuid().ToString("N");
    public int Revision = 1;
    public int SchemaVersion = CurrentSchemaVersion;
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
