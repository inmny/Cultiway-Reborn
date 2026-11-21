using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Visuals;
using UnityEngine;

namespace Cultiway.Content.Skills;

public sealed partial class ConstructedAbilityService
{
    /// <summary>取原本在每次命中施加的元素作用，范围爆发和格挡已有各自入口。</summary>
    /// <param name="emission">原施放受理时的实际输入。</param>
    internal static SkillVfxElementAsset LegacyContactElement(in SkillEmission emission)
    {
        var skill = emission.SkillContainer;
        SkillEntityAsset asset = skill.GetComponent<SkillContainer>().Asset;
        if (emission.RuntimeData.WorldScope != null || asset.WorldVisualProfile?.SuppressDefaultGroundImpact == true ||
            asset.ImpactProfile.IsBarrier || asset.ImpactProfile.CanResolveAtPosition) return null;
        SkillVfxElementAsset element = ModClass.I.SkillV3.ResolveGroundEffectElement(skill, emission.RuntimeData);
        return element?.HasGroundImpact == true ? element : null;
    }

    /// <summary>原单体作用落在实际命中对象的脚下，并先于该对象的来源伤害执行。</summary>
    /// <param name="element">本次发射实际确定的元素。</param>
    /// <param name="foot">共同接触确认的人物脚下位置。</param>
    /// <param name="authority">接触时的实际攻击归属。</param>
    /// <param name="generation">接触所在的世界编号。</param>
    private static void ConfirmLegacyGroundContact(SkillVfxElementAsset element, Vector3 foot,
        CastAuthority authority, long generation)
    {
        SkillGroundFx.OnImpact(foot, element, 0f, false, authority.IsLive ? authority.Actor : null);
        if (generation != worldGeneration) return;
        // 单体风雷等旧规则也会改动邻近格，刷新覆盖其原有的固定范围。
        simulation.RefreshTerrainIn(new Rect(foot.x - 4f, foot.y - 4f, 8f, 8f));
    }
}
