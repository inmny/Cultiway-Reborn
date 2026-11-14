using System;
using System.Linq;
using Cultiway.Core;
using Cultiway.Core.Skills;

namespace Cultiway.Content.Skills;

/// <summary>新创作规则组装实际落脚及普通传播参数，不登记整招执行类型。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>在受理时固定本次真实可用的足部动作，旧请求继续原选择。</summary>
    /// <param name="actor">真实创造者。</param><param name="random">本次确定随机源。</param>
    private static AbilityDefinition CaptureGroundBody(ActorExtend actor, Random random)
    {
        var body = BodyActionCatalog.Create("Cultiway.Creation.BodyInput", "实际足底发力", new BodyActionRequest
        {
            Effector = random.Next(2) == 0 ? "foot.left" : "foot.right", Kind = BodyActionKind.Guard,
            Seconds = .9f + (float)random.NextDouble() * .4f,
            Amplitude = .8f + (float)random.NextDouble() * .4f
        });
        body.Resource = "stamina";
        body.Body.MatchDistance = false;
        foreach (BodyMotionKey key in body.Body.Keys)
        {
            key.Foot = new[] { .1f, key.Foot[1], 0f };
            key.Torso = new[] { 0f, 0f, -.12f };
        }
        return BodyActionBinding.CanBind(actor, AbilityCompiler.Compile(body, cache: false).Body) ? body : null;
    }

    /// <summary>范围、方向、速度及波厚独立变化，支付仍由共同规模估算确定。</summary>
    /// <param name="body">已经固定的真实身体草稿。</param>
    /// <param name="random">本次确定随机源。</param><param name="resource">实际法力通道。</param>
    private static AbilityDefinition GenerateGroundPropagation(AbilityDefinition body, Random random, string resource)
    {
        float Unit() => (float)random.NextDouble();
        float range = random.Next(4) == 0 ? 48f + Unit() * 464f : 12f + Unit() * 20f;
        var settings = new ConstructionPropagation
        {
            Range = range, Speed = 8f + Unit() * 62f, Seconds = .3f + Unit() * .5f,
            Height = .6f + Unit() * 2.4f, InnerRadius = 1.5f,
            Angle = random.Next(2) == 0 ? 360f : 90f + Unit() * 180f,
            Attenuation = .002f + Unit() * .018f
        };
        return BodyConstructionEdits.Propagate(body, settings, resource);
    }

    /// <summary>已有传播构造可以继续改变参数，原术及已在运行的版本保持独立。</summary>
    /// <param name="draft">独立派生草稿。</param><param name="random">本次确定随机源。</param>
    private static AbilityDefinition VaryGroundPropagation(AbilityDefinition draft, Random random)
    {
        var choices = DerivationNodes(draft.Root).SelectMany(n => n.Operations)
            .Where(o => o.Kind == ConstructionOperationKind.Propagate).ToArray();
        var settings = choices[random.Next(choices.Length)].Propagation;
        settings.Range = Math.Min(4096f, settings.Range * (.75f + (float)random.NextDouble() * 1.5f));
        settings.Speed = Math.Min(10000f, settings.Speed * (.75f + (float)random.NextDouble()));
        draft.Lifetime = Math.Max(draft.Lifetime,
            (draft.Body?.Keys.Last().At ?? 0f) + settings.Range / settings.Speed + settings.Seconds);
        return draft;
    }
}
