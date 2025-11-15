using Cultiway.Core.SkillLibV3.Modifiers;
using Cultiway.Utils.Extension;

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
        SetTrajectory.OnAddOrUpgrade = builder =>
        {
            if (builder.HasModifier<Trajectory>())
            {
                return false;
            }

            var traj = ModClass.I.SkillV3.TrajLib.GetRandom();
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
            id = "Cultiway."+nameof(SalvoCount)
        });
        SalvoCount.OnAddOrUpgrade = builder =>
        {
            if (builder.HasModifier<SalvoCount>())
            {
                var modifier = builder.GetModifier<SalvoCount>();
                modifier.Value++;
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
            id = "Cultiway."+nameof(BurstCount)
        });
        BurstCount.OnAddOrUpgrade = builder =>
        {
            if (builder.HasModifier<BurstCount>())
            {
                var modifier = builder.GetModifier<BurstCount>();
                modifier.Value++;
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
}