using System;
using Cultiway.Core;

namespace Cultiway.Content;

/// <summary>处理鼠人击杀进化，并把长期编组与动员委托给 <see cref="SkavenPackService"/>。</summary>
public static class SkavenEvolution
{
    private const float EvolutionChancePerKill = 0.13f;

    /// <summary>每个鼠群的固定人数上限。</summary>
    public const int GroupSize = 13;

    /// <summary>每个鼠巢的固定群组数量。</summary>
    public const int GroupCount = 13;

    private static readonly ActorAsset[] Levels =
    [
        Actors.Skaven_LV1, Actors.Skaven_LV2, Actors.Skaven_LV3, Actors.Skaven_LV4, Actors.Skaven_LV5,
        Actors.Skaven_LV6, Actors.Skaven_LV7, Actors.Skaven_LV8, Actors.Skaven_LV9, Actors.Skaven_LV10,
        Actors.Skaven_LV11, Actors.Skaven_LV12, Actors.Skaven_LV13
    ];

    /// <summary>注册击杀进化与受袭动员事件。</summary>
    public static void Init()
    {
        ActorExtend.RegisterActionOnKill(OnKill);
        ActorExtend.RegisterActionOnBeAttacked(OnBeAttacked);
        SkavenPackService.Init();
    }

    /// <summary>判断角色资产是否属于任一鼠人进化等级。</summary>
    public static bool IsSkaven(Actor actor)
    {
        return actor != null && GetLevel(actor.asset) >= 0;
    }

    /// <summary>判断攻击者与防守方阵营是否真实敌对。</summary>
    public static bool IsHostile(BaseSimObject attacker, Kingdom defender)
    {
        return attacker != null &&
               attacker.kingdom != null &&
               defender != null &&
               defender.isEnemy(attacker.kingdom);
    }

    /// <summary>返回鼠人资产对应的零基进化等级；非鼠人返回 -1。</summary>
    public static int GetLevel(ActorAsset asset)
    {
        for (var i = 0; i < Levels.Length; i++)
        {
            if (Levels[i] == asset) return i;
        }
        return -1;
    }

    /// <summary>
    /// 仅供世界首次索引重建使用，遍历各等级资产维护的单位列表。
    /// 运行期编组、队长和战斗状态不得调用该方法。
    /// </summary>
    public static void ForEachSkaven(Action<Actor> action)
    {
        for (var i = 0; i < Levels.Length; i++)
        {
            var units = Levels[i].units;
            foreach (Actor unit in units) action(unit);
        }
    }

    /// <summary>把一次真实受袭写入所属小队，而不是重新扫描整个鼠群。</summary>
    private static void OnBeAttacked(ActorExtend victim, BaseSimObject attacker, float damage)
    {
        SkavenPackService.ReportThreat(victim.Base, attacker, damage);
    }

    /// <summary>按固定概率将击杀者原地进化一级，并通知编组重新评选队长。</summary>
    private static void OnKill(ActorExtend killer, Actor _, Kingdom __)
    {
        Actor actor = killer.Base;
        if (!actor.hasTrait(ActorTraits.SkavenEvolution.id) ||
            !Randy.randomChance(EvolutionChancePerKill))
            return;

        for (var i = 0; i < Levels.Length - 1; i++)
        {
            if (actor.asset != Levels[i]) continue;
            ActorAsset targetAsset = Levels[i + 1];
            Actor transformed = ActorTransformationService.TransformInPlace(actor, targetAsset);
            if (transformed != null && targetAsset.default_weapons is { Length: > 0 })
                transformed.createNewWeapon(targetAsset.default_weapons[0]);
            if (transformed != null) SkavenPackService.NotifyEvolution(transformed);
            return;
        }
    }
}
