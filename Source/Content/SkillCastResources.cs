using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.SkillLibV3;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>
/// Content 提供的施法资源通道。Core 只消费注册后的抽象资源接口。
/// </summary>
public sealed class SkillCastResources : ExtendLibrary<SkillCastResourceAsset, SkillCastResources>
{
    public static SkillCastResourceAsset Wakan { get; private set; }

    protected override bool AutoRegisterAssets() => true;
    protected override string Prefix() => "Cultiway.SkillCastResource";

    protected override void OnInit()
    {
        Wakan.Configure(HasWakan, ReadWakan, WriteWakan, QuoteWakan);
    }

    private static bool HasWakan(ActorExtend caster)
    {
        return caster.HasCultisys<Xian>();
    }

    private static float ReadWakan(ActorExtend caster)
    {
        return caster.GetCultisys<Xian>().wakan;
    }

    private static void WriteWakan(ActorExtend caster, float amount)
    {
        caster.GetCultisys<Xian>().wakan = Mathf.Max(0f, amount);
    }

    private static float QuoteWakan(ActorExtend caster, Entity skill, float demand)
    {
        return demand;
    }
}
