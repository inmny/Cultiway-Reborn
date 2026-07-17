using System.Collections.Generic;
using Cultiway.Core.Semantics;

namespace Cultiway.Core.SkillLibV3.Utils;

/// <summary>
/// 法术语义匹配规则。优先级决定规则大类顺序，命中语义数用于同优先级细分。
/// </summary>
public sealed class SkillSemanticMatchRule
{
    private readonly HashSet<SemanticAsset> required = new();
    private readonly HashSet<SemanticAsset> any = new();
    private readonly int priority;

    public SkillSemanticMatchRule(int priority)
    {
        this.priority = priority;
    }

    public void AddRequired(params SemanticAsset[] semantics)
    {
        required.UnionWith(semantics);
    }

    public void AddAny(params SemanticAsset[] semantics)
    {
        any.UnionWith(semantics);
    }

    public int Score(HashSet<SemanticAsset> semantics)
    {
        var score = priority;
        foreach (var semantic in required)
        {
            if (!semantics.Contains(semantic)) return -1;
            score += 4;
        }

        if (any.Count == 0) return score;

        var matchedAny = 0;
        foreach (var semantic in any)
        {
            if (semantics.Contains(semantic)) matchedAny++;
        }

        return matchedAny == 0 ? -1 : score + matchedAny;
    }
}
