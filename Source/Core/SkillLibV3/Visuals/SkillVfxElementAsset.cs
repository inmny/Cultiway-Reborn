using System.Collections.Generic;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3.Visuals;

public delegate void SkillVfxGroundFlyOver(WorldTile tile);

public delegate void SkillVfxGroundImpact(WorldTile tile, int radius, bool isArea, BaseSimObject sourceObj);

/// <summary>
/// 法术视觉元素资产。颜色、地面掠过、命中地形反馈等元素差异都挂在这里。
/// </summary>
public class SkillVfxElementAsset : Asset
{
    private static readonly SkillVfxGroundFlyOver NoFlyOver = _ => { };
    private static readonly SkillVfxGroundImpact NoImpact = (_, _, _, _) => { };

    private readonly List<SkillVfxElementMatchRule> _matchRules = new();

    public Color AccentColor = Color.white;
    public float AccentBlend = 0.35f;
    public float AccentAlpha = 0.75f;
    public SkillVfxGroundFlyOver GroundFlyOver = NoFlyOver;
    public SkillVfxGroundImpact GroundImpact = NoImpact;

    public SkillVfxElementAsset SetAccent(Color color, float blend = 0.35f, float alpha = 0.75f)
    {
        AccentColor = color;
        AccentBlend = blend;
        AccentAlpha = alpha;
        return this;
    }

    public SkillVfxElementAsset SetGroundFlyOver(SkillVfxGroundFlyOver action)
    {
        GroundFlyOver = action;
        return this;
    }

    public SkillVfxElementAsset SetGroundImpact(SkillVfxGroundImpact action)
    {
        GroundImpact = action;
        return this;
    }

    public SkillVfxElementAsset MatchAny(int priority, params string[] tags)
    {
        var rule = new SkillVfxElementMatchRule(priority);
        rule.AddAny(tags);
        _matchRules.Add(rule);
        return this;
    }

    public SkillVfxElementAsset MatchAll(int priority, params string[] tags)
    {
        var rule = new SkillVfxElementMatchRule(priority);
        rule.AddRequired(tags);
        _matchRules.Add(rule);
        return this;
    }

    public int ScoreTags(HashSet<string> tags)
    {
        var best = -1;
        foreach (var rule in _matchRules)
        {
            var score = rule.Score(tags);
            if (score > best) best = score;
        }

        return best;
    }

    public Color GetAccentColor(Color baseColor)
    {
        var accent = Color.Lerp(baseColor, AccentColor, AccentBlend);
        accent.a = AccentAlpha;
        return accent;
    }

    public void ApplyGroundFlyOver(WorldTile tile)
    {
        GroundFlyOver(tile);
    }

    public void ApplyGroundImpact(WorldTile tile, int radius, bool isArea, BaseSimObject sourceObj)
    {
        GroundImpact(tile, radius, isArea, sourceObj);
    }
}

public class SkillVfxElementMatchRule
{
    private readonly HashSet<string> _requiredTags = new();
    private readonly HashSet<string> _anyTags = new();
    private readonly int _priority;

    public SkillVfxElementMatchRule(int priority)
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
