using System.Collections.Generic;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using strings;
using UnityEngine;

namespace Cultiway.Content;

[Dependency(typeof(ActorTraitGroups))]
public class ActorTraits : ExtendLibrary<ActorTrait, ActorTraits>
{
    // 拼多多砍价伤害机制的数据存储key
    private const string PDD_HISTORY_KEY = "cw.content.pdd_history";

    public static ActorTrait OpenSource { get; private set; }
    public static ActorTrait Cultivator { get; private set; }
    public static ActorTrait PassiveXianCultivate { get; private set; }
    public static ActorTrait SignIn { get; private set; }
    public static ActorTrait Pdd {get; private set;}
    [GetOnly(S_Trait.immortal)]
    public  static ActorTrait Immortal { get; private set; }

    protected override bool AutoRegisterAssets() => true;
    protected override void OnInit()
    {
        OpenSource.group_id = ActorTraitGroups.Mind.id;
        OpenSource.path_icon = "cultiway/icons/traits/iconOpenSource";

        Cultivator.group_id = ActorTraitGroups.Mind.id;
        Cultivator.path_icon = "cultiway/icons/traits/iconCultivator";

        PassiveXianCultivate.group_id = ActorTraitGroups.System.id;
        PassiveXianCultivate.path_icon = "cultiway/icons/traits/iconPassiveXianCultivate";
        PassiveXianCultivate.special_effect_interval = TimeScales.SecPerMonth;
        PassiveXianCultivate.rarity = Rarity.R3_Legendary;
        PassiveXianCultivate.action_special_effect = (actor, tile) =>
        {
            ActorExtend ae = actor.a.GetExtend();
            if (!ae.HasCultisys<Xian>()) return false;
            ref Xian xian = ref ae.GetCultisys<Xian>();
            Cultisyses.TakeWakanAndCultivate(ae, ref xian);
            if (Cultisyses.Xian.AllowUpgrade(ae)) Cultisyses.Xian.TryPerformUpgrade(ae);

            return true;
        };

        SignIn.group_id = ActorTraitGroups.System.id;
        SignIn.path_icon = "cultiway/icons/traits/iconPassiveXianCultivate";
        SignIn.special_effect_interval = TimeScales.SecPerYear;
        SignIn.rarity = Rarity.R3_Legendary;
        SignIn.action_special_effect = (actor, tile) =>
        {
            var a = actor.a;
            var ae = a.GetExtend();
            if (!ae.HasElementRoot())
            {
                ae.AddComponent(ElementRoot.Roll());
                a.setStatsDirty();
                return true;
            }

            if (!ae.HasCultisys<Xian>())
            {
                ae.NewCultisys(Cultisyses.Xian);
                a.setStatsDirty();
                return true;
            }

            if (!a.hasStatus(WorldboxGame.StatusEffects.Caffeinated.id))
            {
                a.addStatusEffect(WorldboxGame.StatusEffects.Caffeinated.id, TimeScales.SecPerYear * 100);
                return true;
            }

            if (Randy.randomBool())
            {
                ref var er = ref ae.GetComponent<ElementRoot>();
                var composition = new float[8];
                for (var i = 0; i < 8; i++) composition[i] = Mathf.Max(er[i], Mathf.Abs(RdUtils.NextStdNormal()));
                er = new ElementRoot(composition);
                a.setStatsDirty();
            }
            else
            {
                ref var xian = ref ae.GetCultisys<Xian>();
                xian.wakan = a.stats[BaseStatses.MaxWakan.id];
                if (Cultisyses.Xian.AllowUpgrade(ae))
                {
                    Cultisyses.Xian.TryPerformUpgrade(ae);
                }
            }

            return true;
        };
    
        Pdd.group_id = ActorTraitGroups.System.id;
        Pdd.path_icon = "cultiway/icons/traits/iconPdd";
        Pdd.rarity = Rarity.R3_Legendary;
        ActorExtend.RegisterActionBeforeBeAttacked((ActorExtend self, BaseSimObject attacker, ref ElementComposition damage_composition, ref AttackType attack_type, ref float damage, ref bool ignore_damage_reduction) =>
        {
            if (!self.Base.hasTrait(Pdd.id)) return;

            float currentHealth = self.Base.data.health;
            float maxHealth = self.Base.stats[S.health];
            float healthPercent = currentHealth / maxHealth;

            float currentTime = WorldboxGame.I.GetWorldTime();

            self.Base.data.get(PDD_HISTORY_KEY, out string historyData);
            var history = ParsePddHistory(historyData);

            CleanExpiredAttackers(history, currentTime);

            long attackerId = attacker?.getData().id ?? -1;
            bool isNewAttacker = !history.ContainsKey(attackerId);
            int consecutiveAttacks = isNewAttacker ? 1 : history[attackerId].count + 1;
            history[attackerId] = (consecutiveAttacks, currentTime);

            self.Base.data.set(PDD_HISTORY_KEY, SerializePddHistory(history));

            float damagePercent = CalculatePddDamagePercent(healthPercent, consecutiveAttacks, isNewAttacker);

            damage = currentHealth * damagePercent;

            float stageBoundary = CalculateStageBoundary(healthPercent);
            float maxDamage = currentHealth - (maxHealth * stageBoundary);
            
            if (currentHealth <= 1f)
            {
                damage = 0f;
            }
            else
            {
                float minDamage = Mathf.Min(1f, currentHealth - 1f); 
                float maxAllowed = Mathf.Min(maxDamage, currentHealth - 1f);
                damage = Mathf.Clamp(damage, minDamage, maxAllowed);
            }

            ignore_damage_reduction = true;
        });
    }

    protected override void PostInit(ActorTrait asset)
    {
        if (asset.group_id == ActorTraitGroups.System.id)
        {
            var list = new List<string>();
            foreach (ActorTrait trait in cached_library.list)
            {
                if (trait.id       == asset.id) continue;
                if (trait.group_id == ActorTraitGroups.System.id) list.Add(trait.id);
            }

            asset.addOpposites(list);
        }
    }

    private static Dictionary<long, (int count, float lastTime)> ParsePddHistory(string data)
    {
        var result = new Dictionary<long, (int count, float lastTime)>();
        if (string.IsNullOrEmpty(data)) return result;

        string[] entries = data.Split('|');
        foreach (string entry in entries)
        {
            if (string.IsNullOrEmpty(entry)) continue;
            
            string[] parts = entry.Split(':');
            if (parts.Length == 3)
            {
                if (long.TryParse(parts[0], out long attackerId) &&
                    int.TryParse(parts[1], out int count) &&
                    float.TryParse(parts[2], out float lastTime))
                {
                    result[attackerId] = (count, lastTime);
                }
            }
        }
        return result;
    }
    private static string SerializePddHistory(Dictionary<long, (int count, float lastTime)> history)
    {
        var parts = new List<string>();
        foreach (var kvp in history)
        {
            parts.Add($"{kvp.Key}:{kvp.Value.count}:{kvp.Value.lastTime}");
        }
        return string.Join("|", parts);
    }
    private static void CleanExpiredAttackers(
        Dictionary<long, (int count, float lastTime)> history,
        float currentTime)
    {
        float expiryTime = currentTime - TimeScales.SecPerYear;
        var expiredKeys = history
            .Where(kvp => kvp.Value.lastTime < expiryTime)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            history.Remove(key);
        }
    }
    private static float CalculateStageBoundary(float healthPercent)
    {
        int stage = Mathf.Max(0, (int)Mathf.Ceil(-Mathf.Log10(Mathf.Max(healthPercent, 0.0000001f))) - 1);
        return Mathf.Pow(0.1f, stage + 1);
    }
    private static float CalculatePddDamagePercent(
        float healthPercent,
        int consecutiveAttacks,
        bool isNewAttacker)
    {
        int stage = Mathf.Max(0, (int)Mathf.Ceil(-Mathf.Log10(Mathf.Max(healthPercent, 0.0000001f))) - 1);
        
        float stageMultiplier = Mathf.Pow(0.1f, stage);
        
        if (isNewAttacker)
        {
            float minDamage = 0.05f * stageMultiplier;
            float maxDamage = 0.15f * stageMultiplier;
            return Randy.randomFloat(minDamage, maxDamage);
        }
        else
        {
            float baseDamage = 0.15f * stageMultiplier / consecutiveAttacks;
            float minDamage = 0.01f * stageMultiplier;
            return Mathf.Max(minDamage, baseDamage);
        }
    }

}