using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Cultiway.Core.SkillLibV3.Blueprints;

public static class SkillBlueprintSignature
{
    public static string Build(SkillBlueprint blueprint)
    {
        if (blueprint == null) return string.Empty;

        var canonical = BuildCanonical(blueprint);
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical));
        var result = new StringBuilder(hash.Length * 2);
        foreach (var value in hash)
        {
            result.Append(value.ToString("x2"));
        }
        return result.ToString();
    }

    internal static string BuildCanonical(SkillBlueprint blueprint)
    {
        var builder = new StringBuilder();
        Append(builder, blueprint.EntityAssetId);
        Append(builder, blueprint.TrajectoryAssetId);

        foreach (var modifier in blueprint.Modifiers
                     .OrderBy(item => item.AssetId, StringComparer.Ordinal))
        {
            Append(builder, modifier.AssetId);
            foreach (var parameter in modifier.Parameters.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                Append(builder, parameter.Key);
                Append(builder, parameter.Value);
            }
        }
        return builder.ToString();
    }

    private static void Append(StringBuilder builder, string value)
    {
        value ??= string.Empty;
        builder.Append(value.Length);
        builder.Append(':');
        builder.Append(value);
        builder.Append('|');
    }
}
