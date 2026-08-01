using System.Collections.Generic;
using Cultiway.Core.Semantics;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Utils;
using Friflo.Engine.ECS;

namespace Cultiway.Content;

/// <summary>集中声明魔法师自治研究与自治使用不能跨越的玩家控制边界。</summary>
internal static class MagicAutonomyRules
{
    /// <summary>注水会无条件制造水域，首版只允许玩家明确授予或控制，不进入自治学习。</summary>
    public static bool IsAutonomousStudyCandidate(Entity skill)
    {
        if (skill.IsNull || !skill.HasComponent<SkillContainer>()) return false;
        SkillContainer container = skill.GetComponent<SkillContainer>();
        var semantics = new HashSet<SemanticAsset>();
        SkillSemanticCollector.CollectAssetSemantics(
            container.Asset,
            semantics);
        SkillSemanticCollector.CollectModifierSemantics(skill, semantics);
        SkillSemanticCollector.CollectTrajectorySemantics(
            container.Asset,
            skill,
            semantics);
        return !semantics.Contains(SkillSemantics.Effect.FillWater);
    }
}
