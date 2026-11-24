using Cultiway.Content.Components;
using Cultiway.Content.Visuals;
using Cultiway.Core;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Patch;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Combat;

/// <summary>把境界成长应用到同一真实作用范围，再让显示读取结果，避免大特效和小伤害范围脱节。</summary>
internal static class BattleSpellScale
{
    /// <summary>接入正式范围成长和换世界时的法相清理。</summary>
    internal static void Initialize()
    {
        SkillEffectRadius.CasterGrowth = ResolveContainerGrowth;
        PatchMapBox.RegisterActionOnClearWorld(BattleSpellVisualRuntime.Clear);
    }

    /// <summary>按实际体系境界分段成长，最高层再允许有限的战力差异。</summary>
    /// <param name="actor">真实资源所有者。</param>
    internal static float Resolve(ActorExtend actor)
    {
        float scale = 1f;
        if(actor.TryGetComponent(out Xian xian))
            scale=xian.level switch{1=>1.35f,2=>2.3f,3=>3.7f,>=4=>6.4f,_=>1f};
        if(actor.TryGetComponent(out Magic magic))scale=Mathf.Max(scale,Mathf.Lerp(1f,5.5f,Mathf.Clamp01(magic.level/8f)));
        if(actor.TryGetComponent(out Knight knight))scale=Mathf.Max(scale,Mathf.Lerp(1f,5.5f,Mathf.Clamp01(knight.level/9f)));
        if(scale>3f)scale*=1f+Mathf.Clamp01((actor.GetPowerLevel()-8f)/16f)*.2f;
        return scale;
    }

    /// <summary>容器词条与境界共用的唯一范围成长规则。</summary>
    /// <param name="owner">真实支付者。</param><param name="container">实际技能容器。</param>
    private static float ResolveContainerGrowth(ActorExtend owner,Entity container)
    {
        if(owner==null)return 1f;
        SkillEntityAsset asset=container.GetComponent<SkillContainer>().Asset;
        if(!OriginalSkillGrowthRules.ScaledSkills.Contains(asset.id) && asset!=SkillEntities.LightningStrike && asset!=SkillEntities.GroundSpike)return 1f;
        float growth=Resolve(owner);
        if(asset==SkillEntities.TidalWave || asset==SkillEntities.EarthRidge)
            growth*=Mathf.Lerp(1f,2f,Mathf.InverseLerp(1f,6.4f,growth));
        float basis=SkillEffectRadius.ResolveContainer(container,Mathf.Max(asset.ImpactProfile.EffectRadius*asset.ImpactTuning.EffectRadiusMultiplier,asset.ImpactProfile.CollisionRadius));
        return basis>0?Mathf.Max(1f,Mathf.Min(growth,32f/basis)):growth;
    }
}
