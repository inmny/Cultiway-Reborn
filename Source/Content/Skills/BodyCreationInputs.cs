using System;
using System.Linq;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Persistence;
using Cultiway.Core.Skills;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Skills;

/// <summary>在机会受理时固定实际部位、完整动作和支付通道，等待及读档后不重新取身体。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>模型数值继续共享采集，人物只独立保存本次已选的完整身体动作。</summary>
    /// <param name="actor">机会受理时的真实创造者。</param><param name="seed">本次固定种子。</param>
    /// <param name="inspiration">已经固定的生成规则与倾向。</param>
    private static SynthesisResources CaptureCreationResources(ActorExtend actor, int seed, ConstructionInspiration inspiration)
    {
        SynthesisResources common = CaptureSynthesisResources(true);
        if (inspiration.Rules < 23)
            common = new SynthesisResources(common.Forms.Where(form => SynthesisResources.RequiredForms.Contains(form.Id) || form.Id == "energy_ray").ToArray(), common.Bodies, common.Resource);
        if (inspiration.Rules < 10)
            common = new SynthesisResources(common.Forms.Where(form => SynthesisResources.RequiredForms.Contains(form.Id)).ToArray(), common.Bodies, common.Resource);
        if (inspiration.Rules <= 6) return common;
        var forms = inspiration.Rules >= 30 ? common.Forms.Append(CaptureSynthesisForm("fireball")).ToArray() : common.Forms;
        SynthesisWorldAction[] worldActions = inspiration.Rules >= 17
            ? ConstructionWorldActions.All.OrderBy(action => action.Id, StringComparer.Ordinal)
                .Select(action => new SynthesisWorldAction(action.Id, action.DefaultDuration)).ToArray()
            : null;
        var materials = inspiration.Rules >= 26 ? CaptureInventoryMaterials(actor) : null;
        var erosion = inspiration.Rules >= 28 ? AbilityMatters.All.Where(matter => matter.ErosionFactor > 0)
            .OrderBy(matter => matter.Id, StringComparer.Ordinal).ThenBy(matter => matter.State, StringComparer.Ordinal)
            .Select(matter => new SynthesisErosionMaterial(matter.Id, matter.State)).ToArray() : null;
        var choices = BodyPartChoices(actor);
        int palms = inspiration.CanProject ? common.Bodies.Length : 0;
        var random = new Random(unchecked(seed ^ 0x431f29b));
        string resource = actor.HasCultisys<Yao>() && !actor.HasCultisys<Xian>() ? "yao_power" : "wakan";
        if (inspiration.Rules >= 8 && random.Next(3) == 0)
        {
            // 从实际能接管的腿链生成触地动作；接管失败继续使用已有真实部位选择。
            AbilityDefinition foot = CaptureGroundBody(actor, random);
            if (foot != null) return new SynthesisResources(forms, new[] { foot }, resource, worldActions,
                inspiration.Rules >= 24 ? common.DeformationForms : null, materials, erosion);
        }
        AbilityDefinition[] bodies = Array.Empty<AbilityDefinition>();
        if (palms + choices.Effectors.Length > 0)
        {
            int selected = random.Next(palms + choices.Effectors.Length);
            AbilityDefinition body;
            if (selected < palms) body = common.Bodies[selected];
            else
            {
                string part = choices.Effectors[selected - palms];
                body = BodyActionCatalog.Create("Cultiway.Creation.BodyInput", "实际身体发力", new BodyActionRequest
                {
                    Effector = part, Kind = (BodyActionKind)random.Next((int)BodyActionKind.Shock + 1),
                    Seconds = .9f + (float)random.NextDouble() * .4f, Arc = 35f + (float)random.NextDouble() * 40f,
                    Amplitude = .8f + (float)random.NextDouble() * .4f, Strength = .8f + (float)random.NextDouble() * .4f,
                    Plane = (float)random.NextDouble() * 20f - 10f,
                    OrganAxis = part.StartsWith("part:", StringComparison.Ordinal) ? new[] { -1f, 0f, 0f } : null
                });
            }
            body.Resource = "stamina";
            bodies = new[] { body };
        }
        return new SynthesisResources(forms, bodies, resource, worldActions,
            inspiration.Rules >= 24 ? common.DeformationForms : null, materials, erosion);
    }
}

/// <summary>旧请求保留原生成规则和两份完整掌法，仅补记原本使用的灵力通道。</summary>
internal sealed class CreationV8ToV9 : ISaveMigration
{
    /// <summary>已有三轴身体动作快照的请求版本。</summary>
    public int FromVersion => 8;
    /// <summary>不从读档后的身体或技能集合补选输入。</summary>
    /// <param name="data">保存层提供的独立旧资料。</param>
    public JObject Apply(JObject data)
    {
        if (data["Resources"] is JObject resources) resources["Resource"] = "wakan";
        return data;
    }
}
