using Cultiway.Abstract;
using Cultiway.Core.Semantics;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Semantics;

/// <summary>
/// 集中声明具备明确视觉含义的规范语义颜色。抽象形态、传递方式和角色语义不在这里着色。
/// </summary>
[Dependency(typeof(CultivationSemantics))]
public sealed class SemanticVisualColors : ICanInit
{
    public void Init()
    {
        ConfigureElements();
        ConfigureSkillEffects();
        ConfigureCultivationSemantics();
    }

    private static void ConfigureElements()
    {
        Set(SkillSemantics.Element.Iron, 217, 210, 166, 1.25f);
        Set(SkillSemantics.Element.Wood, 96, 204, 70, 1.25f);
        Set(SkillSemantics.Element.Water, 92, 166, 242, 1.25f);
        Set(SkillSemantics.Element.Ice, 188, 230, 230, 1.5f);
        Set(SkillSemantics.Element.Poison, 137, 224, 47, 1.5f);
        Set(SkillSemantics.Element.Fire, 249, 143, 57, 1.25f);
        Set(SkillSemantics.Element.Earth, 187, 131, 71, 1.25f);
        Set(SkillSemantics.Element.Neg, 92, 28, 150, 1.25f);
        Set(SkillSemantics.Element.Pos, 255, 244, 79, 1.25f);
        Set(SkillSemantics.Element.Entropy, 242, 41, 232, 1.5f);
        Set(SkillSemantics.Element.Wind, 179, 230, 211, 1.5f);
        Set(SkillSemantics.Element.Lightning, 165, 218, 230, 1.5f);
        Set(SkillSemantics.Element.Generic, 198, 205, 216, 0.2f);
        Set(SkillSemantics.Theme.Metal, 217, 210, 166, 1f);
    }

    private static void ConfigureSkillEffects()
    {
        Set(SkillSemantics.Effect.Burn, 230, 60, 18, 1.5f);
        Set(SkillSemantics.Effect.Freeze, 210, 244, 244, 1.5f);
        Set(SkillSemantics.Effect.Blast, 230, 119, 41, 1.5f);
        Set(SkillSemantics.Effect.Growth, 114, 216, 112, 1.5f);
        Set(SkillSemantics.Effect.Pull, 87, 56, 184, 1.5f);
        Set(SkillSemantics.Effect.Random, 233, 91, 225, 1.5f);
        Set(SkillSemantics.Effect.Curse, 97, 13, 128, 1.5f);
    }

    private static void ConfigureCultivationSemantics()
    {
        Set(CultivationSemantics.Effect.ArmorBreak, 228, 213, 135, 1f);
        Set(CultivationSemantics.Effect.Binding, 103, 48, 135, 1f);
        Set(CultivationSemantics.Effect.Concealment, 68, 32, 95, 1f);
        Set(CultivationSemantics.Effect.Devouring, 54, 16, 68, 1f);
        Set(CultivationSemantics.Effect.Guardian, 201, 151, 63, 1f);
        Set(CultivationSemantics.Effect.Impact, 200, 121, 61, 1f);
        Set(CultivationSemantics.Effect.Mobility, 157, 224, 199, 1f);
        Set(CultivationSemantics.Effect.Perception, 255, 230, 106, 1f);
        Set(CultivationSemantics.Effect.Purification, 255, 240, 123, 1f);
        Set(CultivationSemantics.Effect.Recovery, 130, 219, 145, 1f);
        Set(CultivationSemantics.Effect.Resonance, 241, 214, 101, 1f);
        Set(CultivationSemantics.Effect.Revealing, 255, 245, 155, 1f);
        Set(CultivationSemantics.Effect.Sealing, 117, 64, 149, 1f);
        Set(CultivationSemantics.Effect.Storage, 83, 187, 199, 1f);
        Set(CultivationSemantics.Effect.Transformation, 215, 71, 212, 1f);
        Set(CultivationSemantics.Effect.Ward, 211, 168, 93, 1f);

        Set(CultivationSemantics.Material.Brittle, 201, 238, 240, 1f);
        Set(CultivationSemantics.Material.Capacity, 72, 169, 183, 1f);
        Set(CultivationSemantics.Material.Flexibility, 138, 215, 181, 1f);
        Set(CultivationSemantics.Material.Hardness, 184, 178, 160, 1f);
        Set(CultivationSemantics.Material.Immoveable, 116, 86, 59, 1f);
        Set(CultivationSemantics.Material.Lightweight, 189, 231, 214, 1f);
        Set(CultivationSemantics.Material.Stability, 155, 117, 79, 1f);
        Set(CultivationSemantics.Material.Volatility, 232, 91, 35, 1f);

        Set(CultivationSemantics.Resource.Reserve, 97, 199, 209, 1f);
        Set(CultivationSemantics.Resource.Spirituality, 122, 221, 224, 1f);
        Set(CultivationSemantics.Resource.Vitality, 105, 201, 106, 1f);

        Set(CultivationSemantics.Theme.Dragon, 63, 184, 140, 1f);
        Set(CultivationSemantics.Theme.Illusion, 112, 69, 146, 1f);
        Set(CultivationSemantics.Theme.Soul, 82, 34, 120, 1f);
        Set(CultivationSemantics.Theme.Sound, 232, 215, 97, 1f);
        Set(CultivationSemantics.Theme.Space, 73, 53, 143, 1f);
        Set(CultivationSemantics.Theme.Spirit, 79, 182, 198, 1f);
    }

    private static void Set(SemanticAsset semantic, byte red, byte green, byte blue, float salience)
    {
        semantic.visual_color = new Color32(red, green, blue, 255);
        semantic.color_salience = salience;
    }
}
