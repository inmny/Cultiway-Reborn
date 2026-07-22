using System.Collections.Generic;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Core.Semantics;
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

    private readonly List<SkillSemanticMatchRule> _matchRules = new();

    private Color fallbackAccentColor = Color.white;

    /// <summary>该视觉形态最能代表的语义，用于复合技能强制确定主色。</summary>
    public SemanticAsset VisualSemantic;
    /// <summary>是否应让代表语义覆盖技能档案中的普通主色；通用兜底形态会关闭此项。</summary>
    public bool PreferVisualSemantic = true;
    /// <summary>由代表语义的规范色派生出的特效色；语义未配置颜色时使用显式兜底色。</summary>
    public Color AccentColor => VisualSemantic?.HasVisualColor == true
        ? SemanticColorResolver.ToVfxColor(VisualSemantic.visual_color)
        : fallbackAccentColor;
    public float AccentBlend = 0.35f;
    public float AccentAlpha = 0.75f;
    public SkillVfxGroundFlyOver GroundFlyOver = NoFlyOver;
    public SkillVfxGroundImpact GroundImpact = NoImpact;
    public SkillFlyOverParticleStyle FlyOverParticles = SkillFlyOverParticleStyle.Default;
    public string ImpactSound;
    public float ImpactFeedbackInterval = 0.12f;
    public float AreaShakeIntensity;
    public DropAsset GrantDrop;

    public SkillVfxElementAsset SetAccent(Color color, float blend = 0.35f, float alpha = 0.75f)
    {
        fallbackAccentColor = color;
        AccentBlend = blend;
        AccentAlpha = alpha;
        return this;
    }

    /// <summary>声明视觉形态的代表语义，并配置沿用原行为的混色与透明度参数。</summary>
    public SkillVfxElementAsset SetVisualSemantic(
        SemanticAsset semantic,
        float blend = 0.35f,
        float alpha = 0.75f,
        bool prefer = true)
    {
        VisualSemantic = semantic;
        PreferVisualSemantic = prefer;
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

    public SkillVfxElementAsset SetImpactSound(string impactSound, float areaShakeIntensity = 0f)
    {
        ImpactSound = impactSound;
        AreaShakeIntensity = areaShakeIntensity;
        return this;
    }

    public SkillVfxElementAsset SetFlyOverParticles(SkillFlyOverParticleStyle style)
    {
        FlyOverParticles = style;
        return this;
    }

    public SkillVfxElementAsset SetGrantDrop(DropAsset drop)
    {
        GrantDrop = drop;
        return this;
    }

    public SkillVfxElementAsset MatchAny(int priority, params SemanticAsset[] semantics)
    {
        var rule = new SkillSemanticMatchRule(priority);
        rule.AddAny(semantics);
        _matchRules.Add(rule);
        return this;
    }

    public SkillVfxElementAsset MatchAll(int priority, params SemanticAsset[] semantics)
    {
        var rule = new SkillSemanticMatchRule(priority);
        rule.AddRequired(semantics);
        _matchRules.Add(rule);
        return this;
    }

    public int ScoreSemantics(HashSet<SemanticAsset> semantics)
    {
        var best = -1;
        foreach (var rule in _matchRules)
        {
            var score = rule.Score(semantics);
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

    public void PlayImpactSound(Vector3 position, bool isArea)
    {
        if (ImpactSound.Length > 0)
        {
            MusicBox.playSound(ImpactSound, position.x, position.y, pGameViewOnly: true);
        }

        if (isArea && AreaShakeIntensity > 0f)
        {
            World.world.startShake(0.08f, 0.02f, AreaShakeIntensity);
        }
    }
}
