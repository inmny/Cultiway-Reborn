using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Utils;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.YaoBeasts;

/// <summary>妖兽玩法的世界日志写入入口；每条日志都写清主体、原因和结果。</summary>
public static class YaoWorldLog
{
    /// <summary>凡兽启灵。</summary>
    public static void Awakened(ActorExtend actor, string speciesId)
    {
        Write(WorldLogs.LogYaoAwakened, actor, speciesId);
    }

    /// <summary>完成一次淬血。</summary>
    public static void QuenchedBlood(ActorExtend actor, int step)
    {
        Write(WorldLogs.LogYaoQuenchedBlood, actor, step.ToString());
    }

    /// <summary>器官炼化成功。</summary>
    public static void OrganDigested(ActorExtend actor, string organId)
    {
        Write(WorldLogs.LogYaoOrganDigested, actor, organId);
    }

    /// <summary>器官炼化失败并排异。</summary>
    public static void OrganRejected(ActorExtend actor, string organId)
    {
        Write(WorldLogs.LogYaoOrganRejected, actor, organId);
    }

    /// <summary>凝丹成功。</summary>
    public static void CoreCondensed(ActorExtend actor, string patternId, float quality)
    {
        Write(WorldLogs.LogYaoCoreCondensed, actor, YaoCoreService.GetPatternName(patternId));
    }

    /// <summary>妖丹出现裂痕。</summary>
    public static void CoreCracked(ActorExtend actor, int cracks)
    {
        Write(WorldLogs.LogYaoCoreCracked, actor, cracks.ToString());
    }

    /// <summary>渡劫开始。</summary>
    public static void TribulationStarted(ActorExtend actor)
    {
        Write(WorldLogs.LogYaoTribulationStarted, actor, string.Empty);
    }

    /// <summary>渡劫成功。</summary>
    public static void TribulationSucceeded(ActorExtend actor)
    {
        Write(WorldLogs.LogYaoTribulationSucceeded, actor, string.Empty);
    }

    /// <summary>超时退劫。</summary>
    public static void TribulationRetreated(ActorExtend actor)
    {
        Write(WorldLogs.LogYaoTribulationRetreated, actor, string.Empty);
    }

    /// <summary>妖丹受损裂开。</summary>
    public static void TribulationCracked(ActorExtend actor)
    {
        Write(WorldLogs.LogYaoCoreCracked, actor, "1");
    }

    /// <summary>化形成功。</summary>
    public static void HumanTransformation(ActorExtend actor)
    {
        Write(WorldLogs.LogYaoHumanTransformation, actor, string.Empty);
    }

    /// <summary>返祖完成。</summary>
    public static void AtavismCompleted(ActorExtend actor, YaoAtavismNode node, string organId)
    {
        Write(WorldLogs.LogYaoAtavism, actor, organId);
    }

    /// <summary>固血完成。</summary>
    public static void Solidified(ActorExtend actor, string organId)
    {
        Write(WorldLogs.LogYaoSolidified, actor, organId);
    }

    /// <summary>进入涅槃体阶段。</summary>
    public static void NirvanaStarted(ActorExtend actor)
    {
        Write(WorldLogs.LogYaoNirvanaStarted, actor, string.Empty);
    }

    /// <summary>涅槃重生成功。</summary>
    public static void NirvanaReborn(ActorExtend actor)
    {
        Write(WorldLogs.LogYaoNirvanaReborn, actor, string.Empty);
    }

    /// <summary>九尾以尾代命。</summary>
    public static void TailLifeSubstituted(ActorExtend actor)
    {
        Write(WorldLogs.LogYaoTailLife, actor, string.Empty);
    }

    /// <summary>后代出生遗传结果确定。</summary>
    public static void BirthResolved(ActorExtend child, string bloodlineId, bool awakenedOffspring)
    {
        Write(WorldLogs.LogYaoBirthResolved, child, bloodlineId ?? "Cultiway.Yao.Bloodline.none".Localize());
    }

    private static void Write(WorldLogAsset asset, ActorExtend actor, string value)
    {
        if (actor?.Base == null) return;
        var message = new WorldLogMessage(asset, actor.Base.getName(), value)
        {
            unit = actor.Base,
            location = actor.Base.current_position,
        };
        if (actor.Base.kingdom != null)
        {
            message.color_special1 = actor.Base.kingdom.getColor().getColorText();
        }
        message.add();
    }
}
