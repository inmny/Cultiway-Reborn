using System.Collections.Generic;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Utils;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3.Motions;

/// <summary>
/// 法术运动与飞行视觉配置。轨迹决定路径形状，本资产决定速度、响应、动画节奏和贴身残影。
/// </summary>
public class SkillMotionProfileAsset : Asset
{
    private readonly List<SkillTagMatchRule> _matchRules = new();

    public float BaseSpeed = 48f;
    public float MaxSpeed = 120f;
    public float MaxFlightDuration = 0.55f;
    public float TurnRate = 480f;
    public float FrameInterval = 0.065f;
    public float LaunchMultiplier = 1.2f;
    public float CruiseMultiplier = 1f;
    public float RampDuration = 0.08f;
    public AnimAfterimage Afterimage = AnimAfterimage.HorizontalTrajectory();

    public SkillMotionProfileAsset Configure(float baseSpeed, float maxSpeed, float maxFlightDuration,
        float turnRate, float frameInterval, float launchMultiplier, float cruiseMultiplier, float rampDuration,
        AnimAfterimage afterimage)
    {
        BaseSpeed = baseSpeed;
        MaxSpeed = maxSpeed;
        MaxFlightDuration = maxFlightDuration;
        TurnRate = turnRate;
        FrameInterval = frameInterval;
        LaunchMultiplier = launchMultiplier;
        CruiseMultiplier = cruiseMultiplier;
        RampDuration = rampDuration;
        Afterimage = afterimage;
        return this;
    }

    public SkillMotionProfileAsset MatchAny(int priority, params string[] tags)
    {
        var rule = new SkillTagMatchRule(priority);
        rule.AddAny(tags);
        _matchRules.Add(rule);
        return this;
    }

    public SkillMotionProfileAsset MatchAll(int priority, params string[] tags)
    {
        var rule = new SkillTagMatchRule(priority);
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

    public float ResolveSpeed(float distance)
    {
        var speed = BaseSpeed;
        if (MaxFlightDuration > 0.01f)
        {
            speed = Mathf.Max(speed, distance / MaxFlightDuration);
        }

        return Mathf.Min(speed, MaxSpeed);
    }
}
