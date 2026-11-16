using System.Collections.Generic;
using System.Threading;
using System.Linq;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Skills;
using Cultiway.Core;
using Cultiway.Core.Combat;
using Cultiway.Core.EventSystem.Events;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>以固定环境来源驱动煞风劫，所有风柱及劫风使用共同构造执行。</summary>
internal static class BalefulWindTribulationSkillService
{
    private const int TotalWaves = 9;

    private const float InitialDelay = 1f;
    private const float EmissionInterval = 0.25f;
    private const float WaveSpawnRadius = 10f;
    private const float BaseDamageRatio = 0.15f;
    private const float DamageGrowthPerWave = 0.01f;

    private static readonly Dictionary<long, TribulationSequenceSession> sessionsByActor = new();
    /// <summary>由实际施放编号找到劫程，接触与结束事件不借旧实体转发。</summary>
    private static readonly Dictionary<long, TribulationSequenceSession> sessionsByCast = new();
    private static readonly List<TribulationSequenceSession> sessionsToUpdate = new();
    /// <summary>共同接触和结束事件只登记一次。</summary>
    private static bool constructionHooksRegistered;
    private static long nextCorrelationId;
    private static bool deathHookRegistered;
    private static bool worldClearHookRegistered;

    public static void Initialize()
    {
        if (!constructionHooksRegistered)
        {
            ConstructedAbilityService.ContactConfirmed += OnContact;
            ConstructedAbilityService.CastEnded += OnEnded;
            constructionHooksRegistered = true;
        }
        if (!deathHookRegistered)
        {
            ActorExtend.RegisterActionOnDeath(OnActorDeath);
            deathHookRegistered = true;
        }
        if (worldClearHookRegistered) return;

        Cultiway.Patch.PatchMapBox.RegisterActionOnClearWorld(ClearWorldState);
        worldClearHookRegistered = true;
    }

    /// <summary>为人物开始一场新的煞风劫。</summary>
    /// <param name="actor">接受劫风的人物。</param>
    public static bool Start(ActorExtend actor) => Start(actor, null);

    /// <summary>开始新劫或从已有承伤结果继续剩余波次。</summary>
    /// <param name="actor">当前人物。</param>
    /// <param name="saved">当前操作使用的资料。</param>
    private static bool Start(ActorExtend actor, SaveRecord saved)
    {
        if (actor?.Base == null || actor.Base.isRekt()) return false;
        Initialize();
        if (sessionsByActor.ContainsKey(actor.Base.data.id)) return false;

        long correlationId = Interlocked.Increment(ref nextCorrelationId);
        var session = new TribulationSequenceSession(
            actor.Base,
            saved?.Anchor ?? (Vector3)actor.Base.current_position,
            correlationId);
        if (saved != null) session.Restore(saved);
        if (saved?.Completed == true)
        {
            sessionsByActor.Add(actor.Base.data.id, session);
            return true;
        }
        if (!IsEligibleBody(actor.Base)) return false;
        sessionsByActor.Add(actor.Base.data.id, session);
        session.PrepareWinds();
        if (session.SpawnCenter())
        {
            session.PinActor();
            return true;
        }
        session.Close();
        return false;
    }

    public static bool IsInProgress(ActorExtend actor)
    {
        return TryGetSession(actor, out TribulationSequenceSession session) && session.IsInProgress;
    }

    public static bool IsPassed(ActorExtend actor)
    {
        return TryGetSession(actor, out TribulationSequenceSession session) && session.IsPassed;
    }

    public static bool TryGetProgress(ActorExtend actor, out int wavesSurvived, out int totalWaves)
    {
        totalWaves = TotalWaves;
        if (!TryGetSession(actor, out TribulationSequenceSession session))
        {
            wavesSurvived = 0;
            return false;
        }

        wavesSurvived = session.WavesSurvived;
        return true;
    }

    /// <summary>保存已经真实承受的波次，飞行中的劫风读取后重新发出。</summary>
    internal static WorldSaveState CaptureWorldSave() => new()
    {
        Sessions = new List<SaveRecord>(sessionsByActor.Values.Select(session => session.Capture()))
    };

    /// <summary>恢复成功证据或继续未完成波次，不重复已承受的伤害。</summary>
    /// <param name="data">待保存或恢复的资料。</param>
    internal static void RestoreWorldSave(WorldSaveState data)
    {
        foreach (SaveRecord record in data.Sessions)
        {
            Actor actor = World.world.units.get(record.Actor);
            if (actor == null || record.Resolved?.Length != TotalWaves + 1 || record.Damage?.Length != TotalWaves + 1)
            {
                ModClass.I.WorldSaves.LastReport.Skip("煞风劫", record.Actor.ToString(), "人物或波次资料无效");
                continue;
            }
            Start(actor.GetExtend(), record);
        }
    }

    /// <summary>煞风劫世界记录。</summary>
    internal sealed class WorldSaveState
    {
        /// <summary>各人物真实劫程。</summary>
        public List<SaveRecord> Sessions = new();
    }

    /// <summary>单个人物的劫程资料。</summary>
    internal sealed class SaveRecord
    {
        /// <summary>人物编号。</summary>
        public long Actor;
        /// <summary>固定劫程位置。</summary>
        public Vector3 Anchor;
        /// <summary>各波已经结算的劫风数量。</summary>
        public int[] Resolved;
        /// <summary>各波冻结的单次劫伤。</summary>
        public float[] Damage;
        /// <summary>已通过波数。</summary>
        public int Survived;
        /// <summary>是否已完全通过且尚待突破流程领取。</summary>
        public bool Completed;
    }

    public static void UpdateAll()
    {
        sessionsToUpdate.Clear();
        foreach (TribulationSequenceSession session in sessionsByActor.Values)
        {
            if (session.IsInProgress) sessionsToUpdate.Add(session);
        }
        for (int i = 0; i < sessionsToUpdate.Count; i++)
        {
            sessionsToUpdate[i].Update();
        }
    }

    /// <summary>只有共同接触已经提交实际伤害后才登记该股劫风的结果。</summary>
    /// <param name="contact">实际接触事实。</param>
    private static void OnContact(ConstructedContact contact)
    {
        if (sessionsByCast.TryGetValue(contact.CastId, out TribulationSequenceSession session))
            session.ResolveImpact(contact);
    }

    /// <summary>未接触就结束的劫风判为失败；风柱正常到期仍可按原规则重新生成。</summary>
    /// <param name="ended">已经退出共同执行的施放。</param>
    private static void OnEnded(ConstructedCastEnded ended)
    {
        if (!sessionsByCast.TryGetValue(ended.CastId, out TribulationSequenceSession session)) return;
        sessionsByCast.Remove(ended.CastId);
        session.OnEnded(ended.CastId);
    }

    public static void Fail(Actor actor)
    {
        if (actor == null || !sessionsByActor.TryGetValue(
                actor.data.id,
                out TribulationSequenceSession session)) return;

        bool shouldLog = session.IsInProgress;
        session.Close();
        if (shouldLog) WorldLogUtils.LogBalefulWindTribulationFailed(actor);
    }

    public static void Cleanup(ActorExtend actor)
    {
        if (actor?.Base == null) return;
        Cleanup(actor.Base.data.id);
    }

    public static void Cleanup(long actorId)
    {
        if (sessionsByActor.TryGetValue(actorId, out TribulationSequenceSession session))
            session.Close();
    }

    private static bool TryGetSession(ActorExtend actor, out TribulationSequenceSession session)
    {
        session = null;
        return actor?.Base != null && sessionsByActor.TryGetValue(actor.Base.data.id, out session);
    }

    private static void ClearWorldState()
    {
        sessionsToUpdate.Clear();
        sessionsByCast.Clear();
        sessionsByActor.Clear();
    }

    private static void OnActorDeath(ActorExtend actor)
    {
        if (actor?.Base != null) Fail(actor.Base);
    }

    private static bool IsEligibleBody(Actor actor)
    {
        if (actor == null || actor.isRekt() || !actor.isAlive() || actor.data.health <= 0) return false;
        ActorExtend actorExtend = actor.GetExtend();
        return !actorExtend.HasComponent<YuanyingSoulState>()
               && actorExtend.TryGetComponent(out Xian xian)
               && xian.CurrLevel == XianLevels.Yuanying
               && actorExtend.TryGetComponent(out Yuanying yuanying)
               && yuanying.stage >= Cultisyses.MaximumYuanyingStage;
    }

    private static float GetHuashenPowerLevel()
    {
        return Cultisyses.Xian.GetLevelPower(XianLevels.Huashen);
    }

    private static void ShowCompletion(Actor actor)
    {
        if (!MapBox.isRenderGameplay() || !actor.is_visible) return;
        float scale = Mathf.Max(0.35f, actor.actor_scale);
        EffectsLibrary.spawnAt("fx_teleport_singularity", actor.current_position, scale * 0.9f);
        EffectsLibrary.spawnAt("fx_cast_ground_blue", actor.current_position, scale * 1.4f);
    }

    private sealed class TribulationSequenceSession
    {
        private readonly Actor actor;
        private readonly Vector3 anchor;
        private readonly long correlationId;
        /// <summary>每股尚未承受的实际构造编号及所属波次。</summary>
        private readonly Dictionary<long, int> pendingWinds = new();
        private readonly int[] resolvedByWave = new int[TotalWaves + 1];
        private readonly float[] damagePerWind = new float[TotalWaves + 1];
        /// <summary>当前中央风柱编号，零表示尚未生成或已经退出。</summary>
        private long center;
        /// <summary>下股劫风的实际世界发射时间。</summary>
        private double nextEmissionAt;
        /// <summary>本次实际需要重新发出的剩余劫风所属波次。</summary>
        private readonly List<(int Wave, Vector3 Source)> plannedWaves = new();
        private int assignedEmissionCount;
        private int wavesSurvived;
        private bool emissionsEnded;
        private bool aborted;
        private bool completed;

        public bool IsInProgress => !aborted && !completed;
        public bool IsPassed => completed;
        public int WavesSurvived => wavesSurvived;

        public TribulationSequenceSession(Actor actor, Vector3 anchor, long correlationId)
        {
            this.actor = actor;
            this.anchor = anchor;
            this.correlationId = correlationId;
        }

        /// <summary>采集结果数据，不保存技能执行实体。</summary>
        public SaveRecord Capture() => new()
        {
            Actor = actor.data.id, Anchor = anchor, Resolved = (int[])resolvedByWave.Clone(),
            Damage = (float[])damagePerWind.Clone(), Survived = wavesSurvived, Completed = completed
        };

        /// <summary>从实际已结算结果继续剩余波次。</summary>
        /// <param name="record">当前对象的存档记录。</param>
        public void Restore(SaveRecord record)
        {
            record.Resolved.CopyTo(resolvedByWave, 0);
            record.Damage.CopyTo(damagePerWind, 0);
            wavesSurvived = record.Survived;
            completed = record.Completed;
        }

        /// <summary>只安排尚未结算的劫风，保持原出生角度及逐股间隔。</summary>
        public void PrepareWinds()
        {
            nextEmissionAt = World.world.getCurWorldTime() + InitialDelay;
            int actorAngle = (int)(actor.data.id % 360L);
            for (int wave = 1; wave <= TotalWaves; wave++)
            {
                float angleOffset = (actorAngle + wave * 137.5f) * Mathf.Deg2Rad;
                float angleStep = Mathf.PI * 2f / wave;
                for (int index = resolvedByWave[wave]; index < wave; index++)
                {
                    float angle = angleOffset + index * angleStep;
                    plannedWaves.Add((wave, anchor + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * WaveSpawnRadius));
                }
            }
            emissionsEnded = plannedWaves.Count == 0;
        }

        /// <summary>同一更新至多发出一股；每波伤害在该波第一次发出时按真实最大生命确定。</summary>
        private void EmitNext()
        {
            if (emissionsEnded || World.world.getCurWorldTime() < nextEmissionAt) return;
            var planned = plannedWaves[assignedEmissionCount];
            int wave = planned.Wave;
            if (damagePerWind[wave] <= 0f)
            {
                float ratio = BaseDamageRatio + (wave - 1) * DamageGrowthPerWave;
                damagePerWind[wave] = Mathf.Max(1f, actor.getMaxHealth() * ratio) / wave;
            }
            var target = new ActiveAbilityTarget(actor, anchor,
                runtimeData: new Core.SkillLibV3.Components.SkillCastRuntimeData { CorrelationId = correlationId });
            long wind = ConstructedAbilityService.StartWorld(BalefulWindConstruction.Wave((anchor - planned.Source).normalized), target,
                planned.Source, Damage(damagePerWind[wave]));
            if (wind == 0) { Fail(actor); return; }
            pendingWinds.Add(wind, wave);
            sessionsByCast.Add(wind, this);
            assignedEmissionCount++;
            nextEmissionAt += EmissionInterval;
            emissionsEnded = assignedEmissionCount == plannedWaves.Count;
        }

        /// <summary>真实命中只推进本股一次；人物已经死亡或失去渡劫资格时不能领取成功。</summary>
        /// <param name="contact">已经完成的共同伤害结算。</param>
        public void ResolveImpact(ConstructedContact contact)
        {
            if (!pendingWinds.TryGetValue(contact.CastId, out int wave)) return;
            if (contact.TargetId != actor.id || !contact.DamageAttempted || !IsActorInProgress())
            {
                Fail(actor);
                return;
            }
            pendingWinds.Remove(contact.CastId);
            sessionsByCast.Remove(contact.CastId);
            resolvedByWave[wave]++;
            AdvanceWaves();
            TryComplete();
        }

        /// <summary>风柱自然结束后重新生成；未能真实命中的劫风结束会中止当前劫程。</summary>
        /// <param name="castId">已经退出的真实构造编号。</param>
        public void OnEnded(long castId)
        {
            if (castId == center) center = 0;
            else if (pendingWinds.ContainsKey(castId)) Fail(actor);
        }

        /// <summary>环境伤害不绑定攻击人物，保留化神境界和完整混合元素。</summary>
        /// <param name="amount">该波冻结的单股伤害。</param>
        private static GetHitEvent Damage(float amount) => new()
        {
            Damage = amount, Element = BalefulWindConstruction.Element,
            AttackType = AttackType.Other, AttackerPowerLevel = GetHuashenPowerLevel(),
            DamageOrigin = DamageOrigin.Primary
        };

        public void PinActor()
        {
            if (aborted || completed || !IsActorInProgress()) return;
            actor.stopMovement();
            actor.current_position = new Vector2(anchor.x, anchor.y);
            actor.dirty_current_tile = true;
            actor.velocity = Vector3.zero;
            actor.velocity_speed = 0f;
            actor.under_forces = false;
        }

        public void Update()
        {
            if (aborted || completed) return;
            if (!IsActorInProgress())
            {
                Fail(actor);
                return;
            }
            PinActor();
            if (center == 0 && !SpawnCenter()) { Fail(actor); return; }
            EmitNext();
            TryComplete();
        }

        public void Close()
        {
            if (aborted) return;
            aborted = true;
            Cancel(center);
            foreach (long wind in pendingWinds.Keys) Cancel(wind);
            pendingWinds.Clear();
            Unregister();
        }

        /// <summary>固定地点直接生成普通无伤风柱，不创建旧容器或回调占位实体。</summary>
        public bool SpawnCenter()
        {
            var target = new ActiveAbilityTarget(null, anchor,
                runtimeData: new Core.SkillLibV3.Components.SkillCastRuntimeData { CorrelationId = correlationId });
            center = ConstructedAbilityService.StartWorld(BalefulWindConstruction.Center, target, anchor, Damage(0f));
            if (center != 0) sessionsByCast.Add(center, this);
            return center != 0;
        }

        private void AdvanceWaves()
        {
            while (wavesSurvived < TotalWaves &&
                   resolvedByWave[wavesSurvived + 1] >= wavesSurvived + 1)
            {
                wavesSurvived++;
            }
        }

        private void TryComplete()
        {
            if (aborted || completed || !emissionsEnded || pendingWinds.Count > 0 ||
                wavesSurvived < TotalWaves) return;

            completed = true;
            Cancel(center);
            WorldLogUtils.LogBalefulWindTribulationSurvived(actor);
            ShowCompletion(actor);
        }

        private bool IsActorInProgress()
        {
            return !aborted && !completed && IsEligibleBody(actor);
        }

        private void Unregister()
        {
            if (sessionsByActor.TryGetValue(actor.data.id, out TribulationSequenceSession actorSession) &&
                ReferenceEquals(actorSession, this))
                sessionsByActor.Remove(actor.data.id);

        }

        /// <summary>先解除劫程关联再取消；共同结束事件不能把主动清理误当失败。</summary>
        /// <param name="castId">本劫程保存的真实施放编号。</param>
        private static void Cancel(long castId)
        {
            sessionsByCast.Remove(castId);
            if (castId != 0) ConstructedAbilityService.Cancel(castId);
        }
    }
}
