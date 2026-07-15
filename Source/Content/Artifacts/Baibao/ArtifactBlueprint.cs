using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Cultiway.Content.Components;
using Cultiway.Core.Components;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Artifacts.Baibao;

public enum ArtifactBlueprintOriginKind
{
    Forged,
    Archived,
}

/// <summary>
/// 专属法宝通过已注册 codec 写入蓝图的额外数据。
/// </summary>
[Serializable]
public sealed class ArtifactBlueprintExtensionData
{
    public string ExtensionId;
    public JToken Data;
}

/// <summary>
/// 百宝阁保存的法宝制造蓝图。只记录法宝固有结构，不记录认主、装备和当前运行状态。
/// </summary>
[Serializable]
public sealed class ArtifactBlueprint
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion = CurrentSchemaVersion;
    public string Id;
    public string Name;
    public string ShapeId;
    public ItemLevel Level;
    public ArtifactAtomData AtomData;
    public ArtifactControlProfile ControlProfile;
    public ArtifactAppearance Appearance;
    public ArtifactAbilitySet AbilitySet;
    public List<ArtifactBlueprintExtensionData> Extensions = new();
    public ArtifactBlueprintOriginKind OriginKind;
    public long SourceActorId;
    public string SourceActorName;
    public bool Favorite;
    public int SortOrder;
    public long CreatedAtUtcTicks;
    public long UpdatedAtUtcTicks;

    public ArtifactBlueprint DeepClone()
    {
        return new ArtifactBlueprint
        {
            SchemaVersion = SchemaVersion,
            Id = Id,
            Name = Name,
            ShapeId = ShapeId,
            Level = Level,
            AtomData = ArtifactBlueprintData.Clone(AtomData),
            ControlProfile = ControlProfile,
            Appearance = ArtifactBlueprintData.Clone(Appearance),
            AbilitySet = ArtifactBlueprintData.Clone(AbilitySet),
            Extensions = Extensions.Select(extension => new ArtifactBlueprintExtensionData
            {
                ExtensionId = extension.ExtensionId,
                Data = extension.Data?.DeepClone(),
            }).ToList(),
            OriginKind = OriginKind,
            SourceActorId = SourceActorId,
            SourceActorName = SourceActorName,
            Favorite = Favorite,
            SortOrder = SortOrder,
            CreatedAtUtcTicks = CreatedAtUtcTicks,
            UpdatedAtUtcTicks = UpdatedAtUtcTicks,
        };
    }
}

internal static class ArtifactBlueprintData
{
    internal static ArtifactAtomData Clone(ArtifactAtomData source)
    {
        return new ArtifactAtomData { atom_ids = source.atom_ids?.ToArray() ?? [] };
    }

    internal static ArtifactAppearance Clone(ArtifactAppearance source)
    {
        ArtifactAppearancePart[] parts = source.parts == null
            ? []
            : new ArtifactAppearancePart[source.parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            ArtifactAppearancePart part = source.parts[i];
            parts[i] = new ArtifactAppearancePart
            {
                slot = part.slot,
                module = part.module,
                variant = part.variant,
                color_scheme = part.color_scheme,
                colors = part.colors?.ToArray() ?? [],
            };
        }
        return new ArtifactAppearance
        {
            template_key = source.template_key,
            parts = parts,
        };
    }

    internal static ArtifactAbilitySet Clone(ArtifactAbilitySet source)
    {
        ArtifactAbilityInstance[] sourceAbilities = source.abilities ?? [];
        ArtifactAbilityInstance[] abilities = new ArtifactAbilityInstance[sourceAbilities.Length];
        for (int i = 0; i < abilities.Length; i++)
        {
            ArtifactAbilityInstance ability = sourceAbilities[i];
            abilities[i] = new ArtifactAbilityInstance
            {
                instance_id = ability.instance_id,
                ability_id = ability.ability_id,
                parameters = ability.parameters?.ToArray() ?? [],
                initial_state = ability.initial_state?.ToArray() ?? [],
            };
        }
        return new ArtifactAbilitySet { abilities = abilities };
    }
}

/// <summary>
/// 计算忽略目录元数据和显示名称的法宝结构签名。
/// </summary>
public static class ArtifactBlueprintSignature
{
    public static string Build(ArtifactBlueprint blueprint)
    {
        JObject payload = new()
        {
            ["shape"] = blueprint.ShapeId,
            ["level"] = JToken.FromObject(blueprint.Level),
            ["atoms"] = JToken.FromObject(blueprint.AtomData),
            ["control"] = JToken.FromObject(blueprint.ControlProfile),
            ["appearance"] = JToken.FromObject(blueprint.Appearance),
            ["abilities"] = JToken.FromObject(blueprint.AbilitySet),
            ["extensions"] = new JArray(blueprint.Extensions
                .OrderBy(extension => extension.ExtensionId, StringComparer.Ordinal)
                .Select(extension => new JObject
                {
                    ["id"] = extension.ExtensionId,
                    ["data"] = extension.Data?.DeepClone() ?? JValue.CreateNull(),
                })),
        };
        string canonical = Canonicalize(payload).ToString(Formatting.None);
        using SHA256 sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical));
        return BitConverter.ToString(hash).Replace("-", string.Empty);
    }

    private static JToken Canonicalize(JToken token)
    {
        return token switch
        {
            JObject obj => new JObject(obj.Properties()
                .OrderBy(property => property.Name, StringComparer.Ordinal)
                .Select(property => new JProperty(property.Name, Canonicalize(property.Value)))),
            JArray array => new JArray(array.Select(Canonicalize)),
            _ => token.DeepClone(),
        };
    }
}
