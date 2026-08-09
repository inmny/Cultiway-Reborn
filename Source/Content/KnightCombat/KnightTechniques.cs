using Cultiway.Abstract;
using Cultiway.Content.Libraries;

namespace Cultiway.Content.KnightCombat;

/// <summary>注册骑士首批九个战技领域资产。</summary>
[Dependency(typeof(KnightStyles))]
public sealed class KnightTechniques : ExtendLibrary<KnightTechniqueAsset, KnightTechniques>
{
    public static KnightTechniqueAsset GuardStance { get; private set; }

    public static KnightTechniqueAsset Repulse { get; private set; }

    public static KnightTechniqueAsset GuardianBulwark { get; private set; }

    public static KnightTechniqueAsset ArmorPiercingThrust { get; private set; }

    public static KnightTechniqueAsset FormationCharge { get; private set; }

    public static KnightTechniqueAsset SkyfallStrike { get; private set; }

    public static KnightTechniqueAsset CommittedStrike { get; private set; }

    public static KnightTechniqueAsset CounterStance { get; private set; }

    public static KnightTechniqueAsset LegendaryFlurry { get; private set; }

    protected override bool AutoRegisterAssets() => true;
    protected override string Prefix() => "Cultiway.KnightTechnique";

    protected override void OnInit()
    {
        Configure(GuardStance, KnightStyles.Guardian, 0, 5f, 4f);
        Configure(Repulse, KnightStyles.Guardian, 3, 45f, 6f);
        Configure(GuardianBulwark, KnightStyles.Guardian, 7, 360f, 12f);
        Configure(ArmorPiercingThrust, KnightStyles.Lancer, 0, 5f, 3f);
        Configure(FormationCharge, KnightStyles.Lancer, 3, 45f, 7f);
        Configure(SkyfallStrike, KnightStyles.Lancer, 7, 360f, 10f);
        Configure(CommittedStrike, KnightStyles.Duelist, 0, 5f, 2.5f);
        Configure(CounterStance, KnightStyles.Duelist, 3, 45f, 8f);
        Configure(LegendaryFlurry, KnightStyles.Duelist, 7, 360f, 12f);
    }

    private static void Configure(
        KnightTechniqueAsset technique,
        KnightStyleAsset style,
        int minimumLevel,
        float vigorCost,
        float cooldown)
    {
        technique.Style = style;
        technique.NameKey = technique.id;
        technique.DescriptionKey = technique.id + ".Description";
        technique.IconPath = "cultiway/icons/iconKnight";
        technique.MinimumKnightLevel = minimumLevel;
        technique.VigorCost = vigorCost;
        technique.Cooldown = cooldown;
        // 具体执行资产与主动画像由对应战技实现步骤配置。
        technique.ActiveUse = null;
    }
}
