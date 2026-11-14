using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Cultiway.Core.Skills;

/// <summary>真实身体触地后的传播仍是普通主体及操作，创作与玩家改造共同使用。</summary>
public static partial class BodyConstructionEdits
{
    /// <summary>保留实际身体动作，在首次真实触地位置启动独立的地表过程。</summary>
    /// <param name="source">尚未生成独立主体的真实动作。</param>
    /// <param name="settings">传播路程、波厚及有限预算。</param>
    /// <param name="resource">本次整招的实际支付通道。</param>
    /// <param name="form">使用其材质及共同形体入口的资源。</param>
    /// <param name="matter">实际材料。</param>
    /// <param name="surface">明确外观，空值采用材料表面。</param>
    public static AbilityDefinition Propagate(AbilityDefinition source, ConstructionPropagation settings,
        string resource = "wakan", string form = "sword", string matter = "physical", string surface = "stone")
    {
        if (source?.Body == null || source.Root != null || settings == null)
            throw new InvalidDataException("触地传播需要尚未生成主体的真实身体动作和明确传播参数");
        CompiledAbility original = AbilityCompiler.Compile(source, cache: false);
        if (!original.Body.Construction.MayContact) throw new InvalidDataException("该动作没有实际接触部位");
        var propagation = JsonConvert.DeserializeObject<ConstructionPropagation>(JsonConvert.SerializeObject(settings));
        _ = new CompiledPropagation(propagation, "触地传播");
        AbilityDefinition result = ConstructionEditing.Copy(source);
        result.Resource = resource;
        result.TargetRange = null;
        result.Body.MatchDistance = false;
        result.Lifetime = Math.Max(result.Lifetime,
            result.Body.Keys.Last().At + propagation.Range / propagation.Speed + propagation.Seconds);
        result.Root = new ConstructionNode
        {
            Id = "ground", Form = form, Matter = matter, Surface = surface,
            Activation = new ConstructionActivation
            {
                Source = "body", Trigger = ConstructionChangeTrigger.WorldContact,
                Placement = ConstructionEventPlacement.Event
            },
            Operations = new[]
            {
                new ConstructionOperation { Kind = ConstructionOperationKind.Propagate, Propagation = propagation }
            }
        };
        return ConstructionPricing.Price(result);
    }
}
