using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cultiway.Core.ActorFiltering;
using Cultiway.Core.Progression;
using Cultiway.Patch;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS.Systems;
using NeoModLoader.api;
using UnityEngine;

namespace Cultiway.Core.WorldTools;

/// <summary>升级雨支持的五种互斥进阶方式。</summary>
public enum UpgradeRainMode
{
    /// <summary>依次授予固定次数的小境界进展，不跨越大境界。</summary>
    FixedMinor,
    /// <summary>同一次升级雨启用期间，每名角色累计至多获得指定次数的小境界进展。</summary>
    CappedMinor,
    /// <summary>逐项结算必要小境界后，依次授予固定次数的大境界进展。</summary>
    FixedMajor,
    /// <summary>逐项结算必要小境界后，在本次启用期间为每名角色至多授予指定次数的大境界进展。</summary>
    CappedMajor,
    /// <summary>逐级授予到指定的主境界序号；序号在 UI 中从一开始。</summary>
    ToRealm
}

/// <summary>升级雨配置窗口与实际投放共享的可变设置。</summary>
public sealed class UpgradeRainSettings
{
    public const int MaxAmount = 10000;

    public UpgradeRainSettings()
    {
        Filter.Changed += NotifyChanged;
    }

    /// <summary>配置发生变化后通知窗口刷新。</summary>
    public event Action Changed;

    /// <summary>当前选择的进阶方式。</summary>
    public UpgradeRainMode Mode { get; private set; } = UpgradeRainMode.FixedMinor;

    /// <summary>次数或从一开始显示的目标境界序号。</summary>
    public int Amount { get; private set; } = 1;

    /// <summary>升级目标使用的角色逻辑筛选表达式。</summary>
    public ActorFilterSettings Filter { get; } = new();

    public void SetMode(UpgradeRainMode mode)
    {
        if (Mode == mode) return;
        Mode = mode;
        Changed?.Invoke();
    }

    public void SetAmount(int amount)
    {
        amount = Mathf.Clamp(amount, 1, MaxAmount);
        if (Amount == amount) return;
        Amount = amount;
        Changed?.Invoke();
    }

    /// <summary>表达式完整时冻结当前设置和已编译表达式，供一次投放使用。</summary>
    internal bool TrySnapshot(out UpgradeRainConfiguration configuration)
    {
        if (!Filter.TrySnapshot(out var expression))
        {
            configuration = default;
            return false;
        }
        configuration = new UpgradeRainConfiguration(Mode, Amount, expression);
        return true;
    }

    private void NotifyChanged()
    {
        Changed?.Invoke();
    }
}

/// <summary>一次投放开始时冻结的升级雨配置。</summary>
internal readonly struct UpgradeRainConfiguration
{
    public UpgradeRainConfiguration(UpgradeRainMode mode, int amount, ActorFilterToken[] compiledExpression)
    {
        Mode = mode;
        Amount = amount;
        CompiledExpression = compiledExpression;
    }

    public UpgradeRainMode Mode { get; }
    public int Amount { get; }
    public ActorFilterToken[] CompiledExpression { get; }
}

/// <summary>负责升级雨 brush 投放、雨滴载荷生命周期、过滤和落地进阶结算。</summary>
public static class UpgradeRainService
{
    private sealed class Payload
    {
        public UpgradeRainMode Mode;
        public int Amount;
        public ActorFilterToken[] CompiledExpression;
        public int SessionGeneration;
        public float ExpiresAt;
    }

    private const float PayloadLifetime = 30f;
    private static readonly Dictionary<long, Payload> Payloads = new();
    /// <summary>本次升级雨启用期间，每名角色已经成功获得的小境界次数。</summary>
    private static readonly Dictionary<long, int> CappedMinorProgressByActor = new();
    /// <summary>本次升级雨启用期间，每名角色已经成功获得的大境界次数。</summary>
    private static readonly Dictionary<long, int> CappedMajorProgressByActor = new();
    private static long _nextToken = DateTime.UtcNow.Ticks;
    private static bool _initialized;
    private static bool _modeWasActive;
    private static int _sessionGeneration;

    /// <summary>配置窗口和后续每次投放使用的共享设置。</summary>
    public static UpgradeRainSettings Settings { get; } = new();

    /// <summary>绑定世界 power、雨滴落地回调和世界清理生命周期。</summary>
    public static void Initialize()
    {
        ActorFilterCatalog.Initialize();
        WorldboxGame.GodPowers.UpgradeRain.click_action = TrySpawn;
        WorldboxGame.GodPowers.UpgradeRain.click_brush_action = TrySpawnBrush;
        WorldboxGame.Drops.UpgradeRain.action_landed_drop = OnDropLanded;

        if (_initialized) return;
        _initialized = true;
        PatchMapBox.RegisterActionOnClearWorld(ClearWorldState);
        ModClass.I.GeneralLogicSystems.Add(new UpdateSystem());
    }

    [ClickActionCaller]
    public static bool TrySpawnBrush(WorldTile tile, string powerId)
    {
        if (!Settings.Filter.ExpressionState.IsComplete)
        {
            WorldTip.showNow("Cultiway.UpgradeRain.UI.Tip.InvalidExpression".Localize(), false, "top", 3f);
            return false;
        }
        World.world.loopWithBrush(tile, Config.current_brush_data, TrySpawn, powerId);
        return true;
    }

    /// <summary>格子中至少有一个修炼体系通过过滤时，生成一枚携带配置快照的升级雨滴。</summary>
    public static bool TrySpawn(WorldTile tile, string powerId)
    {
        if (!Settings.TrySnapshot(out var configuration))
        {
            WorldTip.showNow("Cultiway.UpgradeRain.UI.Tip.InvalidExpression".Localize(), false, "top", 3f);
            return false;
        }
        var actors = WorldToolDropTargets.SnapshotAliveActors(tile);
        var hasTarget = false;
        for (var i = 0; i < actors.Count; i++)
        {
            if (ResolveCultisysIds(actors[i].GetExtend(), configuration.CompiledExpression).Length == 0) continue;
            hasTarget = true;
            break;
        }
        if (!hasTarget) return false;

        var token = Interlocked.Increment(ref _nextToken);
        Payloads[token] = new Payload
        {
            Mode = configuration.Mode,
            Amount = configuration.Amount,
            CompiledExpression = configuration.CompiledExpression,
            SessionGeneration = _sessionGeneration,
            ExpiresAt = Time.realtimeSinceStartup + PayloadLifetime
        };
        World.world.drop_manager.spawn(tile, WorldboxGame.Drops.UpgradeRain, pCasterId: token);
        _modeWasActive = true;
        return true;
    }

    /// <summary>雨滴落地后按冻结配置处理该格子中所有角色的目标修炼体系。</summary>
    public static void OnDropLanded(Drop drop, WorldTile tile, string dropId)
    {
        var token = drop.getCasterId();
        if (!Payloads.TryGetValue(token, out var payload)) return;
        Payloads.Remove(token);
        if (payload.SessionGeneration != _sessionGeneration || payload.ExpiresAt < Time.realtimeSinceStartup) return;

        var actors = WorldToolDropTargets.SnapshotAliveActors(tile);
        for (var i = 0; i < actors.Count; i++)
        {
            var actor = actors[i];
            if (!actor.isAlive()) continue;
            var actorExtend = actor.GetExtend();
            var cultisysIds = ResolveCultisysIds(actorExtend, payload.CompiledExpression);
            if (cultisysIds.Length == 0) continue;
            Apply(actorExtend, payload, cultisysIds);
        }
    }

    private static string[] ResolveCultisysIds(ActorExtend actor, ActorFilterToken[] compiledExpression)
    {
        var result = new List<string>();
        var registered = ProgressionService.RegisteredCultisyses;
        for (var i = 0; i < registered.Count; i++)
        {
            var cultisys = registered[i];
            if (!cultisys.IsOwnedBy(actor)) continue;
            if (!ActorFilterExpression.Evaluate(compiledExpression, actor.Base,
                    filterId => string.Equals(filterId, cultisys.id, StringComparison.Ordinal))) continue;
            result.Add(cultisys.id);
        }
        return result.ToArray();
    }

    private static void Apply(ActorExtend actor, Payload payload, string[] cultisysIds)
    {
        if (payload.Mode is UpgradeRainMode.CappedMinor or UpgradeRainMode.CappedMajor)
        {
            ApplySingleCappedProgress(actor, payload, cultisysIds, payload.Mode == UpgradeRainMode.CappedMinor
                ? ProgressionKind.Minor
                : ProgressionKind.Major);
            return;
        }

        for (var i = 0; i < cultisysIds.Length; i++)
        {
            var cultisys = ProgressionService.GetRegistered(cultisysIds[i]);
            if (cultisys == null || !cultisys.IsOwnedBy(actor)) continue;

            switch (payload.Mode)
            {
                case UpgradeRainMode.FixedMinor:
                    ApplyRepeated(payload.Amount, () => cultisys.GrantNextMinor(actor));
                    break;
                case UpgradeRainMode.FixedMajor:
                    ApplyRepeated(payload.Amount, () => cultisys.GrantNextRealm(actor));
                    break;
                case UpgradeRainMode.ToRealm:
                    cultisys.GrantToRealm(actor, payload.Amount - 1);
                    break;
            }
        }
    }

    /// <summary>让一滴封顶模式的升级雨至多提交一次进展，并累计本次启用期间的成功次数。</summary>
    private static void ApplySingleCappedProgress(ActorExtend actor, Payload payload, string[] cultisysIds,
        ProgressionKind kind)
    {
        var progressByActor = kind == ProgressionKind.Minor
            ? CappedMinorProgressByActor
            : CappedMajorProgressByActor;
        var actorId = actor.Base.data.id;
        progressByActor.TryGetValue(actorId, out var progress);
        if (progress >= payload.Amount) return;

        for (var i = 0; i < cultisysIds.Length; i++)
        {
            var cultisys = ProgressionService.GetRegistered(cultisysIds[i]);
            if (cultisys == null || !cultisys.IsOwnedBy(actor)) continue;
            var result = kind == ProgressionKind.Minor
                ? cultisys.GrantNextMinor(actor)
                : cultisys.GrantNextRealm(actor);
            if (!result.Changed) continue;
            progressByActor[actorId] = progress + 1;
            return;
        }
    }

    private static void ApplyRepeated(int count, Func<ProgressionResult> grant)
    {
        for (var i = 0; i < count; i++)
        {
            if (!grant().Changed) break;
        }
    }

    private static void Tick()
    {
        if (_modeWasActive && !WorldboxGame.GodPowers.UpgradeRain.isSelected())
        {
            ClearPayloads();
            return;
        }

        var now = Time.realtimeSinceStartup;
        var expired = Payloads.Where(pair => pair.Value.ExpiresAt < now).Select(pair => pair.Key).ToArray();
        for (var i = 0; i < expired.Length; i++) Payloads.Remove(expired[i]);
    }

    private static void ClearPayloads()
    {
        _sessionGeneration++;
        Payloads.Clear();
        CappedMinorProgressByActor.Clear();
        CappedMajorProgressByActor.Clear();
        _modeWasActive = false;
    }

    private static void ClearWorldState()
    {
        ClearPayloads();
        Settings.Filter.ClearWorldExpression();
    }

    private sealed class UpdateSystem : BaseSystem
    {
        protected override void OnUpdateGroup()
        {
            base.OnUpdateGroup();
            Tick();
        }
    }
}
