using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Extensions;
using Cultiway.Content.Visuals;
using Cultiway.Core.Components;
using NeoModLoader.api.attributes;
using Cultiway.Core.Libraries;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Patch;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using HarmonyLib;
using strings;
using UnityEngine;

namespace Cultiway.Core;

public partial class ActorExtend : ExtendComponent<Actor>, IHasInventory, IHasStatus, IHasForce, IDisposable
{
    private static Action<ActorExtend> action_on_new_creature;

    private static Action<ActorExtend>            action_on_update_stats;
    private static Action<ActorExtend, BaseStats> action_on_rebuild_cached_stats;
    private readonly HashSet<Entity> _learned_skills_v3 = new();
    private readonly List<Entity> _learned_skill_order = new();
    private readonly BaseStats       _cached_cultiway_stats = new();
    private bool                     _cached_cultiway_stats_dirty = true;
    private bool                     _skill_cache_dirty = true;
    private Entity          e;
    public HashSet<Entity> all_skills = new();
    public List<Entity> all_attack_skills = new();
    private  Dictionary<string, Entity> _skill_action_modifiers = new();
    private  Dictionary<string, Entity> _skill_entity_modifiers = new();
    private readonly List<RecentAttackerRecord> _recent_attackers = new();
    internal float[]         s_armor        = new float[9];
    public Sect sect;
    private const float RecentAttackerLifetime = 5f;

    private Dictionary<Type, Dictionary<IDeleteWhenUnknown, float>>  _master_items = new();
    public void Master<T>(T item, float value) where T : Asset, IDeleteWhenUnknown
    {
        if (!_master_items.TryGetValue(typeof(T), out var dict))
        {
            dict = new Dictionary<IDeleteWhenUnknown, float>();
            _master_items.Add(typeof(T), dict);
        }

        if (!dict.ContainsKey(item))
        {
            item.Current++;
        }
        dict[item] = value;
    }
    public void DeMaster<T>(T item) where T : Asset, IDeleteWhenUnknown
    {
        if (!_master_items.TryGetValue(typeof(T), out var dict))
        {
            return;
        }

        if (dict.ContainsKey(item))
        {
            item.Current--;
            dict.Remove(item);
        }
    }

    public bool HasMaster<T>() where T : Asset, IDeleteWhenUnknown
    {
        return _master_items.TryGetValue(typeof(T), out var dict) && dict.Count > 0;
    }

    public float GetMaster<T>(T item) where T : Asset, IDeleteWhenUnknown
    {
        return _master_items.TryGetValue(typeof(T), out var dict) ? (dict.TryGetValue(item, out var value) ? value : 0) : 0;
    }

    public IEnumerable<(T, float)> GetAllMaster<T>() where T : Asset, IDeleteWhenUnknown
    {
        return _master_items.TryGetValue(typeof(T), out var dict) ? dict.Select(x => ((T)x.Key, x.Value)) : Array.Empty<(T, float)>();
    }
    public ActorExtend(Entity e)
    {
        this.e = e;
        e.GetComponent<ActorBinder>()._ae = this;
        _ = e.GetComponent<ActorBinder>().Actor;
    }

    public void Dispose()
    {
        if (!e.IsNull)
        {
            ModClass.I.CommandBuffer.AddTag<TagRecycle>(e.Id);
            //ModClass.LogInfo($"Disposing ActorExtend for Actor {Base.data.id} ({e})");
            foreach (var item in GetItems())
            {
                ModClass.I.CommandBuffer.AddTag<TagRecycle>(item.Id);
            }
            ModClass.I.ActorExtendManager.Remove(Base);
        }

        if (_master_items != null)
        {
            foreach (var items in _master_items.Values)
            {
                if (items != null)
                {
                    foreach (var item in items.Keys)
                    {
                        item.Current--;
                    }
                }
            }
            _master_items = null;
        }

        if (_learned_skills_v3 != null && _learned_skills_v3.Count > 0)
        {
            _learned_skills_v3.Clear();
        }
        _learned_skill_order.Clear();
        if (_recent_attackers.Count > 0)
        {
            _recent_attackers.Clear();
        }
        all_skills = null;
        all_attack_skills = null;

        if (_skill_action_modifiers != null)
        {
            foreach (var skill_action in _skill_action_modifiers.Values)
            {
                ModClass.I.CommandBuffer.AddTag<TagRecycle>(skill_action.Id);
            }

            _skill_action_modifiers = null;
        }
        if (_skill_entity_modifiers != null)
        {
            foreach (var skill_entity in _skill_entity_modifiers.Values)
            {
                ModClass.I.CommandBuffer.AddTag<TagRecycle>(skill_entity.Id);
            }

            _skill_entity_modifiers = null;
        }
    }

    public override Entity E => e;
    public override Actor Base => e.HasComponent<ActorBinder>() ? e.GetComponent<ActorBinder>().Actor : null;
    public bool TryGetComponent<TComponent>(out TComponent component) where TComponent : struct, IComponent
    {
        return e.TryGetComponent(out component);
    }
    public ref TComponent GetOrAddComponent<TComponent>() where TComponent : struct, IComponent
    {
        if (!e.HasComponent<TComponent>())
        {
            e.AddComponent(new TComponent());
        }
        return ref e.GetComponent<TComponent>();
    }

    public new void AddComponent<TComponent>(TComponent component = default) where TComponent : struct, IComponent
    {
        e.AddComponent(component);
    }

    public void MarkCultiwayStatsDirty(bool set_actor_stats_dirty = true)
    {
        _cached_cultiway_stats_dirty = true;
        if (set_actor_stats_dirty)
        {
            Base?.setStatsDirty();
        }
    }

    public void MarkCultiwaySkillCacheDirty(bool set_actor_stats_dirty = true)
    {
        _skill_cache_dirty = true;
        if (set_actor_stats_dirty)
        {
            Base?.setStatsDirty();
        }
    }
    public void GetForce(BaseSimObject source, float x, float y, float z)
    {
        var actor = Base;
        if (!actor.asset.can_be_moved_by_powers)
        {
            return;
        }
        if (actor.position_height > 0f)
        {
            return;
        }
        var power_level = GetPowerLevel();
        var source_power_level = (source?.isActor()??false) ? (source.isRekt() ? 0 : source.a.GetExtend().GetPowerLevel()) : 0;
        if (power_level > source_power_level)
        {
            x /= Mathf.Pow(DamageCalcHyperParameters.PowerBase, power_level - source_power_level);
            y /= Mathf.Pow(DamageCalcHyperParameters.PowerBase, power_level - source_power_level);
            z /= Mathf.Pow(DamageCalcHyperParameters.PowerBase, power_level - source_power_level);
        }
        var reduction = Base.stats[S.knockback_reduction];
        if (reduction >= 0)
        {
            var ratio = 1 / (1 + reduction);
            x *= ratio;
            y *= ratio;
            z *= ratio;
        }
        else
        {
            var ratio = 1 - reduction;
            x *= ratio;
            y *= ratio;
            z *= ratio;
        }

        if (x * x + y * y + z * z > 1)
        {
            actor.velocity.x = x;
            actor.velocity.y = y;
            actor.velocity.z = z;
            actor.velocity_speed = z;
            actor.under_forces = true;
        }
    }
    public float GetCultisysLevelForSort<T>() where T : struct, ICultisysComponent
    {
        return e.TryGetComponent(out T cultisys) ? cultisys.Asset.GetLevelForSort(this, cultisys.CurrLevel) : -1;
    }
    public float GetPowerLevel()
    {
        return E.TryGetComponent(out PowerLevel power_level) ? power_level.value : 0;
    }

    public void UpgradePowerLevel(float min_level)
    {
        if (E.HasComponent<PowerLevel>())
        {
            ref var power_level = ref E.GetComponent<PowerLevel>();
            power_level.value = Mathf.Max(power_level.value, min_level);
        }
        else
        {
            E.AddComponent(new PowerLevel { value = min_level });
        }
    }

    /// <summary>
    ///     防御 PowerLevel 解析器：target 在受击时可消耗资源调整自己的防御 PL。
    ///     返回 null 表示不处理（回退默认 GetPowerLevel）；返回 float 表示调整后的防御 PL。
    /// </summary>
    public delegate float? DefensePowerLevelResolver(ActorExtend target, float attacker_power_level, float damage);

    private static DefensePowerLevelResolver _defense_power_level_resolver;

    public static void RegisterDefensePowerLevelResolver(DefensePowerLevelResolver resolver)
    {
        _defense_power_level_resolver += resolver;
    }

    /// <summary>
    ///     获取用于防御判定的 PowerLevel。会触发已注册的解析器（如魔法 mana 护盾）。
    ///     无解析器处理时回退到 GetPowerLevel()。
    /// </summary>
    public float GetDefensePowerLevel(float attacker_power_level, float damage)
    {
        if (_defense_power_level_resolver == null) return GetPowerLevel();
        foreach (var d in _defense_power_level_resolver.GetInvocationList())
        {
            var adjusted = ((DefensePowerLevelResolver)d)(this, attacker_power_level, damage);
            if (adjusted.HasValue) return adjusted.Value;
        }
        return GetPowerLevel();
    }
    public void AddSpecialItem(Entity item)
    {
        item.GetIncomingLinks<EquippedArtifactRelation>().Entities
            .Do(owner => owner.RemoveRelation<EquippedArtifactRelation>(item));
        item.GetIncomingLinks<InventoryRelation>().Entities
            .Do(owner => owner.RemoveRelation<InventoryRelation>(item));
        e.AddRelation(new InventoryRelation { item = item });
        if (!item.IsNull)
        {
            SpecialItemIconVfx.QueueGain(Base, item);
        }
    }

    public void ExtractSpecialItem(Entity item)
    {
        e.RemoveRelation<EquippedArtifactRelation>(item);
        e.RemoveRelation<InventoryRelation>(item);
    }

    public bool AddSharedStatus(Entity item)
    {
        // 检查是否是负面状态
        if (item.TryGetComponent(out StatusComponent statusComponent))
        {
            var statusAsset = statusComponent.Type;
            if (statusAsset != null && statusAsset.GetExtend<StatusAssetExtend>().negative)
            {
                // 如果有施加方信息，计算powerlevel差距并调整状态时长
                var source = statusComponent.Source;
                var sourcePowerLevel = statusComponent.SourcePowerLevel;
                if (!sourcePowerLevel.HasValue && source != null && source.isActor())
                {
                    sourcePowerLevel = source.a.GetExtend().GetPowerLevel();
                }
                if (sourcePowerLevel.HasValue)
                {
                    var targetPowerLevel = GetPowerLevel();
                    var powerLevelDiff = sourcePowerLevel.Value - targetPowerLevel;

                    // 如果目标powerlevel更高，减少状态时长
                    // 差距越大，减少越多（使用对数衰减）
                    if (powerLevelDiff < 0)
                    {
                        var reductionFactor = Mathf.Exp(powerLevelDiff * 0.1f); // 每差1级，效果减少约10%
                        if (item.TryGetComponent(out AliveTimeLimit timeLimit))
                        {
                            var originalDuration = timeLimit.value;
                            var adjustedDuration = originalDuration * reductionFactor;
                            
                            // 如果调整后的时长过短（小于0.1秒），则不添加状态
                            if (adjustedDuration < 0.1f)
                            {
                                return false;
                            }
                            
                            timeLimit.value = adjustedDuration;
                        }
                    }
                }
            }
        }

        e.AddRelation(new StatusRelation { status = item });
        Base.setStatsDirty();
        return true;
    }

    public void RemoveSharedStatus(Entity item)
    {
        e.RemoveRelation<StatusRelation>(item);
        Base.setStatsDirty();
    }

    public List<Entity> GetStatuses()
    {
        var status_rels = e.GetRelations<StatusRelation>();
        var list = new List<Entity>(status_rels.Length);
        for (int i = 0; i < status_rels.Length; i++)
        {
            list.Add(status_rels[i].status);
        }
        return list;
    }

    public IEnumerable<Entity> GetItems()
    {
        var rels = e.GetRelations<InventoryRelation>();
        var rel_count = rels.Length;
        for (int i = 0; i < rel_count; i++)
        {
            yield return rels[i].item;
        }
        yield break;
    }

    public static void RegisterActionOnNewCreature(Action<ActorExtend> action)
    {
        action_on_new_creature += action;
    }

    public override string ToString()
    {
        return $"[{e.GetComponent<ActorBinder>().ID}] {Base.getName()}: {e}";
    }
    public void LearnSkillV3(Entity skill_container, bool clone = false)
    {
        SkillOwnershipService.Learn(this, skill_container, clone);
    }

    public bool ForgetSkillV3(Entity skillContainer)
    {
        return SkillOwnershipService.Forget(this, skillContainer) == SkillOwnershipResult.Forgotten;
    }

    public bool ReplaceSkillV3(Entity oldContainer, Entity newContainer)
    {
        return SkillOwnershipService.Replace(this, oldContainer, newContainer) == SkillOwnershipResult.Replaced;
    }

    internal bool OwnsLearnedSkill(Entity container)
    {
        return _learned_skills_v3.Contains(container);
    }

    internal IReadOnlyList<Entity> GetLearnedSkillsInOrder()
    {
        return _learned_skill_order;
    }

    internal void AttachLearnedSkill(Entity container)
    {
        if (!_learned_skills_v3.Add(container)) return;
        _learned_skill_order.Add(container);
        E.AddRelation(new SkillMasterRelation { SkillContainer = container });
        MarkCultiwaySkillCacheDirty();
    }

    internal bool DetachLearnedSkill(Entity container)
    {
        if (!_learned_skills_v3.Remove(container)) return false;
        _learned_skill_order.Remove(container);
        E.RemoveRelation<SkillMasterRelation>(container);
        MarkCultiwaySkillCacheDirty();
        return true;
    }

    internal bool ReplaceLearnedSkill(Entity oldContainer, Entity newContainer)
    {
        var index = _learned_skill_order.IndexOf(oldContainer);
        if (index < 0 || !_learned_skills_v3.Add(newContainer)) return false;

        E.AddRelation(new SkillMasterRelation { SkillContainer = newContainer });
        E.RemoveRelation<SkillMasterRelation>(oldContainer);
        _learned_skills_v3.Remove(oldContainer);
        _learned_skill_order[index] = newContainer;
        MarkCultiwaySkillCacheDirty();
        return true;
    }

    public SpecialItem GetRandomSpecialItem(Func<Entity, bool> filter)
    {
        using var pool = new ListPool<SpecialItem>(GetItems()
            .Select(x => x.GetComponent<SpecialItem>()).Where(x => filter(x.self)));
        if (pool.Any())
            return pool.GetRandom();
        return default;
    }

    public Entity GetSkillActionModifiers(string action_id, Entity default_modifiers)
    {
        return _skill_action_modifiers.TryGetValue(action_id, out var res) ? res : default_modifiers;
    }
    public Entity GetSkillEntityModifiers(string entity_id, Entity default_modifiers)
    {
        return _skill_entity_modifiers.TryGetValue(entity_id, out var res) ? res : default_modifiers;
    }

    private Actor FindTestTarget()
    {
        Actor target = null;
        int count = 0;
        do
        {
            target = World.world.units.GetRandom();
            if (++count > 100)
            {
                ModClass.LogInfo($"{Base.data.id} failed to find enemy");
                throw new Exception();
            }
        } while (!Base.kingdom.isEnemy(target.kingdom));

        return target;
    }

    private void TestCastFireballV3()
    {
        var target = FindTestTarget();

        var skill_container = new SkillContainerBuilder(SkillEntities.Fireball).Build();
        ModClass.I.SkillV3.SpawnSkill(skill_container, Base, target, 1);
    }
    private void TestConsumeEnlightenElixir()
    {
        var elixir = SpecialItemUtils.StartBuild(ItemShapes.Ball, World.world.getCurWorldTime(), Base.getName())
            .AddComponent(new Elixir()
            {
                elixir_id = Elixirs.EnlightenElixir.id
            })
            .Build();
        this.TryConsumeElixir(elixir);
    }
    public static void RegisterActionOnGetStats(Action<ActorExtend, string> action)
    {
        action_on_get_stats += action;
    }

    private static Action<ActorExtend, string> action_on_get_stats;
    private static Action<ActorExtend, Actor, Kingdom> action_on_kill;
    private static Action<ActorExtend, BaseSimObject, float> action_on_be_attacked;
    public delegate void ActionBeforeBeAttacked(ActorExtend self, BaseSimObject attacker, ref ElementComposition damage_composition, ref AttackType attack_type, ref float damage, ref bool ignore_damage_reduction);
    private static ActionBeforeBeAttacked action_before_be_attacked;
    public float GetStat(string stat_id)
    {
        action_on_get_stats?.Invoke(this, stat_id);
        return Base.stats[stat_id];
    }
    private void CreateTalent()
    {
        // 灵根
        var has_element_root = Base.asset.GetExtend<ActorAssetExtend>().must_have_element_root ||
                                Randy.randomChance(GeneralSettings.SpawnNaturally);
        if (has_element_root)
        {
            e.AddComponent(ElementRoot.Roll());
        }

        e.AddComponent(ValuableTalent.Roll());
    }
    internal void ExtendNewCreature()
    {
        CreateTalent();
        action_on_new_creature?.Invoke(this);
    }

    public static void RegisterActionOnUpdateStats(Action<ActorExtend> action)
    {
        action_on_update_stats += action;
    }

    public static void RegisterCachedStatsBuilder(Action<ActorExtend, BaseStats> action)
    {
        action_on_rebuild_cached_stats += action;
    }

    [Hotfixable]
    internal void ExtendUpdateStats()
    {
        foreach (var status_entity in GetStatuses())
        {
            if (status_entity.HasComponent<StatusOverwriteStats>())
            {
                ref var status_stats = ref status_entity.GetComponent<StatusOverwriteStats>().stats;
                if (status_stats != null)
                    Base.stats.mergeStats(status_entity.GetComponent<StatusOverwriteStats>().stats);
            }
            else
            {
                if (status_entity.TryGetComponent(out StatusStatsMultiplier mul))
                {
                    Base.stats.MergeStats(status_entity.GetComponent<StatusComponent>().Type.stats, mul.Value);
                }
                else
                {
                    Base.stats.mergeStats(status_entity.GetComponent<StatusComponent>().Type.stats);
                }
            }
        }

        RebuildCultiwayStatsCacheIfDirty();
        Base.stats.mergeStats(_cached_cultiway_stats);

        RebuildSkillCacheIfDirty();

        action_on_update_stats?.Invoke(this);
    }

    private void RebuildCultiwayStatsCacheIfDirty()
    {
        if (!_cached_cultiway_stats_dirty) return;

        _cached_cultiway_stats.clear();

        if (HasElementRoot())
        {
            _cached_cultiway_stats.mergeStats(GetElementRoot().Stats);
        }
        if (E.TryGetComponent(out ValuableTalent valuable_talent))
        {
            _cached_cultiway_stats.mergeStats(valuable_talent.Stats);
        }

        if (E.TryGetComponent(out Qiyun qiyun))
        {
            _cached_cultiway_stats[nameof(WorldboxGame.BaseStats.MaxQiyun)] += qiyun.MaxValue;
        }

        if (E.TryGetComponent(out PermanentStats permanent_stats))
        {
            _cached_cultiway_stats.mergeStats(permanent_stats.Stats);
        }

        action_on_rebuild_cached_stats?.Invoke(this, _cached_cultiway_stats);

        _cached_cultiway_stats_dirty = false;
    }

    private void RebuildSkillCacheIfDirty()
    {
        if (!_skill_cache_dirty) return;

        all_skills.Clear();
        all_skills.UnionWith(_learned_skills_v3);

        all_attack_skills.Clear();

        foreach (var skill in all_skills)
        {
            if (skill.GetComponent<SkillContainer>().Asset.Type == SkillEntityType.Attack)
            {
                all_attack_skills.Add(skill);
            }
        }

        _skill_cache_dirty = false;
    }

    internal void PostUpdateStats()
    {
        var stats = Base.stats;

        var armor = Mathf.Max(stats[S.armor], 0);
        s_armor[ElementIndex.Entropy + 1] = armor / (armor + DamageCalcHyperParameters.ArmorEffectDecay);
        float master = 0;
        for (var i = 0; i < 8; i++)
        {
            armor = stats[WorldboxGame.BaseStats.ArmorStats[i]];
            master = stats[WorldboxGame.BaseStats.MasterStats[i]];
            s_armor[i] = armor / (armor + DamageCalcHyperParameters.ArmorEffectDecay) * master /
                         (master + DamageCalcHyperParameters.MasterEffectDecay);
        }
    }

    public void NewCultisys<T>(CultisysAsset<T> cultisys) where T : struct, ICultisysComponent
    {
        e.AddComponent(cultisys.DefaultComponent);
        cultisys.OnGetAction?.Invoke(this, cultisys, ref e.GetComponent<T>());
        MarkCultiwayStatsDirty();
    }

    public bool HasElementRoot()
    {
        return e.HasComponent<ElementRoot>();
    }

    public ref ElementRoot GetElementRoot()
    {
        return ref e.GetComponent<ElementRoot>();
    }

    public bool HasCultisys<T>() where T : struct, ICultisysComponent
    {
        return e.HasComponent<T>();
    }

    public ref T GetCultisys<T>() where T : struct, ICultisysComponent
    {
        return ref e.GetComponent<T>();
    }

    public Entity GetFirstItemWithComponent<TComponent>() where TComponent : struct, IComponent
    {
        return GetItems().FirstOrDefault(x => x.HasComponent<TComponent>());
    }

    public bool HasItem<TComponent>() where TComponent : struct, IComponent
    {
        return GetItems().Any(x => x.HasComponent<TComponent>());
    }
    public static void RegisterActionOnKill(Action<ActorExtend, Actor, Kingdom> action)
    {
        action_on_kill += action;
    }
    
    public static void RegisterActionOnBeAttacked(Action<ActorExtend, BaseSimObject, float> action)
    {
        action_on_be_attacked += action;
    }
    public static void RegisterActionBeforeBeAttacked(ActionBeforeBeAttacked action)
    {
        action_before_be_attacked += action;
    }
    
    public void NewKillAction(Actor dead_unit, Kingdom dead_kingdom)
    {
        using var pool = new ListPool<Entity>(dead_unit.GetExtend().GetItems());
        foreach (var item in pool)
        {
            if (item.Tags.HasAny(Tags.Get<TagOccupied, TagConsumed, TagUncompleted>()))
            {
                item.AddTag<TagRecycle>();
                continue;
            }
            AddSpecialItem(item);
        }
        action_on_kill?.Invoke(this, dead_unit, dead_kingdom);
    }

    public bool HasRelatedForce<TRelation>() where TRelation : struct, IForceRelation
    {
        return E.GetRelations<TRelation>().Length > 0;
    }
    public IEnumerable<Entity> GetForces<TRelation>() where TRelation : struct, IForceRelation
    {
        var rels = E.GetRelations<TRelation>();
        var rel_count = rels.Length;
        for (int i = 0; i < rel_count; i++)
        {
            yield return rels[i].GetRelationKey();  
        }
        yield break;
    }

    public void JoinForce<TRelation>(Entity force) where TRelation : struct, IForceRelation
    {
        E.AddRelation(new TRelation { ForceEntity = force });
    }
    public void ExitForce<TRelation>(Entity force) where TRelation : struct, IForceRelation
    {
        E.RemoveRelation<TRelation>(force);
    }

    private static Action<ActorExtend> action_on_death;

    public static void RegisterActionOnDeath(Action<ActorExtend> action)
    {
        action_on_death += action;
    }
    public void OnDeath()
    {
        action_on_death?.Invoke(this);
        using var pool = new ListPool<Entity>(GetItems());
        if (Base.hasCity())
        {
            var ce = Base.GetExtend();
            foreach (var item in pool)
            {
                if (item.Tags.HasAny(Tags.Get<TagOccupied, TagConsumed, TagUncompleted>()))
                {
                    item.AddTag<TagRecycle>();
                    continue;
                }
                ce.AddSpecialItem(item);
            }
        }
    }
    /// <summary>
    /// 复制修仙里头所有的数据
    /// </summary>
    /// <param name="clone_source"></param>
    [Hotfixable]
    public void CloneLeftFrom(ActorExtend clone_source)
    {
        var self = Base;
        var source = clone_source.Base;

        #region 一般数据复制

        source.data.save();
        if (source.data.custom_data_bool != null)
        {
            self.data.custom_data_bool = new()
            {
                dict = new(source.data.custom_data_bool.dict)
            };
        }

        if (source.data.custom_data_float != null)
        {
            self.data.custom_data_float = new()
            {
                dict = new(source.data.custom_data_float.dict)
            };
        }

        if (source.data.custom_data_int != null)
        {
            self.data.custom_data_int = new()
            {
                dict = new(source.data.custom_data_int.dict)
            };
        }

        if (source.data.custom_data_string != null)
        {
            self.data.custom_data_string = new()
            {
                dict = new(source.data.custom_data_string.dict)
            };
        }

        if (source.data.custom_data_flags != null)
        {
            self.data.custom_data_flags = new(source.data.custom_data_flags);
        }

        #endregion

        #region 实体复制

        var old_binder = E.GetComponent<ActorBinder>();
        var cloned_entity = E.Store.CloneEntity(clone_source.E);
        cloned_entity.AddComponent(old_binder);

        E.AddTag<TagRecycle>();
        e = cloned_entity;
        #endregion

        // TODO: 各种Relation复制(比如背包/自制势力)

        #region 技能相关复制

        _skill_action_modifiers.Clear();
        _skill_entity_modifiers.Clear();

        foreach (var item in clone_source._skill_action_modifiers)
        {
            _skill_action_modifiers[item.Key] = item.Value.Store.CloneEntity(item.Value);
        }

        foreach (var item in clone_source._skill_entity_modifiers)
        {
            _skill_entity_modifiers[item.Key] = item.Value.Store.CloneEntity(item.Value);
        }

        #endregion
        
    }

    public void SetSect(Sect new_sect)
    {
        WorldboxGame.I.Sects.setDirtyUnits(sect);
        if (new_sect == null)
        {
            E.RemoveComponent<SectJobState>();
        }
        else
        {
            E.AddComponent(new SectJobState { SectId = -1 });
        }
        sect = new_sect;
        WorldboxGame.I.Sects.unitAdded(new_sect);
        Base.setStatsDirty();
    }

    public bool CastSkillV3(Entity skill, BaseSimObject target, float strength = 100, float? power_level = null,
        SkillCastCostSource cost_source = SkillCastCostSource.CasterWakan)
    {
        if (!GeneralSettings.EnableSkillSystems) return false;
        if (!skill.HasComponent<SkillContainer>())
        {
            ModClass.LogError($"技能实体{skill}不包含技能");
            return false;
        }

        var stepLimit = SkillCastCost.GetAffordableStepLimit(this, skill, cost_source);
        var plan = SkillCastPlanner.CreatePlan(this, skill, target, stepLimit);
        if (plan.Steps.Count == 0) return false;

        return ModClass.I.SkillV3.StartSkillSequence(this, skill, plan, strength, power_level ?? GetPowerLevel(),
            cost_source);
    }

    // ======== 师徒系统核心方法（不依赖Content） ========
    
    /// <summary>
    /// 获取所有弟子
    /// </summary>
    public List<ActorExtend> GetApprentices()
    {
        var apprentices = new List<ActorExtend>();
        if (E.IsNull) return apprentices;
        
        // 查找所有指向此Entity的MasterApprenticeRelation
        var allActors = World.world.units.units_only_alive;
        foreach (var actor in allActors)
        {
            var apprenticeAe = actor.GetExtend();
            if (apprenticeAe.HasMaster() && apprenticeAe.GetMaster() == Base)
            {
                apprentices.Add(apprenticeAe);
            }
        }
        
        return apprentices;
    }
    
    /// <summary>
    /// 检查是否有师傅
    /// </summary>
    [Hotfixable]
    public bool HasMaster()
    {
        if (E.IsNull) return false;
        var relations = E.GetRelations<MasterApprenticeRelation>();
        if (relations.Length == 0) return false;
        var relation = relations[0];
        // 检查师傅是否还存在
        if (relation.Master.IsNull) return false;
        var masterActor = relation.Master.GetComponent<ActorBinder>().Actor;
        if (masterActor.isRekt()) return false;
        return true;
    }
    
    /// <summary>
    /// 获取师傅
    /// </summary>
    [Hotfixable]
    public Actor GetMaster()
    {
        if (!HasMaster()) return null;
        
        var relations = E.GetRelations<MasterApprenticeRelation>();
        if (relations.Length == 0) return null;
        
        var masterEntity = relations[0].Master;
        if (masterEntity.IsNull) return null;
        
        var masterBinder = masterEntity.GetComponent<ActorBinder>();
        
        return masterBinder.Actor;
    }
    
    /// <summary>
    /// 获取师徒关系
    /// </summary>
    public ref MasterApprenticeRelation GetMasterRelation()
    {
        var relations = E.GetRelations<MasterApprenticeRelation>();
        var relation = relations[0];

        return ref E.GetRelation<MasterApprenticeRelation, Entity>(relation.Master);
    }
    
    /// <summary>
    /// 增加亲密度
    /// </summary>
    [Hotfixable]
    public void AddIntimacy(float amount)
    {
        if (!HasMaster()) return;
        
        ref var relation = ref GetMasterRelation();
        relation.Intimacy = Mathf.Clamp(relation.Intimacy + amount, 0, 100);
        
        // 检查是否升级关系类型
        UpdateRelationType(ref relation);
    }
    
    /// <summary>
    /// 获取亲密度
    /// </summary>
    public float GetIntimacy()
    {
        if (!HasMaster()) return 0;
        ref var relation = ref GetMasterRelation();
        return relation.Intimacy;
    }
    
    /// <summary>
    /// 获取关系类型
    /// </summary>
    public MasterApprenticeTypeAsset GetRelationType()
    {
        if (!HasMaster()) return MasterApprenticeTypes.Nominal;
        ref var relation = ref GetMasterRelation();
        return ModClass.L.MasterApprenticeTypeLibrary.GetOrDefault(relation.RelationTypeId);
    }
    
    /// <summary>
    /// 更新关系类型（根据亲密度）
    /// </summary>
    private static void UpdateRelationType(ref MasterApprenticeRelation relation)
    {
        relation.RelationTypeId = ModClass.L.MasterApprenticeTypeLibrary
            .GetByIntimacy(relation.Intimacy, relation.IsSuccessor)
            .id;
    }

    private static Action<Actor> action_on_addchildren;

    public static void RegisterPossibleChildren<TActorComponent>() where TActorComponent : BaseActorComponent
    {
        action_on_addchildren += (Actor a) => {
            if (a.avatar?.HasComponent<TActorComponent>() ?? false)
            {
                a.addChild(a.avatar.GetComponent<TActorComponent>());
            }
        };
    }
    internal void OnAddChildren()
    {
        action_on_addchildren?.Invoke(Base);
        
		if (Base.children_pre_behaviour != null || Base.children_special != null)
		{
			Base.batch.c_update_children.Add(Base);
		}
    }
}
