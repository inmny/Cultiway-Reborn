using System;
using System.Collections.Generic;
using Cultiway.Core;
using Cultiway.Content.Skills;
using Cultiway.Core.Skills;
using Cultiway.Core.CollectiveProjects;
using Cultiway.Core.EventSystem.Events;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.CollectiveProjects;

/// <summary>
/// 把城市功能法术工程转换为角色寻位、真实施法、完成事件和世界状态验收。
/// </summary>
internal sealed class MagicUtilityProjectExecutor : ICollectiveProjectExecutor
{
    private const double MinimumExecutionTimeout = 30d;
    private const float CastRangeMargin = 0.2f;
    private const float VerificationSettleMargin = 0.25f;

    private static readonly object Sync = new();
    private static readonly Dictionary<long, PreparedCast> PreparedByActor = new();
    private static readonly Dictionary<long, PendingCast> PendingByActor = new();

    public string Id => CityMagicUtilityProjectIds.Executor;

    /// <summary>所有当前城市法术工程共用一个自然工作，具体工程保存在通用认领表中。</summary>
    public string ResolveRoutineJobId(in CollectiveProjectView project)
    {
        return ActorJobs.CollectiveProject.id;
    }

    /// <summary>检查角色当前是否能够以真实已学法术满足项目最低收益。</summary>
    public bool CanExecute(ActorExtend actor, in CollectiveProjectView project)
    {
        if (actor == null || actor.Base.isRekt() || HasLivePendingCast(actor.Base.getID())) return false;
        return TryResolveBestCast(actor, in project, out _, out _, out _, out _, out _);
    }

    /// <summary>按实际收益、单次资源需求和施法距离评价同一工程的候选执行者。</summary>
    public float ScoreExecutor(ActorExtend actor, in CollectiveProjectView project)
    {
        if (!TryResolveBestCast(actor, in project, out _, out WorldTile target, out _,
                out MagicUtilitySpellOption option, out float utility)) return float.MinValue;
        float distance = Vector2.Distance(actor.Base.current_position, target.posV);
        return utility * 10f - option.Demand - distance * 0.05f;
    }

    /// <summary>冻结本次认领使用的法术版本，并为原版移动行为设置合法施法站位。</summary>
    public bool TryPrepare(ActorExtend actor, in CollectiveProjectView project)
    {
        if (actor == null || actor.Base.isRekt() || HasLivePendingCast(actor.Base.getID())) return false;
        if (!TryResolveBestCast(actor, in project, out _, out WorldTile target,
                out CityMagicUtilityProjectPayload payload, out MagicUtilitySpellOption option,
                out float utility)) return false;
        if (!TryResolveCastTile(actor.Base, target, option, out WorldTile castTile)) return false;

        payload.SetPlan(option);
        payload.ExpectedUtility = utility;
        actor.Base.beh_tile_target = castTile;
        lock (Sync)
        {
            PreparedByActor[actor.Base.getID()] = new PreparedCast(
                project.ProjectId,
                option.Ability,
                option.Radius,
                castTile.tile_id);
        }

        return true;
    }

    /// <summary>
    /// 到达站位后再次校验法术、冻结真实足迹并从统一主动能力入口提交施法。
    /// </summary>
    public bool TryExecute(ActorExtend actor, in CollectiveProjectView project)
    {
        if (actor == null || actor.Base.isRekt() ||
            !TryGetPrepared(actor.Base.getID(), project.ProjectId, out PreparedCast prepared)) return false;
        if (!TryResolveBestCast(actor, in project, out _, out WorldTile target,
                out CityMagicUtilityProjectPayload payload, out MagicUtilitySpellOption option,
                out float utility)) return false;

        if (option.Ability != prepared.Ability)
        {
            prepared = new PreparedCast(project.ProjectId, option.Ability, option.Radius, prepared.CastTileId);
        }

        var validationTarget = new ActiveAbilityTarget(
            null,
            option.TargetPoint(target),
            attackKingdom: actor.Base.kingdom);
        payload.SetPlan(option);
        if (!ActiveAbilityService.CanUse(actor, prepared.Ability, in validationTarget) ||
            !CityMagicUtilityProjectRules.CaptureExecutionBaseline(project, option.Radius,
                option.Construction == null ? null : payload.PlannedActionTileIds)) return false;

        payload.ExpectedUtility = utility;
        double settleDelay = option.Construction == null
            ? ResolveVerificationDelay(option.Skill)
            : VerificationSettleMargin;
        double timeout = Math.Max(MinimumExecutionTimeout,
            (option.Construction?.Lifetime ?? (float)settleDelay) * 4d + 10d);
        if (!CollectiveProjectService.TryBeginExecution(
                project.ProjectId,
                actor.Base.getID(),
                timeout,
                out long executionToken)) return false;

        long actorId = actor.Base.getID();
        bool learnedSkill = option.Construction == null;
        payload.ConfirmedWorldChange = false;
        var pending = new PendingCast(
            project.ProjectId,
            executionToken,
            option.Skill,
            settleDelay, learnedSkill ? option.Skill.GetComponent<SkillContainer>().SkillEntityAssetID : option.Ability.EntryId,
            option.Action ?? CityMagicUtilityProjectRules.ActionFor(payload.EffectSemantic),
            payload, learnedSkill);
        lock (Sync)
        {
            PreparedByActor.Remove(actorId);
            PendingByActor[actorId] = pending;
        }

        long projectId = project.ProjectId;
        City city = actor.Base.city;
        HashSet<int> footprint = learnedSkill ? pending.Footprint : new HashSet<int>(payload.PlannedFootprintTileIds);
        ConstructionWorldScope cityScope = CityConstructionWorldScope.Create(city.id,
            () => actor.Base.id == actorId && actor.Base.isAlive() && actor.Base.city == city &&
                  (payload.Districts == null || payload.Districts.IsConnected(city, target)) &&
                  CollectiveProjectService.TryGetProject(projectId, out var current) &&
                  current.State == CollectiveProjectState.Executing &&
                  PendingByActor.TryGetValue(actorId, out var active) && ReferenceEquals(active, pending));
        var worldScope = new ConstructionWorldScope(cityScope.IsValid,
            (action, tile) => footprint.Contains(tile.tile_id) &&
                (payload.Districts == null || tile.zone == target.zone) && cityScope.Allows(action, tile));

        var executionTarget = new ActiveAbilityTarget(
            null,
            option.TargetPoint(target),
            attackKingdom: actor.Base.kingdom,
            runtimeData: new SkillCastRuntimeData { CorrelationId = executionToken },
            worldScope: worldScope);

        if (ActiveAbilityService.TryUse(
                actor,
                prepared.Ability,
                in executionTarget,
                ActiveAbilityUseOrigin.Autonomous)) return true;

        lock (Sync)
        {
            if (PendingByActor.TryGetValue(actor.Base.getID(), out PendingCast current) &&
                current.ExecutionToken == executionToken)
                PendingByActor.Remove(actor.Base.getID());
        }

        CollectiveProjectService.TryFailExecution(project.ProjectId, executionToken, true);
        return false;
    }

    /// <summary>认领有效期间，角色必须仍然持有该工程对应的准备记录。</summary>
    public bool IsAssignmentActive(ActorExtend actor, in CollectiveProjectView project)
    {
        return actor != null && !actor.Base.isRekt() &&
               TryGetPrepared(actor.Base.getID(), project.ProjectId, out _);
    }

    /// <summary>任务被取消或切换时移除尚未提交的施法准备态。</summary>
    public void OnAssignmentReleased(
        long actorId,
        ActorExtend actor,
        in CollectiveProjectView project)
    {
        lock (Sync)
        {
            if (PreparedByActor.TryGetValue(actorId, out PreparedCast prepared) &&
                prepared.ProjectId == project.ProjectId)
                PreparedByActor.Remove(actorId);
        }
    }

    /// <summary>执行超时、验收结束或所有者失效时按令牌移除完成事件等待记录。</summary>
    public void OnExecutionReleased(
        long actorId,
        long executionToken,
        in CollectiveProjectView project)
    {
        lock (Sync)
        {
            if (PendingByActor.TryGetValue(actorId, out PendingCast pending) &&
                pending.ProjectId == project.ProjectId &&
                pending.ExecutionToken == executionToken)
                PendingByActor.Remove(actorId);
        }
    }

    /// <summary>清理换图后不应跨世界保留的角色准备和施法完成等待记录。</summary>
    public void ClearWorldState()
    {
        lock (Sync)
        {
            PreparedByActor.Clear();
            PendingByActor.Clear();
            CityMagicUtilityProjectPlannerSearch.Clear();
            MagicUtilitySpellResolver.Clear();
        }
    }

    /// <summary>
    /// 接收统一技能序列完成事件；只有角色、容器、资源来源和执行令牌都匹配时才进入验收。
    /// </summary>
    internal static void HandleCastCompleted(in SkillCastCompletedEvent evt)
    {
        if (evt.Caster?.Base == null ||
            evt.FundingSource != SkillCastFundingSource.CasterResources) return;
        long actorId = evt.Caster.Base.getID();
        PendingCast pending;
        lock (Sync)
        {
            if (!PendingByActor.TryGetValue(actorId, out pending) ||
                !pending.WaitingForSequence || pending.Skill != evt.SkillContainer ||
                pending.ExecutionToken != evt.RuntimeData.CorrelationId) return;
            pending.WaitingForSequence = false;
        }

        TryCompleteCast(actorId, pending);
    }

    /// <summary>将真实受理编号连接到已经受理的城市执行令牌。</summary>
    /// <param name="evt">共同施放入口发布的事实。</param>
    internal static void HandleConstructedStarted(ConstructedCastStarted evt)
    {
        if (PendingByActor.TryGetValue(evt.SourceId, out var pending) &&
            pending.AbilityId == evt.AbilityId && pending.ExecutionToken == evt.CorrelationId)
            (pending.RunningCasts ??= new HashSet<long>()).Add(evt.CastId);
    }

    /// <summary>只累计本次工程要求的原理、足迹和实际世界改变。</summary>
    /// <param name="evt">已经提交的地块结果。</param>
    internal static void HandleConstructedContact(ConstructedWorldContact evt)
    {
        if (PendingByActor.TryGetValue(evt.SourceId, out var pending) && pending.RunningCasts?.Contains(evt.CastId) == true &&
            pending.AbilityId == evt.AbilityId && pending.Action == evt.Action && evt.Result.Changed)
        {
            WorldTile tile = World.world.GetTileSimple(evt.X, evt.Y);
            if (tile != null && pending.Footprint.Contains(tile.tile_id)) pending.Payload.ConfirmedWorldChange = true;
        }
    }

    /// <summary>在实际结束后检查任务自己的结果，不以预计寿命或其他原理的收益冒充完成。</summary>
    /// <param name="evt">已经释放本次施放的结束事实。</param>
    internal static void HandleConstructedEnded(ConstructedCastEnded evt)
    {
        if (!PendingByActor.TryGetValue(evt.SourceId, out var pending) ||
            pending.AbilityId != evt.AbilityId || pending.ExecutionToken != evt.CorrelationId ||
            pending.RunningCasts?.Remove(evt.CastId) != true) return;
        TryCompleteCast(evt.SourceId, pending);
    }

    /// <summary>最后一次实际作用结束且原队列不再发射，才提交本次工程的真实结果。</summary>
    private static void TryCompleteCast(long actorId, PendingCast pending)
    {
        if (pending.WaitingForSequence || pending.RunningCasts is { Count: > 0 }) return;
        lock (Sync)
        {
            if (!PendingByActor.TryGetValue(actorId, out var current) || !ReferenceEquals(current, pending)) return;
            PendingByActor.Remove(actorId);
        }
        bool usedCommonExecution = pending.RunningCasts != null;
        if (!usedCommonExecution) pending.Payload.ConfirmedWorldChange = null;
        if (pending.Payload.ConfirmedWorldChange == false)
            CollectiveProjectService.TryFailExecution(pending.ProjectId, pending.ExecutionToken, true);
        else
            CollectiveProjectService.TryBeginVerification(pending.ProjectId, actorId, pending.ExecutionToken,
                usedCommonExecution ? VerificationSettleMargin : pending.VerificationDelay);
    }

    /// <summary>解析项目城市、目标、合法边界以及角色当前最合适的法术版本。</summary>
    private static bool TryResolveBestCast(
        ActorExtend actor,
        in CollectiveProjectView project,
        out City city,
        out WorldTile target,
        out CityMagicUtilityProjectPayload payload,
        out MagicUtilitySpellOption option,
        out float utility)
    {
        option = default;
        utility = 0f;
        if (!CityMagicUtilityProjectRules.TryResolveProject(project, out city, out target, out payload) ||
            actor == null || actor.Base.city != city ||
            payload.Districts is { } sequence && !sequence.IsConnected(city, target)) return false;
        HashSet<int> allowed = CityMagicUtilityProjectRules.CollectAllowedTileIds(city);
        return MagicUtilitySpellResolver.TrySelectForActor(
            actor,
            city,
            target,
            payload,
            allowed,
            out option,
            out utility);
    }

    /// <summary>在法术射程内选择角色可到达且最接近当前位置的确定性站位。</summary>
    private static bool TryResolveCastTile(
        Actor actor,
        WorldTile target,
        in MagicUtilitySpellOption option,
        out WorldTile selected)
    {
        selected = null;
        if (actor?.current_tile == null || target == null) return false;
        float range = ActiveAbilityService.ResolveRange(option.Caster, option.Ability, null);
        float usableRange = Math.Max(0f, range - CastRangeMargin);
        float maxDistanceSquared = usableRange * usableRange;
        if (IsStandTile(actor, actor.current_tile) &&
            (actor.current_tile.posV - target.posV).sqrMagnitude <= maxDistanceSquared)
        {
            selected = actor.current_tile;
            return true;
        }

        using var scope = new ListPool<WorldTile>();
        if (!CityCollectiveProjectOwnerAdapter.CollectScopeTiles(actor.city, true, scope)) return false;
        float bestDistanceSquared = float.MaxValue;
        for (int i = 0; i < scope.Count; i++)
        {
            WorldTile tile = scope[i];
            if (!IsStandTile(actor, tile) ||
                (tile.posV - target.posV).sqrMagnitude > maxDistanceSquared) continue;
            Vector2 tilePosition = tile.posV;
            Vector2 actorPosition = actor.current_position;
            float actorDistanceSquared = (tilePosition - actorPosition).sqrMagnitude;
            if (selected != null && actorDistanceSquared > bestDistanceSquared) continue;
            if (selected != null && Mathf.Approximately(actorDistanceSquared, bestDistanceSquared) &&
                tile.tile_id >= selected.tile_id) continue;
            selected = tile;
            bestDistanceSquared = actorDistanceSquared;
        }

        return selected != null;
    }

    /// <summary>镜像原版/战术移动的地形约束，避免把普通角色送到无法抵达的施法点。</summary>
    private static bool IsStandTile(Actor actor, WorldTile tile)
    {
        if (actor == null || tile?.Type == null || tile.Type.lava || tile.hasBuilding()) return false;
        if (actor.isFlying()) return true;
        if (tile.Type.block || !tile.isSameIsland(actor.current_tile)) return false;
        return actor.isWaterCreature() == tile.is_liquid || actor.asset.force_land_creature;
    }

    /// <summary>按技能实体真实寿命为地块效果和原版地图缓存留出稳定时间。</summary>
    private static double ResolveVerificationDelay(Entity skill)
    {
        if (skill.IsNull || !skill.HasComponent<SkillContainer>()) return VerificationSettleMargin;
        SkillContainer container = skill.GetComponent<SkillContainer>();
        float fallback = container.Asset.ImpactProfile.Lifetime *
                         container.Asset.ImpactTuning.LifetimeMultiplier;
        return Math.Max(
            VerificationSettleMargin,
            container.Asset.ResolveRuntimeLifetime(skill, fallback) + VerificationSettleMargin);
    }

    /// <summary>读取并校验角色当前的准备记录。</summary>
    private static bool TryGetPrepared(long actorId, long projectId, out PreparedCast prepared)
    {
        lock (Sync)
        {
            return PreparedByActor.TryGetValue(actorId, out prepared) &&
                   prepared.ProjectId == projectId;
        }
    }

    /// <summary>阻止同一角色在上一项功能法术尚未完成时又认领新的工程。</summary>
    private static bool HasLivePendingCast(long actorId)
    {
        PendingCast pending;
        lock (Sync)
        {
            if (!PendingByActor.TryGetValue(actorId, out pending)) return false;
        }

        if (CollectiveProjectService.TryGetProject(pending.ProjectId, out CollectiveProjectView project) &&
            project.State is CollectiveProjectState.Executing or CollectiveProjectState.Verifying) return true;
        lock (Sync)
        {
            if (PendingByActor.TryGetValue(actorId, out PendingCast current) &&
                current.ExecutionToken == pending.ExecutionToken)
                PendingByActor.Remove(actorId);
        }

        return false;
    }

    /// <summary>角色移动前冻结的具体法术与施法站位。</summary>
    private readonly struct PreparedCast
    {
        public PreparedCast(long projectId, ActiveAbilityHandle ability, float radius, int castTileId)
        {
            ProjectId = projectId;
            Ability = ability;
            Radius = radius;
            CastTileId = castTileId;
        }

        public long ProjectId { get; }
        public ActiveAbilityHandle Ability { get; }
        public float Radius { get; }
        public int CastTileId { get; }
    }

    /// <summary>技能序列完成事件到达前保存的工程执行令牌。</summary>
    private sealed class PendingCast
    {
        /// <summary>在正式支付前连接工程、原理和真实结束等待。</summary>
        /// <param name="projectId">当前工程。</param><param name="executionToken">本次执行令牌。</param>
        /// <param name="skill">旧技能容器或新构造来源。</param><param name="verificationDelay">结果稳定等待。</param>
        /// <param name="abilityId">实际执行编号。</param><param name="action">本次要求的原理。</param>
        /// <param name="payload">冻结的工程目标与实际验收资料。</param>
        /// <param name="waitingForSequence">原已学入口仍可能安排后续发射。</param>
        public PendingCast(long projectId, long executionToken, Entity skill, double verificationDelay,
            string abilityId, string action, CityMagicUtilityProjectPayload payload, bool waitingForSequence)
        {
            ProjectId = projectId;
            ExecutionToken = executionToken;
            Skill = skill;
            VerificationDelay = verificationDelay;
            AbilityId = abilityId;
            Action = action;
            Payload = payload;
            Footprint = new HashSet<int>(payload.ExecutionFootprintTileIds);
            WaitingForSequence = waitingForSequence;
        }

        public long ProjectId { get; }
        public long ExecutionToken { get; }
        public Entity Skill { get; }
        public double VerificationDelay { get; }

        /// <summary>实际执行使用的稳定能力编号。</summary>
        internal readonly string AbilityId;

        /// <summary>本次需求要求的原理。</summary>
        internal readonly string Action;

        /// <summary>冻结的任务主作用足迹。</summary>
        internal readonly HashSet<int> Footprint;

        /// <summary>同一工程保存时读取实际结果，不根据作业前后的瞬时污染猜测。</summary>
        internal readonly CityMagicUtilityProjectPayload Payload;

        /// <summary>已受理且仍运行的各发；未进入共同执行时为空。</summary>
        internal HashSet<long> RunningCasts;

        /// <summary>原队列仍可能发出下一步，先结束的几发不能提前验收。</summary>
        internal bool WaitingForSequence;

    }
}
