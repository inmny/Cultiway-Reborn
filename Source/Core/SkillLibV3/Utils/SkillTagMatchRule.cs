using System.Collections.Generic;

namespace Cultiway.Core.SkillLibV3.Utils;

/// <summary>
/// 法术语义标签匹配规则。优先级决定规则大类顺序，命中标签数用于同优先级细分。
/// </summary>
public sealed class SkillTagMatchRule
{
    private readonly HashSet<string> _requiredTags = new();
    private readonly HashSet<string> _anyTags = new();
    private readonly int _priority;

    public SkillTagMatchRule(int priority)
    {
        _priority = priority;
    }

    public void AddRequired(params string[] tags)
    {
        AddTags(_requiredTags, tags);
    }

    public void AddAny(params string[] tags)
    {
        AddTags(_anyTags, tags);
    }

    public int Score(HashSet<string> tags)
    {
        var score = _priority;
        foreach (var tag in _requiredTags)
        {
            if (!tags.Contains(tag)) return -1;
            score += 4;
        }

        if (_anyTags.Count == 0) return score;

        var matchedAny = 0;
        foreach (var tag in _anyTags)
        {
            if (tags.Contains(tag)) matchedAny++;
        }

        return matchedAny == 0 ? -1 : score + matchedAny;
    }

    private static void AddTags(HashSet<string> target, string[] tags)
    {
        foreach (var tag in tags)
        {
            target.Add(tag);
        }
    }
}
