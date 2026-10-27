#if S3COND2_WORLDFALL
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using FirstPerson.Core;
using UnityEngine;

namespace Cultiway.Content.Worldfall;

/// <summary>为停稳的树人与各类泰坦选择收拢后的伪装外观。</summary>
public sealed partial class WorldfallModelIntegration
{
    /// <summary>短暂停步不立即切换外形，避免走动时反复闪动。</summary>
    private const float CamouflageSettleSeconds = 0.4f;

    /// <summary>具有独立树木、岩石、火山或冰块伪装模型的单位。</summary>
    private static readonly HashSet<string> CamouflageActors = new(StringComparer.Ordinal)
    {
        "Cultiway.AcaciaTreants", "Cultiway.BanyanTreants", "Cultiway.CoconutTreants",
        "Cultiway.OakTreants", "Cultiway.SycamoreTreants", "Cultiway.DeathTreants",
        "Cultiway.FireTreants", "Cultiway.RockGiant", "Cultiway.LavaGiant", "Cultiway.IceGiant",
        "Cultiway.VolcanicGiant"
    };

    /// <summary>跟随单位寿命保存本次停稳的开始时刻。</summary>
    private static readonly ConditionalWeakTable<Actor, CamouflageState> CamouflageStates = new();

    /// <summary>一个单位本次进入静止状态的显示记录。</summary>
    private sealed class CamouflageState
    {
        /// <summary>供弱引用表创建记录。</summary>
        public CamouflageState() { }
        /// <summary>上次移动、实际出手或离地的真实时间。</summary>
        private float LastActive;
        /// <summary>上次看到的出手记录，世界暂停后不会被同一次攻击反复刷新。</summary>
        private double LastAttack;
        /// <summary>首次看到单位时先保留活动外观。</summary>
        private bool Observed;

        /// <summary>活动立即解除伪装，首次出现或刚停步时等待片刻。</summary>
        /// <param name="active">是否仍在移动、入水或离地。</param>
        /// <param name="now">当前真实时间。</param>
        /// <param name="lastAttack">游戏记录的最近一次出手时间。</param>
        /// <returns>是否已停稳到可以伪装。</returns>
        public bool UseDisguise(bool active, float now, double lastAttack)
        {
            bool attacked = Observed && lastAttack != LastAttack;
            LastAttack = lastAttack;
            if (!Observed || active || attacked)
            {
                Observed = true;
                LastActive = now;
                return false;
            }
            return now - LastActive >= CamouflageSettleSeconds;
        }
    }

    /// <summary>活动时立即恢复原外观，停稳后选择同尺度的伪装模型族。</summary>
    /// <param name="actor">实际单位。</param>
    /// <param name="family">当前活动模型族。</param>
    /// <returns>本次绘制使用的活动或伪装模型族。</returns>
    private static string CamouflagedCreatureFamily(Actor actor, string family)
    {
        if (!CamouflageActors.Contains(actor.asset.id)) return family;
        CamouflageState state = CamouflageStates.GetOrCreateValue(actor);
        // 选中攻击目标不代表已经出手；默认的零时间戳也不能当作刚攻击过。
        bool active = actor.is_moving || actor._is_in_liquid || actor.isFlying() ||
            actor.is_in_magnet || !actor.isAlive();
        string disguised = family + "_disguised";
        return state.UseDisguise(active, Time.unscaledTime, actor.last_attack_timestamp) && CustomModels.ContainsKey(disguised)
            ? disguised : family;
    }

    /// <summary>伪装网格已经烘焙收臂、埋根或蹲伏姿态，不再叠加行走和呼吸。</summary>
    /// <param name="mesh">当前绘制实例。</param>
    /// <returns>是否采用固定的伪装姿态。</returns>
    private static bool PoseCamouflagedCreature(MeshInstance mesh)
    {
        if (mesh.Model.Info("cultiway_camouflage_state") != "disguised") return false;
        mesh.ResetPose();
        return true;
    }
}
#endif
