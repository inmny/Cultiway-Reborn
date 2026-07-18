using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Core.Localization;
using Cultiway.Core.Progression;
using Cultiway.Utils.Extension;
using NeoModLoader.General;

namespace Cultiway.Core.Libraries;
public delegate float GetDetailedLevel(ActorExtend actor_extend);

/// <summary>修炼体系向通用详情界面追加的一行只读展示数据。</summary>
public readonly struct CultisysDisplayLine
{
    public CultisysDisplayLine(string labelKey, string value, string iconPath = null, string colorHex = null)
        : this(labelKey, value, iconPath, colorHex, float.NaN, float.NaN)
    {
    }

    private CultisysDisplayLine(string labelKey, string value, string iconPath, string colorHex,
                                float progressValue, float progressMax)
    {
        LabelKey = labelKey;
        Value = value;
        IconPath = iconPath;
        ColorHex = colorHex;
        ProgressValue = progressValue;
        ProgressMax = progressMax;
    }

    /// <summary>创建由 UI 以进度条显示的数值行。</summary>
    public static CultisysDisplayLine CreateProgress(string labelKey, float value, float max,
                                                      string iconPath = null, string colorHex = null)
    {
        return new CultisysDisplayLine(labelKey, null, iconPath, colorHex, value, max);
    }

    /// <summary>行标题的本地化键。</summary>
    public string LabelKey { get; }

    /// <summary>已经格式化、无需再次本地化的展示值。</summary>
    public string Value { get; }

    /// <summary>可选图标路径；当前由支持图标的详情界面使用。</summary>
    public string IconPath { get; }

    /// <summary>可选的 HTML 颜色值，例如 #43FF43。</summary>
    public string ColorHex { get; }

    /// <summary>进度条当前值；普通文本行使用 NaN。</summary>
    public float ProgressValue { get; }

    /// <summary>进度条上限；普通文本行使用 NaN。</summary>
    public float ProgressMax { get; }

    /// <summary>该行是否应以进度条显示。</summary>
    public bool HasProgress => !float.IsNaN(ProgressValue) && !float.IsNaN(ProgressMax);
}

/// <summary>由具体修炼体系向通用详情界面追加体系专属数据。</summary>
public delegate void CultisysDisplayDetailProvider(ActorExtend actor, ICollection<CultisysDisplayLine> lines);

/// <summary>
///     不依赖具体 ECS 组件类型的修炼体系资产接口，供通用 UI、任务调度和进阶服务访问。
/// </summary>
public abstract class BaseCultisysAsset : Asset
{
    private const string LevelNameSectionId = "cultisys_level_names";

    private BaseStats[]                _level_accum_stats;
    private BaseStats[]                _level_base_stats;
    private string[]                   _level_desc_keys;
    private string[]                   _level_name_keys;
    private int                        _level_nr;
    private string[]                   _levelup_msg_keys;
    private string                     _name_key;
    protected GetDetailedLevel[]        _detailed_levels;
    public  ReadOnlyCollection<string> LevelDescKeys;
    public  ReadOnlyCollection<string> LevelNameKeys;
    public  ReadOnlyCollection<string> LevelupMsgKeys;

    /// <summary>
    ///     ElementRootPage 展示风格。为 null 时页面回退到仙道默认风格（保持向后兼容）。
    /// </summary>
    public ElementRootDisplayStyle DisplayStyle { get; set; }

    /// <summary>通用修炼体系选择器中使用的图标路径。</summary>
    public string IconPath { get; set; } = "cultiway/icons/iconCultivation";

    /// <summary>为通用详情界面提供体系专属资源、结构或规则数据的可选委托。</summary>
    public CultisysDisplayDetailProvider DisplayDetailProvider { get; set; }

    protected BaseCultisysAsset(string id, int level_nr)
    {
        this.id = id;
        _level_nr = level_nr;

        _level_base_stats = new BaseStats[level_nr];
        _level_accum_stats = new BaseStats[level_nr];
        LevelAccumBaseStats = Array.AsReadOnly(_level_accum_stats);
        LevelBaseStats = Array.AsReadOnly(_level_base_stats);

        _level_name_keys = new string[level_nr];
        _level_desc_keys = new string[level_nr];
        _levelup_msg_keys = new string[level_nr];

        LevelNameKeys = Array.AsReadOnly(_level_name_keys);
        LevelDescKeys = Array.AsReadOnly(_level_desc_keys);
        LevelupMsgKeys = Array.AsReadOnly(_levelup_msg_keys);

        _name_key = $"cultisys_{id}";
        for (int i = 0; i < level_nr; i++)
        {
            _level_base_stats[i] = new();
            _level_accum_stats[i] = new();
            _level_name_keys[i] = $"cultisys_{id}_{i}";
            _level_desc_keys[i] = $"cultisys_info_{id}_{i}";
            _levelup_msg_keys[i] = $"cultisys_{id}_{i}_msg";
        }

        _level_accum_stats[0] = _level_base_stats[0];
    }

    public int                           LevelNumber         => _level_nr;
    public ReadOnlyCollection<BaseStats> LevelAccumBaseStats { get; private set; }
    public ReadOnlyCollection<BaseStats> LevelBaseStats      { get; private set; }

    public void UpdateAccumStats()
    {
        for (int i = 1; i < _level_nr; i++)
        {
            _level_accum_stats[i].clear();
            _level_accum_stats[i].mergeStats(_level_accum_stats[i - 1]);
            _level_accum_stats[i].mergeStats(_level_base_stats[i]);
        }
    }

    public string GetName()
    {
        return LM.Get(_name_key);
    }

    public string GetLevelName(int level)
    {
        return ModifiableLocalizationManager.GetText(LevelNameSectionId, $"{id}.{level}");
    }

    public string GetLevelDescription(int level)
    {
        return LM.Get(_level_desc_keys[level]);
    }

    /// <summary>仅在当前境界确实配置了本地化描述时返回该描述。</summary>
    public bool TryGetLevelDescription(int level, out string description)
    {
        if (level < 0 || level >= _level_desc_keys.Length)
        {
            description = null;
            return false;
        }
        string key = _level_desc_keys[level];
        if (!LMTools.Has(key))
        {
            description = null;
            return false;
        }
        description = LM.Get(key);
        return true;
    }

    public string GetLevelupMessage(int level)
    {
        var key = _levelup_msg_keys[level];
        if (!LMTools.Has(key)) return "";
        return LM.Get(key);
    }

    public float GetLevelForSort(ActorExtend actor_extend, int base_level)
    {
        return base_level + (_detailed_levels[base_level]?.Invoke(actor_extend) ?? 0);
    }

    /// <summary>角色是否拥有本修炼体系。</summary>
    public abstract bool IsOwnedBy(ActorExtend actor);

    /// <summary>读取角色在本体系中的当前主等级；未拥有本体系时返回 -1。</summary>
    public abstract int GetCurrentLevel(ActorExtend actor);

    /// <summary>读取本体系截至指定主等级累计提供的属性；返回对象只用于读取。</summary>
    public abstract BaseStats GetProvidedStats(int level);

    /// <summary>读取指定主等级用于跨体系比较的战力层级。</summary>
    public abstract float GetLevelPower(int level);

    /// <summary>向目标集合追加当前角色的体系专属展示数据。</summary>
    public void AppendDisplayDetails(ActorExtend actor, ICollection<CultisysDisplayLine> lines)
    {
        if (lines == null) throw new ArgumentNullException(nameof(lines));
        DisplayDetailProvider?.Invoke(actor, lines);
    }

    /// <summary>无副作用查询角色当前最可能执行的进阶过渡。</summary>
    public abstract ProgressionQuery QueryProgression(ActorExtend actor);

    /// <summary>当前是否应为角色调度进阶工作。</summary>
    public abstract bool CanScheduleProgression(ActorExtend actor);

    /// <summary>按体系自然规则尝试一次进阶。</summary>
    public abstract ProgressionResult TryAdvanceNaturally(ActorExtend actor);

    /// <summary>无视自然条件，授予下一个小境界，但不改变主等级。</summary>
    public abstract ProgressionResult GrantNextMinor(ActorExtend actor);

    /// <summary>无视自然条件，逐项结算必要小境界后，完成下一个大境界的结构变换与奖励。</summary>
    public abstract ProgressionResult GrantNextRealm(ActorExtend actor);

    /// <summary>逐级授予到目标大境界，结算沿途必要小境界、结构变换和奖励。</summary>
    public abstract ProgressionResult GrantToRealm(ActorExtend actor, int targetLevel);

    /// <summary>从指定大境界出发，沿完整授予路径能够到达的最高大境界。</summary>
    public abstract int GetHighestGrantableRealm(int startLevel);

    /// <summary>把角色同步到目标大境界，执行必要结构修复但不重放奖励。</summary>
    public abstract ProgressionResult SynchronizeToRealm(ActorExtend actor, int targetLevel);

    /// <summary>把来源角色的体系组件和体系专属结构完整传承给目标角色。</summary>
    public abstract ProgressionResult TransferFrom(ActorExtend source, ActorExtend target);
}

/// <summary>绑定具体体系组件、等级属性、技能列表和进阶图的修炼体系资产。</summary>
public class CultisysAsset<T> : BaseCultisysAsset where T : struct, ICultisysComponent
{
    /// <summary>读取体系组件时可附加执行的体系专属回调签名。</summary>
    public delegate void OnGet(ActorExtend ae, CultisysAsset<T> cultisys, ref T component);

    /// <summary>角色首次获得本体系时使用的默认组件值。</summary>
    public readonly T DefaultComponent;

    /// <summary>按主等级保存已经累计可用的技能标识。</summary>
    private readonly List<string>[] _skills;

    /// <summary>按主等级保存用于跨体系比较的战力等级。</summary>
    private readonly float[] _power_levels;

    /// <summary>创建一个具有明确进阶图的修炼体系资产。</summary>
    public CultisysAsset(string id, int level_nr, T default_component,
                         CultisysProgressionProfile<T> progression,
                         List<string>[] skills = null, float[] power_levels = null,
                         GetDetailedLevel[] detailed_levels = null) : base(id, level_nr)
    {
        Progression = progression ?? throw new ArgumentNullException(nameof(progression));
        _power_levels = power_levels                 ?? new float[level_nr];
        _detailed_levels = detailed_levels           ?? new GetDetailedLevel[level_nr];
        _skills = new List<string>[level_nr];

        if (_power_levels.Length != level_nr)
            throw new ArgumentException("power_levels 长度必须与 level_nr 一致", nameof(power_levels));
        if (_detailed_levels.Length != level_nr)
            throw new ArgumentException("detailed_levels 长度必须与 level_nr 一致", nameof(detailed_levels));
        if (skills != null)
        {
            if (skills.Length != level_nr)
                throw new ArgumentException("skills 长度必须与 level_nr 一致", nameof(skills));
            _skills[0] = skills[0] ?? [];
            for (var i = 1; i < level_nr; i++) _skills[i] = _skills[i - 1].Concat(skills[i] ?? []).ToList();
        }
        else
        {
            for (var i = 0; i < level_nr; i++) _skills[i] = [];
        }

        if (power_levels == null)
        {
            for (var i = 0; i < level_nr; i++) _power_levels[i] = i + 1;
        }

        DefaultComponent = default_component;
        PowerLevels = Array.AsReadOnly(_power_levels);
        Skills = Array.AsReadOnly(_skills.Select(x => x.AsReadOnly()).ToArray());
    }

    /// <summary>各主等级对应的只读战力等级表。</summary>
    public ReadOnlyCollection<float> PowerLevels { get; private set; }

    /// <summary>各主等级累计可用的只读技能表。</summary>
    public ReadOnlyCollection<ReadOnlyCollection<string>> Skills { get; private set; }

    /// <summary>读取体系组件时执行的可选专属回调。</summary>
    public OnGet OnGetAction { get; private set; }

    /// <summary>本体系所有境界、过渡、结算和同步规则的唯一进阶定义。</summary>
    public CultisysProgressionProfile<T> Progression { get; }

    public override bool IsOwnedBy(ActorExtend actor)
    {
        return actor.HasCultisys<T>();
    }

    public override int GetCurrentLevel(ActorExtend actor)
    {
        return actor.HasCultisys<T>() ? actor.GetCultisys<T>().CurrLevel : -1;
    }

    public override BaseStats GetProvidedStats(int level)
    {
        return LevelAccumBaseStats[level];
    }

    public override float GetLevelPower(int level)
    {
        return PowerLevels[level];
    }

    public override ProgressionQuery QueryProgression(ActorExtend actor)
    {
        return ProgressionService.Query(this, actor);
    }

    public override bool CanScheduleProgression(ActorExtend actor)
    {
        return ProgressionService.CanSchedule(this, actor);
    }

    public override ProgressionResult TryAdvanceNaturally(ActorExtend actor)
    {
        return ProgressionService.TryAdvanceNaturally(this, actor);
    }

    /// <summary>无视自然条件，授予下一个小境界，但不改变主等级。</summary>
    public override ProgressionResult GrantNextMinor(ActorExtend actor)
    {
        return ProgressionService.GrantNextMinor(this, actor);
    }

    /// <summary>无视自然条件，逐项结算必要小境界后，完成下一个大境界的结构变换与奖励。</summary>
    public override ProgressionResult GrantNextRealm(ActorExtend actor)
    {
        return ProgressionService.GrantNextRealm(this, actor);
    }

    /// <summary>逐级授予到目标大境界，结算沿途必要小境界、结构变换和奖励；缺少过渡时拒绝执行。</summary>
    public override ProgressionResult GrantToRealm(ActorExtend actor, int targetLevel)
    {
        return ProgressionService.GrantToRealm(this, actor, targetLevel);
    }

    /// <summary>从指定大境界出发查询当前进阶图中结构完整、可逐级授予的最高大境界。</summary>
    public override int GetHighestGrantableRealm(int startLevel)
    {
        return ProgressionService.GetHighestGrantableRealm(this, startLevel);
    }

    /// <summary>把角色同步到目标大境界，尽可能执行必要结构修复，不发放奖励或触发表现。</summary>
    public override ProgressionResult SynchronizeToRealm(ActorExtend actor, int targetLevel)
    {
        return ProgressionService.SynchronizeToRealm(this, actor, targetLevel);
    }

    /// <summary>把来源角色的体系组件和体系专属结构完整传承给目标角色。</summary>
    public override ProgressionResult TransferFrom(ActorExtend source, ActorExtend target)
    {
        return ProgressionService.Transfer(this, source, target);
    }
}
