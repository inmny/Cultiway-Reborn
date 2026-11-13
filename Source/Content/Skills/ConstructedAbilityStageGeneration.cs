using System;
using System.Linq;
using Cultiway.Core.Skills;
using Newtonsoft.Json;

namespace Cultiway.Content.Skills;

/// <summary>修士创法在同一真实动作内选择有限作用，保存明确窗口而非完整招式配方。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>先固定次数和窗口，再选择后段强度；成功接触不提前结束，失去支撑及中断仍按原规则收势。</summary>
    /// <param name="source">受理时固定的完整构造。</param><param name="random">本次固定的随机源。</param>
    private static AbilityDefinition GenerateBodyStages(AbilityDefinition source, Random random)
    {
        AbilityDefinition copy = ConstructionEditing.Copy(source);
        int count = random.Next(2, 5);
        float from = copy.Body.ActiveFrom, span = copy.Body.ActiveUntil - from;
        float interval = span / (count - .35f), width = interval * .65f;
        ConstructionImpact impact = JsonConvert.DeserializeObject<ConstructionImpact>(JsonConvert.SerializeObject(
            copy.Body.Impact ?? copy.Impact ?? new ConstructionImpact()));
        impact.DamageScale *= .75f + (float)random.NextDouble() * .5f;
        // 真实结果收势会终止整招；这里明确选择继续后段作用，不改变其他结果的控制归还。
        copy.Body.Responses = copy.Body.Responses?.Where(rule =>
            rule.Result is not (BodyActionResult.Contact or BodyActionResult.Blocked)).ToArray();
        return BodyConstructionEdits.Stages(copy, new[]
        {
            new BodyActionStage { Id = "stage.1", From = from, Until = from + width },
            new BodyActionStage { Id = "stage.2", From = from + interval, Until = from + interval + width,
                Pulses = count - 1, Interval = interval, Impact = impact }
        }, reprice: false);
    }
}
