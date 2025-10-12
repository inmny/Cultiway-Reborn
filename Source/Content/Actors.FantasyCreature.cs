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
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset BloodBeast { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset Bloodsucker { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset Bloodthirsty { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset DestroyRobot { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset FortRobot { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset TankRobot { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset FishPeopleShaman { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset FishPeopleSoldiers { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset FishPeopleWarrior { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset FairyDruid { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset FairyRanger { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset FairyWarrior { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset GhostFire { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset CandleGenie { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset KnowledgeGenie { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset GoblinKnight { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset GoblinShaman { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset GoblinWarrior { get; private set; }
    private void SetupFantasyCreatures()
    {
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
            .SetStandWhileSleeping(true)
            .AddTrait(S_Trait.flower_prints)//花印
            .AddTrait(S_Trait.sunblessed)//阳光祝福
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
            .SetStandWhileSleeping(true)
            .AddTrait(S_Trait.flower_prints)//花印
            .AddTrait(S_Trait.sunblessed)//阳光祝福
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
            .SetStandWhileSleeping(true)
            .AddTrait(S_Trait.flower_prints)//花印
            .AddTrait(S_Trait.sunblessed)//阳光祝福
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
            .SetStandWhileSleeping(true)
            .AddTrait(S_Trait.flower_prints)//花印
            .AddTrait(S_Trait.sunblessed)//阳光祝福
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
            .SetStandWhileSleeping(true)
            .AddTrait(S_Trait.flower_prints)//花印
            .AddTrait(S_Trait.sunblessed)//阳光祝福
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
            .SetStandWhileSleeping(true)
            .AddTrait(S_Trait.evil)//邪恶
            .AddTrait(S_Trait.nightchild)//夜之孩子
            .AddTrait(S_Trait.venomous)//剧毒
            .AddTrait(S_Trait.poisonous)//剧毒
            .AddTrait(S_Trait.poison_immune)//剧毒免疫
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
            .SetStandWhileSleeping(true)
            .AddTrait(S_Trait.evil)//邪恶
            .AddTrait(S_Trait.pyromaniac)//火魔
            .AddTrait(S_Trait.hotheaded)//bold
            .AddTrait(S_Trait.burning_feet)//燃烧脚
            .AddTrait(S_Trait.fire_blood)//火血
            .AddTrait(S_Trait.fire_proof)//火抗
            .Stats(S.damage, 45)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 10)//速度
            .Stats(S.health, 280)//血量
            .Stats(S.scale, 0.2f)//大小
            .Stats(S.armor, 6)//防御
            .Stats(S.lifespan, 80);//寿命
        BloodBeast.SetCamp(KingdomAssets.TreantsEvil)//嗜血野兽
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("actors/species/other/Cultiway/BloodBeast/main/walk_0")
            .SetJumpAnimation(false)
            .AddTrait(S_Trait.evil)//邪恶
            .AddTrait(S_Trait.deceitful)//欺诈
            .AddTrait(S_Trait.bloodlust)//嗜血
            .AddTrait(S_Trait.nightchild)//夜之孩子
            .AddTrait(S_Trait.flesh_eater)//食人
            .AddTrait(S_Trait.heliophobia)// 恐光
            .Stats(S.damage, 40)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 18)//速度
            .Stats(S.health, 350)//血量
            .Stats(S.armor, 8)//防御
            .Stats(S.lifespan, 45);//寿命
        Bloodsucker.SetCamp(KingdomAssets.TreantsEvil)//鲜血贵族
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("cultiway/icons/races/iconBloodsucker")
            .SetJumpAnimation(false)
            .AddTrait(S_Trait.evil)//邪恶
            .SetDefaultWeapons(S_Item.sword_steel)
            .AddTrait(S_Trait.deceitful)//欺诈
            .AddTrait(S_Trait.bloodlust)//嗜血
            .AddTrait(S_Trait.nightchild)//夜之孩子
            .AddTrait(S_Trait.attractive)//美
            .AddTrait(S_Trait.weightless)//无重
            .AddTrait(S_SubspeciesTrait.hovering)//悬浮
            .AddTrait(S_Trait.heliophobia)// 恐光
            .Stats(S.damage, 35)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 15)//速度
            .Stats(S.health, 180)//血量
            .Stats(S.armor, 3)//防御
            .Stats(S.lifespan, 250);//寿命
        Bloodthirsty.SetCamp(KingdomAssets.TreantsEvil)//苍白血仆
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("actors/species/other/Cultiway/Bloodthirsty/main/walk_0")
            .SetJumpAnimation(false)
            .AddTrait(S_Trait.evil)//邪恶
            .AddTrait(S_Trait.deceitful)//欺诈
            .AddTrait(S_Trait.bloodlust)//嗜血
            .AddTrait(S_Trait.nightchild)//夜之孩子
            .AddTrait(S_Trait.weightless)//无重
            .AddTrait(S_SubspeciesTrait.hovering)//悬浮
            .AddTrait(S_Trait.heliophobia)// 恐光
            .Stats(S.damage, 33)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 20)//速度
            .Stats(S.health, 150)//血量
            .Stats(S.armor, 2)//防御
            .Stats(S.lifespan, 60);//寿命
        DestroyRobot.SetCamp(KingdomAssets.Robot)//毁灭机器人
            .SetAnimWalk(S_Anim.walk_0, S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("cultiway/icons/races/iconDestroy_Robot")
            .SetJumpAnimation(true)
            .SetDefaultWeapons(S_Item.evil_staff)
            .AddTrait(S_Trait.evil)//邪恶
            .AddTrait(S_Trait.bubble_defense)//泡泡防御
            .Stats(S.damage, 60)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 5)//速度
            .Stats(S.health, 800)//血量
            .Stats(S.armor, 25)//防御
            .Stats(S.lifespan, 999);//寿命
        FortRobot.SetCamp(KingdomAssets.Robot)//堡垒机器人
            .SetAnimWalk(S_Anim.walk_0, S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("cultiway/icons/races/iconFort_Robot")
            .SetJumpAnimation(false)
            .SetDefaultWeapons(S_Item.shotgun)
            .AddTrait(S_Trait.evil)//邪恶
            .AddTrait(S_Trait.bubble_defense)//泡泡防御
            .Stats(S.damage, 25)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 7)//速度
            .Stats(S.health, 500)//血量
            .Stats(S.armor, 20)//防御
            .Stats(S.lifespan, 999);//寿命
        TankRobot.SetCamp(KingdomAssets.Robot)//坦克机器人
            .SetAnimWalk(S_Anim.walk_0, S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("cultiway/icons/races/iconTank_Robot")
            .SetJumpAnimation(true)
            .SetDefaultWeapons(S_Item.alien_blaster)
            .AddTrait(S_Trait.evil)//邪恶
            .AddTrait(S_Trait.bubble_defense)//泡泡防御
            .Stats(S.damage, 50)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 3)//速度
            .Stats(S.health, 600)//血量
            .Stats(S.armor, 22)//防御
            .Stats(S.lifespan, 999);//寿命
        FishPeopleShaman.SetCamp(KingdomAssets.FishPeople)//鱼人萨满
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("cultiway/icons/races/iconFish_People_Shaman")
            .SetJumpAnimation(false)
            .SetDefaultWeapons(S_Item.druid_staff)
            .AddTrait(S_Trait.arcane_reflexes)//魔力反射
            .AddTrait(S_Trait.deceitful)//欺诈
            .AddTrait(S_Trait.savage)//野蛮
            .AddTrait(S_Trait.greedy)//贪婪
            .Stats(S.damage, 18)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 10)//速度
            .Stats(S.health, 120)//血量
            .Stats(S.armor, 1)//防御
            .Stats(S.lifespan, 40);//寿命
        FishPeopleSoldiers.SetCamp(KingdomAssets.FishPeople)//鱼人小兵
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("cultiway/icons/races/iconFish_People_Soldiers")
            .SetJumpAnimation(false)
            .AddTrait(S_Trait.deceitful)//欺诈
            .AddTrait(S_Trait.savage)//野蛮
            .AddTrait(S_Trait.greedy)//贪婪
            .AddTrait(S_Trait.battle_reflexes)//战斗反射
            .Stats(S.damage, 12)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 14)//速度
            .Stats(S.health, 90)//血量
            .Stats(S.armor, 1)//防御
            .Stats(S.lifespan, 25);//寿命
        FishPeopleWarrior.SetCamp(KingdomAssets.FishPeople)//鱼人战士
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("cultiway/icons/races/iconFish_People_Warrior")
            .SetJumpAnimation(false)
            .SetDefaultWeapons(S_Item.spear_stone)
            .AddTrait(S_Trait.deceitful)//欺诈
            .AddTrait(S_Trait.savage)//野蛮
            .AddTrait(S_Trait.greedy)//贪婪
            .AddTrait(S_Trait.battle_reflexes)//战斗反射
            .Stats(S.damage, 24)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 11)//速度
            .Stats(S.health, 160)//血量
            .Stats(S.armor, 5)//防御
            .Stats(S.lifespan, 50);//寿命
        FairyDruid.SetCamp(KingdomAssets.Fairy)//妖精德鲁伊
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetAnimIdleRaw("walk_0_0,walk_0_1")
            .SetIcon("cultiway/icons/races/iconFairy_Druid")
            .SetJumpAnimation(false)
            .SetDefaultWeapons(S_Item.druid_staff)
            .AddTrait(S_Trait.arcane_reflexes)//魔力反射
            .AddTrait(S_Trait.flower_prints)//花印
            .AddTrait(S_Trait.sunblessed)//阳光祝福
            .Stats(S.damage, 14)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 16)//速度
            .Stats(S.health, 100)//血量
            .Stats(S.armor, 1)//防御
            .Stats(S.lifespan, 120);//寿命
        FairyRanger.SetCamp(KingdomAssets.Fairy)//妖精护卫
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetAnimIdleRaw("walk_0_0,walk_0_1")
            .SetIcon("cultiway/icons/races/iconFairy_Ranger")
            .SetJumpAnimation(false)
            .SetDefaultWeapons(S_Item.bow_wood)
            .AddTrait(S_Trait.flower_prints)//花印
            .AddTrait(S_Trait.sunblessed)//阳光祝福
            .AddTrait(S_Trait.battle_reflexes)//战斗反射
            .Stats(S.damage, 23)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 18)//速度
            .Stats(S.health, 160)//血量
            .Stats(S.armor, 2)//防御
            .Stats(S.lifespan, 90);//寿命
        FairyWarrior.SetCamp(KingdomAssets.Fairy)//妖精战士
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetAnimIdleRaw("walk_0_0,walk_0_1")
            .SetIcon("cultiway/icons/races/iconFairy_Warrior")
            .SetJumpAnimation(false)
            .SetDefaultWeapons(S_Item.hammer_wood)
            .AddTrait(S_Trait.flower_prints)//花印
            .AddTrait(S_Trait.sunblessed)//阳光祝福
            .AddTrait(S_Trait.battle_reflexes)//战斗反射
            .Stats(S.damage, 23)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 18)//速度
            .Stats(S.health, 140)//血量
            .Stats(S.armor, 2)//防御
            .Stats(S.lifespan, 90);//寿命
        
            
            
    }
}