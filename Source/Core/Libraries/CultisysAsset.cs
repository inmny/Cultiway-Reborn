using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Debug;
using Cultiway.Utils.Extension;
using NeoModLoader.General;

namespace Cultiway.Core.Libraries;

public abstract class BaseCultisysAsset : Asset
{
    private BaseStats[]                _level_accum_stats;
    private BaseStats[]                _level_base_stats;
    private string[]                   _level_desc_keys;
    private string[]                   _level_name_keys;
    private int                        _level_nr;
    private string[]                   _levelup_msg_keys;
    private string                     _name_key;
    public  ReadOnlyCollection<string> LevelDescKeys;
    public  ReadOnlyCollection<string> LevelNameKeys;
    public  ReadOnlyCollection<string> LevelupMsgKeys;

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
        return LM.Get(_level_name_keys[level]);
    }

    public string GetLevelDescription(int level)
    {
        return LM.Get(_level_desc_keys[level]);
    }

    public string GetLevelupMessage(int level)
    {
        var key = _levelup_msg_keys[level];
        return LMTools.Has(key) ? LM.Get(key) : "";
    }
}

public class CultisysAsset<T> : BaseCultisysAsset where T : struct, ICultisysComponent
{
    public delegate bool CheckUpgrade(ActorExtend ae, CultisysAsset<T> cultisys, ref T component);

    public delegate void OnGet(ActorExtend ae, CultisysAsset<T> cultisys, ref T component);

    public delegate void Upgrade(ActorExtend ae, CultisysAsset<T> cultisys, ref T component);

    public readonly T              DefaultComponent;
    private readonly List<string>[] _skills;
    private         Upgrade[]      _upgrade_actions;
    private         CheckUpgrade[] _upgrade_checkers;
    private         CheckUpgrade[] _upgrade_pre_checkers;

    public CultisysAsset(string id, int level_nr, T default_component, CheckUpgrade[] upgrade_pre_checkers = null,
                         CheckUpgrade[] upgrade_checkers = null, Upgrade[] upgrade_actions = null,
                         List<string>[] skills           = null) : base(id, level_nr)
    {
        _upgrade_actions = upgrade_actions           ?? new Upgrade[level_nr];
        _upgrade_pre_checkers = upgrade_pre_checkers ?? new CheckUpgrade[level_nr];
        _upgrade_checkers = upgrade_checkers         ?? new CheckUpgrade[level_nr];
        _skills = new List<string>[level_nr];

        Assert.Equals(_upgrade_actions.Length,      level_nr);
        Assert.Equals(_upgrade_checkers.Length,     level_nr);
        Assert.Equals(_upgrade_pre_checkers.Length, level_nr);
        if (skills != null)
        {
            Assert.Equals(skills.Length, level_nr);
            _skills[0] = skills[0] ?? [];
            for (var i = 1; i < level_nr; i++) _skills[i] = _skills[i - 1].Concat(skills[i] ?? []).ToList();
        }

        DefaultComponent = default_component;
        UpgradePreCheckers = Array.AsReadOnly(_upgrade_pre_checkers);
        UpgradeCheckers = Array.AsReadOnly(_upgrade_checkers);
        UpgradeActions = Array.AsReadOnly(_upgrade_actions);
        Skills = Array.AsReadOnly(_skills.Select(x => x.AsReadOnly()).ToArray());
    }

    public ReadOnlyCollection<CheckUpgrade>               UpgradePreCheckers { get; private set; }
    public ReadOnlyCollection<CheckUpgrade>               UpgradeCheckers    { get; private set; }
    public ReadOnlyCollection<Upgrade>                    UpgradeActions     { get; private set; }
    public ReadOnlyCollection<ReadOnlyCollection<string>> Skills             { get; private set; }
    public OnGet                                          OnGetAction        { get; private set; }

    public bool PreCheckUpgrade(ActorExtend ae)
    {
        if (!ae.HasCultisys<T>()) return false;
        ref var c = ref ae.GetCultisys<T>();
        if (c.CurrLevel >= LevelNumber - 1) return false;
        return _upgrade_pre_checkers[c.CurrLevel + 1]?.Invoke(ae, this, ref c) ?? false;
    }

    public bool AllowUpgrade(ActorExtend ae)
    {
        if (!ae.HasCultisys<T>()) return false;
        ref var c = ref ae.GetCultisys<T>();
        if (c.CurrLevel >= LevelNumber - 1) return false;
        return _upgrade_checkers[c.CurrLevel + 1]?.Invoke(ae, this, ref c) ?? false;
    }

    public void TryPerformUpgrade(ActorExtend ae)
    {
        if (!ae.HasCultisys<T>()) return;
        ref var c = ref ae.GetCultisys<T>();
        if (c.CurrLevel >= LevelNumber - 1) return;

        var upgrade_action = _upgrade_actions[c.CurrLevel + 1];
        if (upgrade_action == null)
        {
            c.CurrLevel++;
            ae.Base.setStatsDirty();

            ModClass.I.WorldRecord.CheckAndLogFirstLevelup(id, c.CurrLevel, ae, ref c);
        }
        else
        {
            upgrade_action.Invoke(ae, this, ref c);
        }
    }
}