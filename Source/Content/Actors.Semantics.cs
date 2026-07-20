using Cultiway.Content.Extensions;
using Cultiway.Content.Semantics;
using Cultiway.Core.Semantics;

namespace Cultiway.Content;

public partial class Actors
{
    private static void ConfigureSourceSemantics()
    {
        Plant.AddSemantics(
            CultivationSemantics.Resource.Vitality,
            CultivationSemantics.Material.Stability);
        AcaciaTreants.AddSemantics(
            CultivationSemantics.Resource.Vitality,
            CultivationSemantics.Material.Stability);
        BanyanTreants.AddSemantics(
            CultivationSemantics.Resource.Vitality,
            CultivationSemantics.Material.Stability);
        CoconutTreants.AddSemantics(
            CultivationSemantics.Resource.Vitality,
            CultivationSemantics.Material.Stability);
        OakTreants.AddSemantics(
            CultivationSemantics.Resource.Vitality,
            CultivationSemantics.Material.Stability);
        SycamoreTreants.AddSemantics(
            CultivationSemantics.Resource.Vitality,
            CultivationSemantics.Material.Stability);
        DeathTreants.AddSemantics(
            CultivationSemantics.Resource.Vitality,
            CultivationSemantics.Material.Stability,
            SkillSemantics.Element.Poison,
            SkillSemantics.Element.Neg);
        FireTreants.AddSemantics(
            CultivationSemantics.Resource.Vitality,
            CultivationSemantics.Material.Stability,
            SkillSemantics.Element.Fire,
            CultivationSemantics.Material.Volatility);

        Train.AddSemantics(
            CultivationSemantics.Material.Hardness,
            CultivationSemantics.Material.Stability,
            CultivationSemantics.Resource.Reserve);
        DestroyRobot.AddSemantics(
            CultivationSemantics.Material.Hardness,
            CultivationSemantics.Material.Stability,
            CultivationSemantics.Resource.Reserve,
            CultivationSemantics.Material.Volatility);
        FortRobot.AddSemantics(
            CultivationSemantics.Material.Hardness,
            CultivationSemantics.Material.Stability,
            CultivationSemantics.Resource.Reserve);
        TankRobot.AddSemantics(
            CultivationSemantics.Material.Hardness,
            CultivationSemantics.Material.Stability,
            CultivationSemantics.Resource.Reserve);
        ServoSkull.AddSemantics(
            CultivationSemantics.Material.Hardness,
            CultivationSemantics.Material.Stability,
            CultivationSemantics.Resource.Reserve);
        SkullCannonKhorne.AddSemantics(
            CultivationSemantics.Material.Hardness,
            CultivationSemantics.Material.Stability,
            CultivationSemantics.Resource.Reserve,
            CultivationSemantics.Material.Volatility);

        GhostFire.AddSemantics(
            CultivationSemantics.Theme.Spirit,
            CultivationSemantics.Resource.Spirituality,
            SkillSemantics.Element.Fire);
        CandleGenie.AddSemantics(
            CultivationSemantics.Theme.Spirit,
            CultivationSemantics.Resource.Spirituality,
            SkillSemantics.Element.Fire);
        KnowledgeGenie.AddSemantics(
            CultivationSemantics.Theme.Spirit,
            CultivationSemantics.Resource.Spirituality,
            CultivationSemantics.Effect.Perception);
        NurgleSpirit.AddSemantics(
            CultivationSemantics.Theme.Spirit,
            CultivationSemantics.Resource.Spirituality,
            SkillSemantics.Element.Poison,
            SkillSemantics.Element.Neg);

        QingLong.AddSemantics(
            CultivationSemantics.Theme.Dragon,
            SkillSemantics.Element.Wood);
        FireWyvern.AddSemantics(
            CultivationSemantics.Theme.Dragon,
            SkillSemantics.Element.Fire);
    }
}
