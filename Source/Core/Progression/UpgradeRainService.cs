using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Cultiway.Const;
using Cultiway.Core.Libraries;
using Cultiway.Patch;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS.Systems;
using NeoModLoader.api;
using UnityEngine;

namespace Cultiway.Core.Progression;

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

/// <summary>过滤器下拉候选项的稳定标识和显示名称。</summary>
public readonly struct UpgradeRainFilterOption
{
    public UpgradeRainFilterOption(string id, string displayName, string iconPath = null,
        NanoObject metaObject = null)
    {
        Id = id;
        DisplayName = displayName;
        IconPath = iconPath;
        MetaObject = metaObject;
    }

    /// <summary>元对象 ID 或修炼体系资产 ID 的稳定字符串形式。</summary>
    public string Id { get; }

    /// <summary>配置窗口中展示的当前名称。</summary>
    public string DisplayName { get; }

    /// <summary>非元对象候选项使用的图标路径。</summary>
    public string IconPath { get; }

    /// <summary>元对象候选项的当前世界实例，用于生成原版 banner；其他候选项为 null。</summary>
    public NanoObject MetaObject { get; }
}

/// <summary>已经加入升级雨配置的一项过滤条件。</summary>
public readonly struct UpgradeRainFilterEntry
{
    public UpgradeRainFilterEntry(string typeId, string valueId, string displayName)
    {
        TypeId = typeId;
        ValueId = valueId;
        DisplayName = displayName;
    }

    /// <summary>过滤类别的稳定标识。</summary>
    public string TypeId { get; }

    /// <summary>该类别下被选中对象的稳定标识。</summary>
    public string ValueId { get; }

    /// <summary>添加条件时记录的名称，用于对象消失后仍能说明原条件。</summary>
    public string DisplayName { get; }
}

/// <summary>一种可由升级雨配置窗口选择的过滤类别。</summary>
public sealed class UpgradeRainFilterDescriptor
{
    private readonly Func<IReadOnlyList<UpgradeRainFilterOption>> _getOptions;
    private readonly Func<Actor, string, bool> _matches;

    public UpgradeRainFilterDescriptor(string id, string nameKey, bool isCultisys,
        Func<IReadOnlyList<UpgradeRainFilterOption>> getOptions, Func<Actor, string, bool> matches,
        MetaType metaType = MetaType.None, string iconPath = null)
    {
        Id = id;
        NameKey = nameKey;
        IsCultisys = isCultisys;
        MetaType = metaType;
        IconPath = iconPath;
        _getOptions = getOptions ?? throw new ArgumentNullException(nameof(getOptions));
        _matches = matches;
    }

    /// <summary>过滤类别的稳定标识。</summary>
    public string Id { get; }

    /// <summary>过滤类别名称的本地化键。</summary>
    public string NameKey { get; }

    /// <summary>该类别是否表示修炼体系，而不是角色所属元对象。</summary>
    public bool IsCultisys { get; }

    /// <summary>该类别对应的原版或扩展元对象类型；非元对象类别为 None。</summary>
    public MetaType MetaType { get; }

    /// <summary>过滤类别列表使用的图标路径。</summary>
    public string IconPath { get; }

    /// <summary>重新读取当前世界中可选择的对象。</summary>
    public IReadOnlyList<UpgradeRainFilterOption> GetOptions()
    {
        return _getOptions();
    }

    /// <summary>检查角色是否属于指定元对象；修炼体系类别不通过此入口判断。</summary>
    public bool Matches(Actor actor, string valueId)
    {
        return _matches != null && _matches(actor, valueId);
    }
}

/// <summary>
///     升级雨过滤类别目录。默认覆盖原版所有角色所属元对象，并额外接入宗门、地理区域和修炼体系。
/// </summary>
public static class UpgradeRainFilterCatalog
{
    public const string CultisysTypeId = "cultisys";

    private static readonly List<UpgradeRainFilterDescriptor> Descriptors = new();
    private static bool _initialized;

    /// <summary>按注册顺序提供给配置窗口的过滤类别。</summary>
    public static IReadOnlyList<UpgradeRainFilterDescriptor> Types => Descriptors;

    /// <summary>注册默认过滤类别；重复调用不会产生重复项。</summary>
    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        Register(new UpgradeRainFilterDescriptor(CultisysTypeId,
            "Cultiway.UpgradeRain.UI.FilterType.Cultisys", true, GetCultisysOptions, null,
            iconPath: "cultiway/icons/iconCultivation"));
        RegisterMeta(MetaType.Subspecies, "Cultiway.UpgradeRain.UI.FilterType.Race", actor => actor.subspecies);
        RegisterMeta(MetaType.Family, "Cultiway.UpgradeRain.UI.FilterType.Family", actor => actor.family);
        RegisterMeta(MetaType.Language, "Cultiway.UpgradeRain.UI.FilterType.Language", actor => actor.language);
        RegisterMeta(MetaType.Culture, "Cultiway.UpgradeRain.UI.FilterType.Culture", actor => actor.culture);
        RegisterMeta(MetaType.Religion, "Cultiway.UpgradeRain.UI.FilterType.Religion", actor => actor.religion);
        RegisterMeta(MetaType.Clan, "Cultiway.UpgradeRain.UI.FilterType.Clan", actor => actor.clan);
        RegisterMeta(MetaType.City, "Cultiway.UpgradeRain.UI.FilterType.City", actor => actor.city);
        RegisterMeta(MetaType.Kingdom, "Cultiway.UpgradeRain.UI.FilterType.Kingdom", actor => actor.kingdom);
        RegisterMeta(MetaType.Alliance, "Cultiway.UpgradeRain.UI.FilterType.Alliance",
            actor => actor.kingdom == null ? null : actor.kingdom.getAlliance());
        RegisterMeta(MetaType.Army, "Cultiway.UpgradeRain.UI.FilterType.Army", actor => actor.army);
        RegisterMeta(MetaTypeExtend.Sect.Back(), "Cultiway.UpgradeRain.UI.FilterType.Sect",
            actor => actor.GetExtend().sect);
        Register(new UpgradeRainFilterDescriptor(WorldboxGame.MetaTypes.GeoRegion.id,
            "Cultiway.UpgradeRain.UI.FilterType.GeoRegion", false,
            () => GetMetaOptions(MetaTypeExtend.GeoRegion.Back()), MatchesGeoRegion,
            MetaTypeExtend.GeoRegion.Back(), WorldboxGame.MetaTypes.GeoRegion.icon_single_path));
    }

    /// <summary>
    ///     注册或替换过滤类别。同一 ID 被再次注册时保留原顺序并替换实现，供后续体系扩展更多元对象。
    /// </summary>
    public static void Register(UpgradeRainFilterDescriptor descriptor)
    {
        if (descriptor == null) return;
        for (var i = 0; i < Descriptors.Count; i++)
        {
            if (Descriptors[i].Id != descriptor.Id) continue;
            Descriptors[i] = descriptor;
            return;
        }
        Descriptors.Add(descriptor);
    }

    /// <summary>按稳定类别标识取得过滤定义；不存在时返回 null。</summary>
    public static UpgradeRainFilterDescriptor Get(string id)
    {
        for (var i = 0; i < Descriptors.Count; i++)
        {
            if (Descriptors[i].Id == id) return Descriptors[i];
        }
        return null;
    }

    private static void RegisterMeta(MetaType type, string nameKey, Func<Actor, NanoObject> resolve)
    {
        var asset = type.getAsset();
        Register(new UpgradeRainFilterDescriptor(asset.id, nameKey, false, () => GetMetaOptions(type),
            (actor, valueId) => MatchesSingleMeta(actor, valueId, resolve), type,
            !string.IsNullOrEmpty(asset.icon_single_path) ? asset.icon_single_path : asset.icon_list));
    }

    private static IReadOnlyList<UpgradeRainFilterOption> GetCultisysOptions()
    {
        return ProgressionService.RegisteredCultisyses
            .OrderBy(asset => asset.GetName(), StringComparer.Ordinal)
            .Select(asset => new UpgradeRainFilterOption(asset.id, asset.GetName(), asset.IconPath))
            .ToArray();
    }

    private static IReadOnlyList<UpgradeRainFilterOption> GetMetaOptions(MetaType type)
    {
        var asset = type.getAsset();
        if (asset?.get_list == null) return Array.Empty<UpgradeRainFilterOption>();

        var result = new List<UpgradeRainFilterOption>();
        var objects = asset.get_list();
        if (objects == null) return result;
        foreach (var item in objects)
        {
            if (item == null || !item.isAlive()) continue;
            var name = string.IsNullOrWhiteSpace(item.name)
                ? $"#{item.getID()}"
                : item.name;
            result.Add(new UpgradeRainFilterOption(
                item.getID().ToString(CultureInfo.InvariantCulture), name, metaObject: item));
        }
        result.Sort((left, right) => StringComparer.Ordinal.Compare(left.DisplayName, right.DisplayName));
        return result;
    }

    private static bool MatchesSingleMeta(Actor actor, string valueId, Func<Actor, NanoObject> resolve)
    {
        if (actor == null || !long.TryParse(valueId, NumberStyles.Integer, CultureInfo.InvariantCulture,
                out var targetId)) return false;
        var meta = resolve(actor);
        return meta != null && meta.isAlive() && meta.getID() == targetId;
    }

    private static bool MatchesGeoRegion(Actor actor, string valueId)
    {
        if (actor?.current_tile == null ||
            !long.TryParse(valueId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var targetId))
            return false;

        foreach (var region in actor.current_tile.GetExtend().GetGeoRegions())
        {
            if (region != null && region.isAlive() && region.getID() == targetId) return true;
        }
        return false;
    }
}

/// <summary>升级雨配置窗口与实际投放共享的可变设置。</summary>
public sealed class UpgradeRainSettings
{
    public const int MaxAmount = 10000;

    private readonly List<UpgradeRainFilterToken> _expression = new();
    private UpgradeRainFilterToken[] _compiledExpression = Array.Empty<UpgradeRainFilterToken>();
    private UpgradeRainExpressionState _expressionState = UpgradeRainExpression.Analyze(null);

    /// <summary>配置发生变化后通知窗口刷新。</summary>
    public event Action Changed;

    /// <summary>当前选择的进阶方式。</summary>
    public UpgradeRainMode Mode { get; private set; } = UpgradeRainMode.FixedMinor;

    /// <summary>次数或从一开始显示的目标境界序号。</summary>
    public int Amount { get; private set; } = 1;

    /// <summary>按用户编辑顺序保存的中缀过滤表达式。</summary>
    public IReadOnlyList<UpgradeRainFilterToken> Expression => _expression;

    /// <summary>当前表达式是否完整，以及编辑器下一步允许追加的词元。</summary>
    public UpgradeRainExpressionState ExpressionState => _expressionState;

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

    /// <summary>在表达式末尾追加一个具体过滤条件。</summary>
    public bool AppendPredicate(UpgradeRainFilterEntry entry)
    {
        if (string.IsNullOrEmpty(entry.TypeId) || string.IsNullOrEmpty(entry.ValueId)) return false;
        if (!_expressionState.CanAppend(UpgradeRainFilterTokenKind.Predicate)) return false;
        _expression.Add(UpgradeRainFilterToken.FromPredicate(entry));
        RecompileExpression();
        return true;
    }

    /// <summary>在表达式末尾追加一个当前语法允许的逻辑符号。</summary>
    public bool AppendSymbol(UpgradeRainFilterTokenKind kind)
    {
        if (kind == UpgradeRainFilterTokenKind.Predicate || !_expressionState.CanAppend(kind)) return false;
        _expression.Add(UpgradeRainFilterToken.FromSymbol(kind));
        RecompileExpression();
        return true;
    }

    /// <summary>移除表达式最后一个词元，供配置窗口逐步撤销输入。</summary>
    public void RemoveLastToken()
    {
        if (_expression.Count == 0) return;
        _expression.RemoveAt(_expression.Count - 1);
        RecompileExpression();
    }

    /// <summary>清空整个表达式，恢复为所有已拥有修炼体系都通过过滤。</summary>
    public void ClearExpression()
    {
        if (_expression.Count == 0) return;
        _expression.Clear();
        RecompileExpression();
    }

    /// <summary>
    ///     切换世界时只要表达式引用过世界元对象，就整体清空；纯修炼体系表达式可以跨世界保留。
    /// </summary>
    internal void ClearWorldExpression()
    {
        for (var i = 0; i < _expression.Count; i++)
        {
            var token = _expression[i];
            if (token.Kind != UpgradeRainFilterTokenKind.Predicate ||
                token.Predicate.TypeId == UpgradeRainFilterCatalog.CultisysTypeId) continue;
            _expression.Clear();
            RecompileExpression();
            return;
        }
    }

    /// <summary>表达式完整时冻结当前设置和已编译表达式，供一次投放使用。</summary>
    internal bool TrySnapshot(out UpgradeRainConfiguration configuration)
    {
        if (!_expressionState.IsComplete)
        {
            configuration = default;
            return false;
        }
        configuration = new UpgradeRainConfiguration(Mode, Amount, _compiledExpression);
        return true;
    }

    private void RecompileExpression()
    {
        UpgradeRainExpression.TryCompile(_expression, out _compiledExpression, out _expressionState);
        Changed?.Invoke();
    }
}

/// <summary>一次投放开始时冻结的升级雨配置。</summary>
internal readonly struct UpgradeRainConfiguration
{
    public UpgradeRainConfiguration(UpgradeRainMode mode, int amount,
        UpgradeRainFilterToken[] compiledExpression)
    {
        Mode = mode;
        Amount = amount;
        CompiledExpression = compiledExpression;
    }

    public UpgradeRainMode Mode { get; }
    public int Amount { get; }
    public UpgradeRainFilterToken[] CompiledExpression { get; }
}

/// <summary>负责升级雨 brush 投放、雨滴载荷生命周期、过滤和落地进阶结算。</summary>
public static class UpgradeRainService
{
    private sealed class Payload
    {
        public long TargetActorId;
        public UpgradeRainMode Mode;
        public int Amount;
        public string[] CultisysIds;
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
        UpgradeRainFilterCatalog.Initialize();
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
        if (!Settings.ExpressionState.IsComplete)
        {
            WorldTip.showNow("Cultiway.UpgradeRain.UI.Tip.InvalidExpression".Localize(), false, "top", 3f);
            return false;
        }
        World.world.loopWithBrush(tile, Config.current_brush_data, TrySpawn, powerId);
        return true;
    }

    /// <summary>为命中且通过过滤的角色生成一枚携带配置快照的升级雨滴。</summary>
    public static bool TrySpawn(WorldTile tile, string powerId)
    {
        if (!Settings.TrySnapshot(out var configuration))
        {
            WorldTip.showNow("Cultiway.UpgradeRain.UI.Tip.InvalidExpression".Localize(), false, "top", 3f);
            return false;
        }
        var actor = ActionLibrary.getActorFromTile(tile);
        if (actor == null || !actor.isAlive()) return false;

        var cultisysIds = ResolveCultisysIds(actor.GetExtend(), configuration.CompiledExpression);
        if (cultisysIds.Length == 0) return false;

        var token = Interlocked.Increment(ref _nextToken);
        Payloads[token] = new Payload
        {
            TargetActorId = actor.data.id,
            Mode = configuration.Mode,
            Amount = configuration.Amount,
            CultisysIds = cultisysIds,
            SessionGeneration = _sessionGeneration,
            ExpiresAt = Time.realtimeSinceStartup + PayloadLifetime
        };
        World.world.drop_manager.spawn(tile, WorldboxGame.Drops.UpgradeRain, pCasterId: token);
        _modeWasActive = true;
        return true;
    }

    /// <summary>雨滴落地后取得原目标角色，并按冻结配置处理其全部目标修炼体系。</summary>
    public static void OnDropLanded(Drop drop, WorldTile tile, string dropId)
    {
        var token = drop.getCasterId();
        if (!Payloads.TryGetValue(token, out var payload)) return;
        Payloads.Remove(token);
        if (payload.SessionGeneration != _sessionGeneration || payload.ExpiresAt < Time.realtimeSinceStartup) return;

        var actor = World.world.units.get(payload.TargetActorId);
        if (actor == null || !actor.isAlive()) return;
        Apply(actor.GetExtend(), payload);
    }

    private static string[] ResolveCultisysIds(ActorExtend actor,
        UpgradeRainFilterToken[] compiledExpression)
    {
        var result = new List<string>();
        var registered = ProgressionService.RegisteredCultisyses;
        for (var i = 0; i < registered.Count; i++)
        {
            var cultisys = registered[i];
            if (!cultisys.IsOwnedBy(actor)) continue;
            if (!UpgradeRainExpression.Evaluate(compiledExpression, actor.Base, cultisys.id)) continue;
            result.Add(cultisys.id);
        }
        return result.ToArray();
    }

    private static void Apply(ActorExtend actor, Payload payload)
    {
        if (payload.Mode is UpgradeRainMode.CappedMinor or UpgradeRainMode.CappedMajor)
        {
            ApplySingleCappedProgress(actor, payload, payload.Mode == UpgradeRainMode.CappedMinor
                ? ProgressionKind.Minor
                : ProgressionKind.Major);
            return;
        }

        for (var i = 0; i < payload.CultisysIds.Length; i++)
        {
            var cultisys = ProgressionService.GetRegistered(payload.CultisysIds[i]);
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

    /// <summary>
    ///     让一滴封顶模式的升级雨至多提交一次指定层级的进展，并按角色累计本次启用期间的成功次数。
    /// </summary>
    private static void ApplySingleCappedProgress(ActorExtend actor, Payload payload, ProgressionKind kind)
    {
        var progressByActor = kind == ProgressionKind.Minor
            ? CappedMinorProgressByActor
            : CappedMajorProgressByActor;
        progressByActor.TryGetValue(payload.TargetActorId, out var progress);
        if (progress >= payload.Amount) return;

        for (var i = 0; i < payload.CultisysIds.Length; i++)
        {
            var cultisys = ProgressionService.GetRegistered(payload.CultisysIds[i]);
            if (cultisys == null || !cultisys.IsOwnedBy(actor)) continue;
            var result = kind == ProgressionKind.Minor
                ? cultisys.GrantNextMinor(actor)
                : cultisys.GrantNextRealm(actor);
            if (!result.Changed) continue;

            progressByActor[payload.TargetActorId] = progress + 1;
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
        Settings.ClearWorldExpression();
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
