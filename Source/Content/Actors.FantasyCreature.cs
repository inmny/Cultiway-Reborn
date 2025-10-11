using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.Attributes;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Extensions;
using Cultiway.Core;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;
using NeoModLoader.General.Game.extensions;
using strings;

namespace Cultiway.Content;

public partial class Actors
{
    // 对于一个生物，就是直接这边创建一个类似Bloodsucker的玩意，包括上面方括号的内容
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset Bloodsucker { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset Anubis { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset AcaciaTreants { get; private set; }
        [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset BanyanTreants { get; private set; }
        [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset CoconutTreants { get; private set; }
        [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset OakTreants { get; private set; }
        [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset SycamoreTreants { get; private set; }
        [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset DeathTreants { get; private set; }
        [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset FireTreants { get; private set; }
    private void SetupFantasyCreatures()
    {
        Bloodsucker.SetCamp(KingdomAssets.Vampire)//鲜血贵族
            .SetAnimIdle(S_Anim.walk_0)
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3)
            .SetAnimSwim(ActorAnimationSequences.swim_0_3)
            .SetIcon("cultiway/icons/races/iconBloodsucker")
            .Stats(S.damage, 30)
            .Stats(S.speed, 40)
            .Stats(S.health, 30);
        Anubis.SetCamp(KingdomAssets.Undead)//阿努比斯
            .SetAnimWalk(S_Anim.walk_0, S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("actors/species/other/Cultiway/Anubis/main/walk_0")
            .SetJumpAnimation(false)
            .SetDefaultWeapons(S_Item.evil_staff)
            .Stats(S.damage, 100)
            .Stats(S.scale, 0.2f);
        AcaciaTreants.SetCamp(KingdomAssets.TreantsGood)//金合欢树人
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("actors/species/other/Cultiway/AcaciaTreants/main/walk_0")
            .SetJumpAnimation(true)
            .Stats(S.damage, 28)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 6)//速度
            .Stats(S.health, 290)//血量
            .Stats(S.scale, 0.2f)//大小
            .Stats(S.armor, 12)//防御
            .Stats(S.stamina, 120)//耐力
            .Stats(S.lifespan, 180);//寿命
        BanyanTreants.SetCamp(KingdomAssets.TreantsGood)//榕树人
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("actors/species/other/Cultiway/BanyanTreants/main/walk_0")
            .SetJumpAnimation(true)
            .Stats(S.damage, 25)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 4)//速度
            .Stats(S.health, 450)//血量
            .Stats(S.scale, 0.2f)//大小
            .Stats(S.armor, 15)//防御
            .Stats(S.lifespan, 300);//寿命
        CoconutTreants.SetCamp(KingdomAssets.TreantsGood)//椰树人
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("actors/species/other/Cultiway/CoconutTreants/main/walk_0")
            .SetJumpAnimation(true)
            .Stats(S.damage, 30)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 7)//速度
            .Stats(S.health, 260)//血量
            .Stats(S.scale, 0.2f)//大小
            .Stats(S.armor, 8)//防御
            .Stats(S.lifespan, 150);//寿命
        OakTreants.SetCamp(KingdomAssets.TreantsGood)//橡树人
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("actors/species/other/Cultiway/OakTreants/main/walk_0")
            .SetJumpAnimation(true)
            .Stats(S.damage, 32)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 5)//速度
            .Stats(S.health, 380)//血量
            .Stats(S.scale, 0.2f)//大小
            .Stats(S.armor, 18)//防御
            .Stats(S.lifespan, 400);//寿命
        SycamoreTreants.SetCamp(KingdomAssets.TreantsGood)//梧桐树人
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("actors/species/other/Cultiway/SycamoreTreants/main/walk_0")
            .SetJumpAnimation(true)
            .Stats(S.damage, 22)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 9)//速度
            .Stats(S.health, 220)//血量
            .Stats(S.scale, 0.2f)//大小
            .Stats(S.armor, 5)//防御
            .Stats(S.lifespan, 120);//寿命
        DeathTreants.SetCamp(KingdomAssets.TreantsEvil)//腐朽树人
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("actors/species/other/Cultiway/DeathTreants/main/walk_0")
            .SetJumpAnimation(true)
            .Stats(S.damage, 38)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 8)//速度
            .Stats(S.health, 320)//血量
            .Stats(S.scale, 0.2f)//大小
            .Stats(S.armor, 10)//防御
            .Stats(S.lifespan, 999);//寿命
        FireTreants.SetCamp(KingdomAssets.TreantsEvil)//燃火树人
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("actors/species/other/Cultiway/FireTreants/main/walk_0")
            .SetJumpAnimation(true)
            .Stats(S.damage, 45)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 10)//速度
            .Stats(S.health, 280)//血量
            .Stats(S.scale, 0.2f)//大小
            .Stats(S.armor, 6)//防御
            .Stats(S.lifespan, 80);//寿命
            
    }
}