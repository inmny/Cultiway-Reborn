namespace Cultiway.Core.SkillLibV3;

public class SkillModifierLibrary : AssetLibrary<SkillModifierAsset>
{
    public static SkillModifierAsset SetTrajectory { get; private set; }
    public override void init()
    {
        base.init();
        SetTrajectory = add(new SkillModifierAsset()
        {
            id = nameof(SetTrajectory)
        });
    }
}