using System;
using System.Collections.Generic;
using System.Linq;

namespace Cultiway.Core.SkillLibV3.Effects;

/// <summary>技能容器构建完成后冻结的结构化效果集合。</summary>
public sealed class SkillEffectPipeline
{
    public static readonly SkillEffectPipeline Empty = new(Array.Empty<SkillEffectDescriptor>());

    private readonly SkillEffectDescriptor[] effects;

    public IReadOnlyList<SkillEffectDescriptor> Effects => effects;
    public bool HasObjectEffects { get; }
    public bool HasTileEffects { get; }
    public bool HasPeriodicEffects { get; }
    public float MinimumPeriodicInterval { get; }

    public SkillEffectPipeline(IEnumerable<SkillEffectDescriptor> source)
    {
        effects = source?
            .Where(effect => effect != null && (effect.IsObjectEffect || effect.IsTileEffect))
            .Distinct()
            .ToArray() ?? Array.Empty<SkillEffectDescriptor>();
        HasObjectEffects = effects.Any(effect => effect.IsObjectEffect);
        HasTileEffects = effects.Any(effect => effect.IsTileEffect);
        HasPeriodicEffects = effects.Any(effect => effect.IsPeriodic);
        MinimumPeriodicInterval = effects
            .Where(effect => effect.IsPeriodic)
            .Select(effect => Math.Max(0.05f, effect.Interval))
            .DefaultIfEmpty(0f)
            .Min();
    }
}
