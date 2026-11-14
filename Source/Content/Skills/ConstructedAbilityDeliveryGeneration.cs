using System;
using System.Linq;
using Cultiway.Core.Persistence;
using Cultiway.Core.Skills;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Skills;

/// <summary>实际投送只改普通参数，射线和其他部位继续同一套构造。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>改变已有射线的长度、转向或目标更新，不重建整招和命中组。</summary>
    /// <param name="draft">独立原术副本。</param><param name="random">已固定的随机源。</param>
    private static AbilityDefinition VaryRay(AbilityDefinition draft, Random random)
    {
        var rays = DerivationNodes(draft.Root).SelectMany(node => node.Operations)
            .Concat(draft.Steps.Select(step => step.Operation)).Where(operation => operation.Ray != null).ToArray();
        ConstructionRay ray = rays[random.Next(rays.Length)].Ray;
        switch (random.Next(3))
        {
            case 0: ray.Length *= 1.1f + (float)random.NextDouble() * .35f; break;
            case 1: ray.TurnRate *= .7f + (float)random.NextDouble() * .6f; break;
            default: ray.FollowTarget = !ray.FollowTarget; break;
        }
        return draft;
    }
}

/// <summary>旧请求继续已固定的规则及模型输入，不追补后来新增的伸展模型。</summary>
internal sealed class CreationV10ToV11 : ISaveMigration
{
    /// <summary>只提供前九版生成规则的正式请求区段。</summary>
    public int FromVersion => 10;

    /// <summary>新伸展能力为可选字段；旧输入保持原样，由原规则完成。</summary>
    /// <param name="data">保存层提供的旧请求独立副本。</param>
    public JObject Apply(JObject data) => data;
}
