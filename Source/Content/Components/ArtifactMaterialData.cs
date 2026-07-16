using System.Globalization;
using System.Text;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Components;

/// <summary>
/// 一类语义相同的炼器材料。相同记录会合并数量，避免材料数量增大时重复保存相同数据。
/// </summary>
public struct ArtifactMaterialRecord
{
    public string shape_id;
    public string source_asset_id;
    public string jindan_id;
    public int quality;
    public int count;
    public float iron;
    public float wood;
    public float water;
    public float fire;
    public float earth;
    public float neg;
    public float pos;
    public float entropy;
    public float jing;
    public float qi;
    public float shen;
    public float jindan_strength;

    public string GetIdentityKey()
    {
        StringBuilder builder = new();
        builder.Append(shape_id).Append('|')
            .Append(source_asset_id).Append('|')
            .Append(jindan_id).Append('|')
            .Append(quality);
        Append(builder, iron);
        Append(builder, wood);
        Append(builder, water);
        Append(builder, fire);
        Append(builder, earth);
        Append(builder, neg);
        Append(builder, pos);
        Append(builder, entropy);
        Append(builder, jing);
        Append(builder, qi);
        Append(builder, shen);
        Append(builder, jindan_strength);
        return builder.ToString();
    }

    private static void Append(StringBuilder builder, float value)
    {
        builder.Append('|').Append(value.ToString("R", CultureInfo.InvariantCulture));
    }
}

/// <summary>
/// 材料及 atom 汇总得到的一个命名语义值。
/// </summary>
public struct ArtifactMaterialTrait
{
    public string key;
    public float value;
}

/// <summary>
/// 法器炼成时固化的材料语义。后续能力、重炼和历史系统直接读取该结果。
/// </summary>
public struct ArtifactMaterialData : IComponent
{
    public ArtifactMaterialRecord[] materials = [];
    public ArtifactMaterialTrait[] traits = [];
    public int ingredient_count;
    public float quality_budget;
    public float stability;
    public float complexity;

    public ArtifactMaterialData()
    {
    }

    public float GetTrait(string key)
    {
        ArtifactMaterialTrait[] values = traits ?? [];
        for (int i = 0; i < values.Length; i++)
        {
            if (values[i].key == key) return values[i].value;
        }
        return 0f;
    }

    public int CountShape(string shapeId)
    {
        int count = 0;
        ArtifactMaterialRecord[] values = materials ?? [];
        for (int i = 0; i < values.Length; i++)
        {
            if (values[i].shape_id == shapeId) count += values[i].count;
        }
        return count;
    }

    public int CountSource(string sourceAssetId)
    {
        int count = 0;
        ArtifactMaterialRecord[] values = materials ?? [];
        for (int i = 0; i < values.Length; i++)
        {
            if (values[i].source_asset_id == sourceAssetId) count += values[i].count;
        }
        return count;
    }

    public string GetCacheKey()
    {
        StringBuilder builder = new();
        ArtifactMaterialRecord[] materialValues = materials ?? [];
        for (int i = 0; i < materialValues.Length; i++)
        {
            builder.Append("|m:").Append(materialValues[i].GetIdentityKey())
                .Append('*').Append(materialValues[i].count);
        }

        ArtifactMaterialTrait[] traitValues = traits ?? [];
        for (int i = 0; i < traitValues.Length; i++)
        {
            builder.Append("|t:").Append(traitValues[i].key).Append('=')
                .Append(traitValues[i].value.ToString("R", CultureInfo.InvariantCulture));
        }
        builder.Append("|n:").Append(ingredient_count)
            .Append("|q:").Append(quality_budget.ToString("R", CultureInfo.InvariantCulture))
            .Append("|s:").Append(stability.ToString("R", CultureInfo.InvariantCulture))
            .Append("|c:").Append(complexity.ToString("R", CultureInfo.InvariantCulture));
        return builder.ToString();
    }
}

/// <summary>
/// 法器材料与 atom 共用的语义键。新增能力应依赖这些语义，而不是判断具体器形。
/// </summary>
public static class ArtifactMaterialTraits
{
    public const string Iron = "element.iron";
    public const string Wood = "element.wood";
    public const string Water = "element.water";
    public const string Fire = "element.fire";
    public const string Earth = "element.earth";
    public const string Neg = "element.neg";
    public const string Pos = "element.pos";
    public const string Entropy = "element.entropy";
    public const string Vitality = "essence.vitality";
    public const string Spirituality = "essence.spirituality";
    public const string Quality = "material.quality";
    public const string Quantity = "material.quantity";
    public const string SourceDiversity = "material.source_diversity";
    public const string Stability = "material.stability";
    public const string Edge = "affordance.edge";
    public const string Hardness = "affordance.hardness";
    public const string Flexibility = "affordance.flexibility";
    public const string Mobility = "affordance.mobility";
    public const string Capacity = "affordance.capacity";
    public const string Ward = "affordance.ward";
    public const string Reflection = "affordance.reflection";
    public const string Perception = "affordance.perception";
    public const string Suppression = "affordance.suppression";
    public const string Alchemy = "affordance.alchemy";
    public const string Volatility = "affordance.volatility";
    public const string Binding = "affordance.binding";
    public const string Concealment = "affordance.concealment";
    public const string Resonance = "affordance.resonance";
    public const string Purification = "affordance.purification";
    public const string Devouring = "affordance.devouring";
    public const string Amplification = "affordance.amplification";
    public const string Impact = "affordance.impact";
    public const string Projection = "affordance.projection";
    public const string Storage = "affordance.storage";
    public const string Sealing = "affordance.sealing";
    public const string Sound = "affordance.sound";
    public const string Soul = "affordance.soul";
    public const string Space = "affordance.space";
    public const string Transformation = "affordance.transformation";
    public const string Sustain = "affordance.sustain";
    public const string PiercingFlight = "capability.piercing_flight";
    public const string AlchemyVessel = "capability.alchemy_vessel";
    public const string GuardianWard = "capability.guardian_ward";
    public const string Insight = "capability.insight";
    public const string Renewal = "capability.renewal";
    public const string SpiritReservoir = "capability.spirit_reservoir";
    public const string FieldProjection = "capability.field_projection";
    public const string Vehicle = "capability.vehicle";
    public const string SectGuardian = "capability.sect_guardian";
    public const string ArtifactSpirit = "capability.artifact_spirit";
}
