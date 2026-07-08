using System.Collections.Generic;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Modifiers;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;
using SimilarityTag = Cultiway.Core.SkillLibV3.SkillTags.Similarity;

namespace Cultiway.Core.SkillLibV3;

public class SkillModifierLibrary : AssetLibrary<SkillModifierAsset>
{
    public static SkillModifierAsset SetTrajectory { get; private set; }
    public static SkillModifierAsset SalvoCount { get; private set; }
    public static SkillModifierAsset BurstCount { get; private set; }
    public override void init()
    {
        base.init();
        SetTrajectory = add(new SkillModifierAsset()
        {
            id = "Cultiway."+nameof(SetTrajectory)
        });
        SetTrajectory.AddSimilarityTags(SimilarityTag.Trajectory, SimilarityTag.Motion);
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
            IsDisabled = true
        });
        SalvoCount.AddSimilarityTags(SimilarityTag.Projectile, SimilarityTag.Salvo);
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
            IsDisabled = true
        });
        BurstCount.AddSimilarityTags(SimilarityTag.Projectile, SimilarityTag.Burst);
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
        // 法术可接受的方向姿态；缺省按水平处理（向后兼容）。
        var accepted = asset?.AcceptedOrientations ?? TrajectoryOrientation.Horizontal;
        if (accepted == TrajectoryOrientation.None)
        {
            accepted = TrajectoryOrientation.Horizontal;
        }

        var candidates = new List<TrajectoryAsset>();
        foreach (var trajectory in ModClass.I.SkillV3.TrajLib.list)
        {
            if (trajectory == null) continue;
            if (!trajectory.CanBeSelectedByModifier) continue;
            // 方向姿态必须与法术可接受集合有交集，否则会视觉穿帮（例如把竖直播放的落雷换成水平位移）。
            if ((trajectory.Orientations & accepted) == TrajectoryOrientation.None) continue;
            candidates.Add(trajectory);
        }

        return candidates.Count == 0 ? null : candidates.GetRandom();
    }

    private static void ApplyTrajectoryOnSetup(Entity skillEntity)
    {
        if (!skillEntity.TryGetComponent(out SkillEntity skill)) return;
        if (skill.SkillContainer.IsNull) return;
        if (!skill.SkillContainer.TryGetComponent(out Trajectory trajectory)) return;

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
