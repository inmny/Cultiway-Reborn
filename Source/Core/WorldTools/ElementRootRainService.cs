using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cultiway.Const;
using Cultiway.Content;
using Cultiway.Core.ActorFiltering;
using Cultiway.Core.Components;
using Cultiway.Patch;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS.Systems;
using NeoModLoader.api;
using UnityEngine;

namespace Cultiway.Core.WorldTools;

/// <summary>灵根雨的比例、综合强度与角色过滤配置。</summary>
public sealed class ElementRootRainSettings
{
    public const float MinStrength = 1.001f;
    public const float MaxStrength = 300f;
    public const float MaxRatio = 1000000f;

    private readonly float[] _ratios = { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f };

    public ElementRootRainSettings()
    {
        Filter.Changed += NotifyChanged;
    }

    /// <summary>配置发生变化后通知窗口刷新。</summary>
    public event Action Changed;

    /// <summary>最终生成的灵根综合强度，即 <see cref="ElementRoot.GetStrength"/> 的目标值。</summary>
    public float Strength { get; private set; } = 2f;

    /// <summary>灵根雨目标使用的角色逻辑筛选表达式。</summary>
    public ActorFilterSettings Filter { get; } = new();

    /// <summary>取得一个元素的未归一化相对比例。</summary>
    public float GetRatio(int elementIndex)
    {
        ValidateElementIndex(elementIndex);
        return _ratios[elementIndex];
    }

    /// <summary>设置一个元素的非负相对比例；八项不允许同时为零。</summary>
    public void SetRatio(int elementIndex, float ratio)
    {
        ValidateElementIndex(elementIndex);
        if (float.IsNaN(ratio) || float.IsInfinity(ratio)) return;
        ratio = Mathf.Clamp(ratio, 0f, MaxRatio);
        if (_ratios[elementIndex].Equals(ratio)) return;
        _ratios[elementIndex] = ratio;
        Changed?.Invoke();
    }

    /// <summary>设置最终综合强度，并限制在不会使单元素指数属性溢出的实用区间。</summary>
    public void SetStrength(float strength)
    {
        if (float.IsNaN(strength) || float.IsInfinity(strength)) return;
        strength = Mathf.Clamp(strength, MinStrength, MaxStrength);
        if (Strength.Equals(strength)) return;
        Strength = strength;
        Changed?.Invoke();
    }

    /// <summary>取得一个元素在当前八项比例中的归一化占比。</summary>
    public float GetNormalizedRatio(int elementIndex)
    {
        ValidateElementIndex(elementIndex);
        var sum = GetRatioSum();
        return sum > 0f ? _ratios[elementIndex] / sum : 0f;
    }

    /// <summary>检查比例与筛选表达式，并冻结一次雨滴投放所需的完整配置。</summary>
    internal bool TrySnapshot(out ElementRootRainConfiguration configuration)
    {
        if (!TryResolveComposition(out var composition) || !Filter.TrySnapshot(out var expression))
        {
            configuration = default;
            return false;
        }
        configuration = new ElementRootRainConfiguration(composition, expression);
        return true;
    }

    /// <summary>
    /// 将八项比例归一化，并反解 <see cref="ElementRoot.GetStrength"/> 的指数公式，
    /// 使生成组件的综合强度精确等于当前 <see cref="Strength"/>。
    /// </summary>
    internal bool TryResolveComposition(out float[] composition)
    {
        var sum = GetRatioSum();
        if (sum <= 0f)
        {
            composition = Array.Empty<float>();
            return false;
        }

        var normalized = new float[ElementIndex.Entropy + 1];
        for (var i = 0; i < normalized.Length; i++) normalized[i] = _ratios[i] / sum;
        var weightedRatio =
            (normalized[ElementIndex.Iron] + normalized[ElementIndex.Wood] +
             normalized[ElementIndex.Water] + normalized[ElementIndex.Fire] +
             normalized[ElementIndex.Earth]) / 5f +
            (normalized[ElementIndex.Neg] + normalized[ElementIndex.Pos]) / 2f +
            normalized[ElementIndex.Entropy];
        var scale = 3f * Mathf.Log(Strength) / weightedRatio;

        composition = new float[ElementIndex.Entropy + 1];
        for (var i = 0; i < composition.Length; i++) composition[i] = normalized[i] * scale;
        return true;
    }

    private float GetRatioSum()
    {
        var sum = 0f;
        for (var i = 0; i < _ratios.Length; i++) sum += _ratios[i];
        return sum;
    }

    private static void ValidateElementIndex(int elementIndex)
    {
        if (elementIndex < 0 || elementIndex >= 8)
            throw new ArgumentOutOfRangeException(nameof(elementIndex), elementIndex, null);
    }

    private void NotifyChanged()
    {
        Changed?.Invoke();
    }
}

/// <summary>一枚灵根雨滴生成时冻结的灵根和筛选配置。</summary>
internal readonly struct ElementRootRainConfiguration
{
    public ElementRootRainConfiguration(float[] composition, ActorFilterToken[] compiledExpression)
    {
        Composition = composition;
        CompiledExpression = compiledExpression;
    }

    public float[] Composition { get; }
    public ActorFilterToken[] CompiledExpression { get; }
}

/// <summary>负责灵根雨的角色过滤、雨滴载荷生命周期、灵根重塑和修炼体系重检。</summary>
public static class ElementRootRainService
{
    private sealed class Payload
    {
        public long TargetActorId;
        public float[] Composition;
        public int SessionGeneration;
        public float ExpiresAt;
    }

    private const float PayloadLifetime = 30f;
    private static readonly Dictionary<long, Payload> Payloads = new();
    private static long _nextToken = DateTime.UtcNow.Ticks;
    private static bool _initialized;
    private static bool _modeWasActive;
    private static int _sessionGeneration;

    /// <summary>配置窗口与后续每次投放使用的共享设置。</summary>
    public static ElementRootRainSettings Settings { get; } = new();

    /// <summary>绑定世界 power、雨滴落地回调和世界清理生命周期。</summary>
    public static void Initialize()
    {
        ActorFilterCatalog.Initialize();
        WorldboxGame.GodPowers.ElementRootRain.click_action = TrySpawn;
        WorldboxGame.GodPowers.ElementRootRain.click_brush_action = TrySpawnBrush;
        WorldboxGame.Drops.ElementRootRain.action_landed_drop = OnDropLanded;

        if (_initialized) return;
        _initialized = true;
        PatchMapBox.RegisterActionOnClearWorld(ClearWorldState);
        ModClass.I.GeneralLogicSystems.Add(new UpdateSystem());
    }

    [ClickActionCaller]
    public static bool TrySpawnBrush(WorldTile tile, string powerId)
    {
        if (!CanSpawn())
        {
            ShowInvalidConfigurationTip();
            return false;
        }
        World.world.loopWithBrush(tile, Config.current_brush_data, TrySpawn, powerId);
        return true;
    }

    /// <summary>为命中且通过过滤的角色生成一枚携带灵根快照的雨滴。</summary>
    public static bool TrySpawn(WorldTile tile, string powerId)
    {
        if (!Settings.TrySnapshot(out var configuration))
        {
            ShowInvalidConfigurationTip();
            return false;
        }

        var actor = ActionLibrary.getActorFromTile(tile);
        if (actor == null || !actor.isAlive()) return false;
        var actorExtend = actor.GetExtend();
        var hasCultisys = Cultisyses.HasAnyCultisys(actorExtend);
        var availableCultisyses = Cultisyses.GetAvailableCultisysIds(actorExtend);
        if (!ActorFilterExpression.Evaluate(configuration.CompiledExpression, actor,
                cultisysId => MatchesCultisys(actorExtend, cultisysId, hasCultisys,
                    availableCultisyses))) return false;

        var token = Interlocked.Increment(ref _nextToken);
        Payloads[token] = new Payload
        {
            TargetActorId = actor.data.id,
            Composition = configuration.Composition,
            SessionGeneration = _sessionGeneration,
            ExpiresAt = Time.realtimeSinceStartup + PayloadLifetime
        };
        World.world.drop_manager.spawn(tile, WorldboxGame.Drops.ElementRootRain, pCasterId: token);
        _modeWasActive = true;
        return true;
    }

    /// <summary>雨滴落地后重塑原目标角色的灵根，并补充其新满足准入条件的修炼体系。</summary>
    public static void OnDropLanded(Drop drop, WorldTile tile, string dropId)
    {
        var token = drop.getCasterId();
        if (!Payloads.TryGetValue(token, out var payload)) return;
        Payloads.Remove(token);
        if (payload.SessionGeneration != _sessionGeneration || payload.ExpiresAt < Time.realtimeSinceStartup) return;

        var actor = World.world.units.get(payload.TargetActorId);
        if (actor == null || !actor.isAlive()) return;
        Apply(actor.GetExtend(), payload.Composition);
    }

    private static bool CanSpawn()
    {
        return Settings.Filter.ExpressionState.IsComplete && Settings.TryResolveComposition(out _);
    }

    private static void Apply(ActorExtend actor, float[] composition)
    {
        var hadCultisys = Cultisyses.HasAnyCultisys(actor);
        var root = new ElementRoot(composition);
        if (actor.HasElementRoot())
            actor.GetElementRoot() = root;
        else
            actor.AddComponent(root);
        actor.MarkCultiwayStatsDirty();
        if (!hadCultisys) Cultisyses.RecheckAvailableCultisyses(actor);
    }

    private static bool MatchesCultisys(ActorExtend actor, string cultisysId, bool hasCultisys,
        HashSet<string> availableCultisyses)
    {
        if (!hasCultisys) return availableCultisyses.Contains(cultisysId);
        var cultisys = Cultiway.Core.Progression.ProgressionService.GetRegistered(cultisysId);
        return cultisys != null && cultisys.IsOwnedBy(actor);
    }

    private static void ShowInvalidConfigurationTip()
    {
        WorldTip.showNow("Cultiway.ElementRootRain.UI.Tip.InvalidConfiguration".Localize(),
            false, "top", 3f);
    }

    private static void Tick()
    {
        if (_modeWasActive && !WorldboxGame.GodPowers.ElementRootRain.isSelected())
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
