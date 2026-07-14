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
