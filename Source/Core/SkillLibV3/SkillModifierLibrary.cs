using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Modifiers;
using Cultiway.Core.Semantics;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3;

public class SkillModifierLibrary : AssetLibrary<SkillModifierAsset>
{
    public static SkillModifierAsset SetTrajectory { get; private set; }
    public static SkillModifierAsset SalvoCount { get; private set; }
    public static SkillModifierAsset BurstCount { get; private set; }

    public SkillModifierAsset GetByComponentType(Type type)
    {
        return list.FirstOrDefault(asset => asset.EditorComponentType == type);
    }
    public override void init()
    {
        base.init();
        SetTrajectory = add(new SkillModifierAsset()
        {
            id = "Cultiway."+nameof(SetTrajectory),
            EvaluateLevel = SkillEvaluationActions.None
        });
        SetTrajectory.AddSemantics(SkillSemantics.Effect.Trajectory, SkillSemantics.Effect.MotionChange);
        SetTrajectory.OnSetup = ApplyTrajectoryOnSetup;
        SetTrajectory.OnAddOrUpgrade = builder =>
        {
            if (builder.HasModifier<Trajectory>())
            {
                return false;
            }

            var traj = GetRandomTrajectoryForModifier(builder.EntityAsset);
            if (traj == null)
            {
                return false;
            }

            builder.AddModifier(new Trajectory()
            {
                ID = traj.id
            });

            return true;
        };
        SetTrajectory.GetDescription = entity =>
        {
            if (entity.HasComponent<Trajectory>())
            {
                return $"{SetTrajectory.id.Localize()}: {entity.GetComponent<Trajectory>().Asset.id.Localize()}";
            }

            return null;
        };
        SalvoCount = add(new SkillModifierAsset()
        {
            id = "Cultiway."+nameof(SalvoCount),
            Rarity = SkillModifierRarity.Common,
            WeightMod = 0f,
            IsDisabled = true,
            EvaluateLevel = SkillEvaluationActions.None
        });
        SalvoCount.AddSemantics(SkillSemantics.Delivery.Projectile, SkillSemantics.Effect.Salvo);
        SalvoCount.OnAddOrUpgrade = builder =>
        {
            if (builder.HasModifier<SalvoCount>())
            {
                var modifier = builder.GetModifier<SalvoCount>();
                // 等比增长：每次升级增加当前值的 50%（向上取整）
                modifier.Value += Mathf.Max(2, Mathf.CeilToInt(Mathf.Sqrt(modifier.Value * 0.5f)));
                builder.SetModifier(modifier);
            }
            else
            {
                builder.AddModifier(new SalvoCount()
                {
                    Value = 1
                });
            }

            return true;
        };
        SalvoCount.GetDescription = entity =>
        {
            if (entity.HasComponent<SalvoCount>())
            {
                return $"{SalvoCount.id.Localize()}: {entity.GetComponent<SalvoCount>().Value}";
            }

            return null;
        };
        BurstCount = add(new SkillModifierAsset()
        {
            id = "Cultiway."+nameof(BurstCount),
            Rarity = SkillModifierRarity.Common,
            WeightMod = 0f,
            IsDisabled = true,
            EvaluateLevel = SkillEvaluationActions.None
        });
        BurstCount.AddSemantics(SkillSemantics.Delivery.Projectile, SkillSemantics.Effect.Burst);
        BurstCount.OnAddOrUpgrade = builder =>
        {
            if (builder.HasModifier<BurstCount>())
            {
                var modifier = builder.GetModifier<BurstCount>();
                // 等比增长：每次升级增加当前值的 50%（向上取整）
                modifier.Value += Mathf.Max(2, Mathf.CeilToInt(Mathf.Sqrt(modifier.Value * 0.5f)));
                builder.SetModifier(modifier);
            }
            else
            {
                builder.AddModifier(new BurstCount()
                {
                    Value = 1
                });
            }

            return true;
        };
        BurstCount.GetDescription = entity =>
        {
            if (entity.HasComponent<BurstCount>())
            {
                return $"{BurstCount.id.Localize()}: {entity.GetComponent<BurstCount>().Value}";
            }

            return null;
        };
    }

    private static TrajectoryAsset GetRandomTrajectoryForModifier(SkillEntityAsset asset)
    {
        var candidates = new List<TrajectoryAsset>();
        foreach (var trajectory in ModClass.I.SkillV3.TrajLib.list)
        {
            if (trajectory == null) continue;
            if (!trajectory.CanBeSelectedByModifier) continue;
            if (!SkillTrajectoryCompatibility.IsCompatible(asset, trajectory)) continue;
            candidates.Add(trajectory);
        }

        return candidates.Count == 0 ? null : candidates.GetRandom();
    }

    private static void ApplyTrajectoryOnSetup(Entity skillEntity)
    {
        if (!skillEntity.TryGetComponent(out SkillEntity skill)) return;
        if (skill.SkillContainer.IsNull) return;
        if (!skill.SkillContainer.TryGetComponent(out Trajectory trajectory)) return;

        if (skillEntity.HasComponent<SkillHitMemory>())
        {
            skillEntity.RemoveComponent<SkillHitMemory>();
        }

        var entityTrajectory = new Trajectory()
        {
            ID = trajectory.ID
        };
        var trajectoryAsset = entityTrajectory.Asset;
        if (trajectoryAsset == null) return;

        if (skillEntity.HasComponent<Trajectory>())
        {
            ref var current = ref skillEntity.GetComponent<Trajectory>();
            current = entityTrajectory;
        }
        else
        {
            skillEntity.AddComponent(entityTrajectory);
        }

        trajectoryAsset.OnInit?.Invoke(skillEntity);
    }
}
