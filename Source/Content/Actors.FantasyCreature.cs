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
using UnityEngine;

namespace Cultiway.Content;

public partial class Actors
{
    // 对于一个生物，就是直接这边创建一个类似Bloodsucker的玩意，包括上面方括号的内容
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
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset Mummy { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset Pharaoh { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset Anubis { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset Ossaurus { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset SkeletonKnight { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset Sphinx { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset Diplodocus { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset Dreadnoughtus { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset Triceratops { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset TyrannosaurusRex { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset Pterodactyl { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset Velociraptor { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset IceGiant { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset LavaGiant { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset RockGiant { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset VolcanicGiant { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset GriffinKnight { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset Sorcerer { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset GuardKnight { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset FairyFox { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset FengHuang { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset JinWu { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset QingLong { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset QiLin { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset NineColoredDeer { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset WhiteTiger { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset XuanWu { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset YuChan { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset YueTu { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset ZhuQue { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset FireWyvern { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset VampireHunter { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset HalfDeerMan { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset Mermaid { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset Centaur { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset KingSlime { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset GiantOctopus { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset Werewolf { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset Deer { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset Horse { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset Panda { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset Pig { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset WildBoar { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset Rooster { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset Eagle { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset Mallard { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset Lion { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset Tiger { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset UncleanCreature { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset NurgleSpirit { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset NurgleDiseaseCarrier { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset PlagueBringer { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset GreatUncleanOneButcher { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset PlagueToad { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset GreatUncleanOneBellRinger { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset GreatUncleanOneRainFather { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset Daemonette { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset Hellflayer { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset SlaaneshSeeker { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset SlaaneshMistress { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset SlaaneshFiend { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset KeeperSecrets { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset KeeperSecretsNakari { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset ExaltedKeeperSecrets { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset  CravingManifestation { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset CrimsonScion { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset CrimsonArbiter { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset Cherub { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset ServoSkull { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset TechPriests { get; private set; }
    [SetupButton, CommonCreatureSetup, CloneSource(ActorAssetLibrary.TEMPLATE_BASIC_UNIT_COLORED)]
    public static ActorAsset Emperor { get; private set; }
    
    private void SetupFantasyCreatures()
    {
        AcaciaTreants.SetCamp(KingdomAssets.TreantsGood)//金合欢树人
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("actors/species/other/Cultiway/AcaciaTreants/main/walk_0")
            .SetJumpAnimation(true)
            .SetStandWhileSleeping(true)
            .AddTrait(S_Trait.flower_prints)//花印
            .AddTrait(S_Trait.sunblessed)//阳光祝福
            .AddTrait(S_Trait.regeneration)//回复
            .AddSubspeciesTrait(S_SubspeciesTrait.death_grow_tree)//死亡长树
            .AddSubspeciesTrait(S_SubspeciesTrait.photosynthetic_skin)//photosynthetic_skin
            .AddSubspeciesTrait(S_SubspeciesTrait.reproduction_vegetative)//生长性 reproduce_vegetative
            .AddSubspeciesTrait(S_SubspeciesTrait.accelerated_healing)//回复
            .Stats(S.damage, 28)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 6)//速度
            .Stats(S.health, 290)//血量
            .Stats(S.scale, 0.25f)//大小
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
            .AddTrait(S_Trait.regeneration)//回复
            .AddSubspeciesTrait(S_SubspeciesTrait.death_grow_tree)//死亡长树
            .AddSubspeciesTrait(S_SubspeciesTrait.photosynthetic_skin)//photosynthetic_skin
            .AddSubspeciesTrait(S_SubspeciesTrait.reproduction_vegetative)//生长性 reproduce_vegetative
            .AddSubspeciesTrait(S_SubspeciesTrait.accelerated_healing)//回复
            .Stats(S.damage, 25)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 4)//速度
            .Stats(S.health, 450)//血量
            .Stats(S.scale, 0.25f)//大小
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
            .AddTrait(S_Trait.regeneration)//回复
            .AddSubspeciesTrait(S_SubspeciesTrait.death_grow_tree)//死亡长树
            .AddSubspeciesTrait(S_SubspeciesTrait.photosynthetic_skin)//photosynthetic_skin
            .AddSubspeciesTrait(S_SubspeciesTrait.reproduction_vegetative)//生长性 reproduce_vegetative
            .AddSubspeciesTrait(S_SubspeciesTrait.accelerated_healing)//回复
            .Stats(S.damage, 30)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 7)//速度
            .Stats(S.health, 260)//血量
            .Stats(S.scale, 0.25f)//大小
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
            .AddTrait(S_Trait.regeneration)//回复
            .AddSubspeciesTrait(S_SubspeciesTrait.death_grow_tree)//死亡长树
            .AddSubspeciesTrait(S_SubspeciesTrait.photosynthetic_skin)//photosynthetic_skin
            .AddSubspeciesTrait(S_SubspeciesTrait.reproduction_vegetative)//生长性 reproduce_vegetative
            .AddSubspeciesTrait(S_SubspeciesTrait.accelerated_healing)//回复
            .Stats(S.damage, 32)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 5)//速度
            .Stats(S.health, 380)//血量
            .Stats(S.scale, 0.25f)//大小
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
            .AddTrait(S_Trait.regeneration)//回复
            .AddSubspeciesTrait(S_SubspeciesTrait.death_grow_tree)//死亡长树
            .AddSubspeciesTrait(S_SubspeciesTrait.photosynthetic_skin)//photosynthetic_skin
            .AddSubspeciesTrait(S_SubspeciesTrait.reproduction_vegetative)//生长性 reproduce_vegetative
            .AddSubspeciesTrait(S_SubspeciesTrait.accelerated_healing)//回复
            .Stats(S.damage, 22)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 9)//速度
            .Stats(S.health, 220)//血量
            .Stats(S.scale, 0.25f)//大小
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
            .AddTrait(S_Trait.regeneration)//回复
            .AddSubspeciesTrait(S_SubspeciesTrait.photosynthetic_skin)//photosynthetic_skin
            .AddSubspeciesTrait(S_SubspeciesTrait.reproduction_vegetative)//生长性 reproduce_vegetative
            .AddSubspeciesTrait(S_SubspeciesTrait.accelerated_healing)//回复
            .Stats(S.damage, 38)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 8)//速度
            .Stats(S.health, 320)//血量
            .Stats(S.scale, 0.25f)//大小
            .Stats(S.armor, 10)//防御
            .Stats(S.lifespan, 999);//寿命
        FireTreants.SetCamp(KingdomAssets.TreantsEvil)//燃火树人
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("actors/species/other/Cultiway/FireTreants/main/walk_0")
            .SetJumpAnimation(true)
            .SetStandWhileSleeping(true)
            .AddTrait(S_Trait.light_lamp)
            .AddTrait(S_Trait.evil)//邪恶
            .AddTrait(S_Trait.pyromaniac)//火魔
            .AddTrait(S_Trait.hotheaded)//bold
            .AddTrait(S_Trait.burning_feet)//燃烧脚
            .AddTrait(S_Trait.fire_blood)//火血
            .AddTrait(S_Trait.fire_proof)//火抗
            .AddTrait(S_Trait.light_lamp)//光 lamp
            .AddTrait(S_Trait.regeneration)//回复
            .AddSubspeciesTrait(S_SubspeciesTrait.photosynthetic_skin)//photosynthetic_skin
            .AddSubspeciesTrait(S_SubspeciesTrait.reproduction_vegetative)//生长性 reproduce_vegetative
            .AddSubspeciesTrait(S_SubspeciesTrait.accelerated_healing)//回复
            .Stats(S.damage, 45)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 10)//速度
            .Stats(S.health, 280)//血量
            .Stats(S.scale, 0.25f)//大小
            .Stats(S.armor, 6)//防御
            .Stats(S.lifespan, 80);//寿命
        BloodBeast.SetCamp(KingdomAssets.Vampire)//嗜血野兽
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
            .AddSubspeciesTrait(S_SubspeciesTrait.stomach)//胃
            .AddSubspeciesTrait(S_SubspeciesTrait.diet_hematophagy)//hematophagy
            .AddSubspeciesTrait(S_SubspeciesTrait.gift_of_blood)//吸血
            .AddSubspeciesTrait(S_SubspeciesTrait.circadian_drift)//循环
            .AddSubspeciesTrait(S_SubspeciesTrait.energy_preserver)//保护
            .AddSubspeciesTrait(S_SubspeciesTrait.reproduction_metamorph)//蜕变
            .AddSubspeciesTrait(S_SubspeciesTrait.hovering)//悬浮
            .Stats(S.damage, 40)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 18)//速度
            .Stats(S.health, 350)//血量
            .Stats(S.armor, 8)//防御
            .Stats(S.lifespan, 45);//寿命
        Bloodsucker.SetCamp(KingdomAssets.Vampire)//鲜血贵族
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("cultiway/icons/races/iconBloodsucker")
            .SetJumpAnimation(false)
            .AddTrait(S_Trait.evil)//邪恶
            .AddTrait(S_Trait.deceitful)//欺诈
            .AddTrait(S_Trait.bloodlust)//嗜血
            .AddTrait(S_Trait.nightchild)//夜之孩子
            .AddTrait(S_Trait.attractive)//美
            .AddTrait(S_Trait.weightless)//无重
            .AddTrait(S_SubspeciesTrait.hovering)//悬浮
            .AddTrait(S_Trait.heliophobia)// 恐光
            .AddJob(ActorJobs.SpawnedUnit)//召唤单位
            .AddSubspeciesTrait(S_SubspeciesTrait.stomach)//胃
            .AddSubspeciesTrait(S_SubspeciesTrait.diet_hematophagy)//hematophagy
            .AddSubspeciesTrait(S_SubspeciesTrait.gift_of_blood)//吸血
            .AddSubspeciesTrait(S_SubspeciesTrait.circadian_drift)//循环
            .AddSubspeciesTrait(S_SubspeciesTrait.energy_preserver)//保护
            .AddSubspeciesTrait(S_SubspeciesTrait.reproduction_metamorph)//蜕变
            .AddSubspeciesTrait(S_SubspeciesTrait.hovering)//悬浮
            .Stats(S.damage, 35)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 15)//速度
            .Stats(S.health, 180)//血量
            .Stats(S.armor, 3)//防御
            .Stats(S.lifespan, 250);//寿命
        Bloodthirsty.SetCamp(KingdomAssets.Vampire)//苍白血仆
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
            .AddSubspeciesTrait(S_SubspeciesTrait.stomach)//胃
            .AddSubspeciesTrait(S_SubspeciesTrait.diet_hematophagy)//hematophagy
            .AddSubspeciesTrait(S_SubspeciesTrait.gift_of_blood)//吸血
            .AddSubspeciesTrait(S_SubspeciesTrait.circadian_drift)//循环
            .AddSubspeciesTrait(S_SubspeciesTrait.energy_preserver)//保护
            .AddSubspeciesTrait(S_SubspeciesTrait.reproduction_metamorph)//蜕变
            .AddSubspeciesTrait(S_SubspeciesTrait.hovering)//悬浮
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
            .SetStandWhileSleeping(true)
            .SetDefaultWeapons(S_Item.evil_staff)
            .AddTrait(S_Trait.evil)//邪恶
            .AddTrait(S_Trait.death_nuke)//毁灭核
            .AddTrait(S_Trait.bubble_defense)//泡泡防御
            .AddTrait(S_Trait.light_lamp)
            .AddJob(ActorJobs.SpawnedUnit)//召唤单位
            .AddSubspeciesTrait(S_SubspeciesTrait.stomach)//胃
            .AddSubspeciesTrait(S_SubspeciesTrait.diet_lithotroph)//吸光
            .AddSubspeciesTrait(S_SubspeciesTrait.reproduction_fission)//分离
            .AddSubspeciesTrait(S_SubspeciesTrait.hydrophobia)//恐水
            .AddSubspeciesTrait(S_SubspeciesTrait.cold_resistance)//冷抗
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
            .SetStandWhileSleeping(true)
            .SetDefaultWeapons(S_Item.shotgun)
            .AddTrait(S_Trait.evil)//邪恶
            .AddTrait(S_Trait.death_bomb)//死亡炸弹
            .AddTrait(S_Trait.bubble_defense)//泡泡防御
            .AddTrait(S_Trait.light_lamp)
            .AddSubspeciesTrait(S_SubspeciesTrait.stomach)//胃
            .AddSubspeciesTrait(S_SubspeciesTrait.diet_lithotroph)//吸光
            .AddSubspeciesTrait(S_SubspeciesTrait.reproduction_fission)//分离
            .AddSubspeciesTrait(S_SubspeciesTrait.hydrophobia)//恐水
            .AddSubspeciesTrait(S_SubspeciesTrait.cold_resistance)//冷抗
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
            .SetStandWhileSleeping(true)
            .SetDefaultWeapons(S_Item.alien_blaster)
            .AddTrait(S_Trait.evil)//邪恶
            .AddTrait(S_Trait.death_bomb)//死亡炸弹
            .AddTrait(S_Trait.bubble_defense)//泡泡防御
            .AddTrait(S_Trait.light_lamp)
            .AddSubspeciesTrait(S_SubspeciesTrait.stomach)//胃
            .AddSubspeciesTrait(S_SubspeciesTrait.diet_lithotroph)//吸光
            .AddSubspeciesTrait(S_SubspeciesTrait.reproduction_fission)//分离
            .AddSubspeciesTrait(S_SubspeciesTrait.hydrophobia)//恐水
            .AddSubspeciesTrait(S_SubspeciesTrait.cold_resistance)//冷抗
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
            .SetDefaultWeapons(S_Item.white_staff)
            .AddTrait(S_Trait.arcane_reflexes)//魔力反射
            .AddTrait(S_Trait.deceitful)//欺诈
            .AddTrait(S_Trait.savage)//野蛮
            .AddTrait(S_Trait.greedy)//贪婪
            .AddJob(ActorJobs.SpawnedUnit)//召唤单位
            .AddSubspeciesTrait(S_SubspeciesTrait.fins)//鳍
            .AddSubspeciesTrait(S_SubspeciesTrait.stomach)//胃
            .AddSubspeciesTrait(S_SubspeciesTrait.diet_piscivore)//eat_piscivore
            .AddSubspeciesTrait(S_SubspeciesTrait.gift_of_water)//水份
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
            .AddSubspeciesTrait(S_SubspeciesTrait.fins)//鳍
            .AddSubspeciesTrait(S_SubspeciesTrait.stomach)//胃
            .AddSubspeciesTrait(S_SubspeciesTrait.diet_piscivore)//eat_piscivore
            .AddSubspeciesTrait(S_SubspeciesTrait.gift_of_water)//水份
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
            .AddSubspeciesTrait(S_SubspeciesTrait.fins)//鳍
            .AddSubspeciesTrait(S_SubspeciesTrait.stomach)//胃
            .AddSubspeciesTrait(S_SubspeciesTrait.diet_piscivore)//eat_piscivore
            .AddSubspeciesTrait(S_SubspeciesTrait.gift_of_water)//水份
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
            .AddTrait(S_Trait.light_lamp)//光 lamp
            .AddJob(ActorJobs.SpawnedUnit)//召唤单位
            .AddSubspeciesTrait(S_SubspeciesTrait.hovering)//悬浮
            .AddSubspeciesTrait(S_SubspeciesTrait.bioluminescence)//亮光
            .AddSubspeciesTrait(S_SubspeciesTrait.pollinating)//flower_pollination
            .AddSubspeciesTrait(S_SubspeciesTrait.stomach)//胃
            .AddSubspeciesTrait(S_SubspeciesTrait.diet_florivore)//eat_florivore
            .AddSubspeciesTrait(S_SubspeciesTrait.diet_frugivore)//eat_frugivore
            .AddSubspeciesTrait(S_SubspeciesTrait.annoying_fireworks)//彩火
            .AddSubspeciesTrait(S_SubspeciesTrait.death_grow_plant)//死亡长草
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
            .AddTrait(S_Trait.light_lamp)//光 lamp
            .AddSubspeciesTrait(S_SubspeciesTrait.hovering)//悬浮
            .AddSubspeciesTrait(S_SubspeciesTrait.bioluminescence)//亮光
            .AddSubspeciesTrait(S_SubspeciesTrait.pollinating)//flower_pollination
            .AddSubspeciesTrait(S_SubspeciesTrait.stomach)//胃
            .AddSubspeciesTrait(S_SubspeciesTrait.diet_florivore)//eat_florivore
            .AddSubspeciesTrait(S_SubspeciesTrait.diet_frugivore)//eat_frugivore
            .AddSubspeciesTrait(S_SubspeciesTrait.annoying_fireworks)//彩火
            .AddSubspeciesTrait(S_SubspeciesTrait.death_grow_plant)//死亡长草
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
            .AddTrait(S_Trait.light_lamp)//光 lamp
            .AddSubspeciesTrait(S_SubspeciesTrait.hovering)//悬浮
            .AddSubspeciesTrait(S_SubspeciesTrait.bioluminescence)//亮光
            .AddSubspeciesTrait(S_SubspeciesTrait.pollinating)//flower_pollination
            .AddSubspeciesTrait(S_SubspeciesTrait.stomach)//胃
            .AddSubspeciesTrait(S_SubspeciesTrait.diet_florivore)//eat_florivore
            .AddSubspeciesTrait(S_SubspeciesTrait.diet_frugivore)//eat_frugivore
            .AddSubspeciesTrait(S_SubspeciesTrait.annoying_fireworks)//彩火
            .AddSubspeciesTrait(S_SubspeciesTrait.death_grow_plant)//死亡长草
            .Stats(S.damage, 23)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 18)//速度
            .Stats(S.health, 140)//血量
            .Stats(S.armor, 2)//防御
            .Stats(S.lifespan, 90);//寿命
        GhostFire.SetCamp(KingdomAssets.Spirit)//鬼火精灵
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetAnimIdleRaw("walk_0_0,walk_0_1,walk_0_2,walk_0_3")
            .SetIcon("cultiway/icons/races/iconGhost_Fire")
            .SetJumpAnimation(false)
            .SetStandWhileSleeping(true)
            .AddTrait(S_Trait.sunblessed)//阳光祝福
            .AddTrait(S_Trait.light_lamp)//光 lamp
            .AddSubspeciesTrait(S_SubspeciesTrait.hovering)//悬浮
            .AddSubspeciesTrait(S_SubspeciesTrait.bioluminescence)//亮光
            .AddSubspeciesTrait(S_SubspeciesTrait.annoying_fireworks)//彩火
            .Stats(S.damage, 10)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 24)//速度
            .Stats(S.health, 70)//血量
            .Stats(S.armor, 0)//防御
            .Stats(S.lifespan, 999);//寿命
        CandleGenie.SetCamp(KingdomAssets.Spirit)//烛火精灵
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetAnimIdleRaw("walk_0_0,walk_0_1,walk_0_2,walk_0_3")
            .SetIcon("cultiway/icons/races/iconCandle_Genie")
            .SetJumpAnimation(false)
            .SetStandWhileSleeping(true)
            .AddTrait(S_Trait.sunblessed)//阳光祝福
            .AddTrait(S_Trait.light_lamp)//光 lamp
            .AddSubspeciesTrait(S_SubspeciesTrait.bioluminescence)//亮光
            .AddSubspeciesTrait(S_SubspeciesTrait.annoying_fireworks)//彩火
            .Stats(S.damage, 12)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 15)//速度
            .Stats(S.health, 80)//血量
            .Stats(S.armor, 0)//防御
            .Stats(S.lifespan, 200);//寿命
        KnowledgeGenie.SetCamp(KingdomAssets.Spirit)//知识精灵
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("cultiway/icons/races/iconKnowledge_Genie")
            .SetJumpAnimation(false)
            .AddTrait(S_Trait.sunblessed)//阳光祝福
            .AddTrait(S_Trait.light_lamp)//光 lamp
            .AddSubspeciesTrait(S_SubspeciesTrait.hovering)//悬浮
            .AddSubspeciesTrait(S_SubspeciesTrait.bioluminescence)//亮光
            .AddSubspeciesTrait(S_SubspeciesTrait.annoying_fireworks)//彩火
            .Stats(S.damage, 2)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 18)//速度
            .Stats(S.health, 50)//血量
            .Stats(S.armor, 0)//防御
            .Stats(S.lifespan, 500);//寿命
        GoblinShaman.SetCamp(KingdomAssets.Goblin)//哥布林萨满
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("cultiway/icons/races/iconGoblin_Shaman")
            .SetJumpAnimation(false)
            .SetDefaultWeapons(S_Item.evil_staff)//法杖
            .AddTrait(S_Trait.arcane_reflexes)//魔力反射
            .AddTrait(S_Trait.wise)//智慧
            .AddTrait(S_Trait.heart_of_wizard)//魔力心
            .AddTrait(S_Trait.greedy)//贪婪
            .AddTrait(S_Trait.savage)//野蛮
            .AddJob(ActorJobs.SpawnedUnit)//召唤单位
            .AddSubspeciesTrait(S_SubspeciesTrait.stomach)//胃
            .AddSubspeciesTrait(S_SubspeciesTrait.diet_omnivore)//eat_omnivore
            .AddSubspeciesTrait(S_SubspeciesTrait.big_stomach)//大胃
            .AddSubspeciesTrait(S_SubspeciesTrait.bioproduct_mushrooms)//生物产品-蘑菇
            .AddSubspeciesTrait(S_SubspeciesTrait.gift_of_fire)//火之ift
            .AddSubspeciesTrait(S_SubspeciesTrait.reproduction_sexual)// reproduce_sexual
            .AddSubspeciesTrait(S_SubspeciesTrait.high_fecundity)//高 fecundity
            .AddSubspeciesTrait(S_SubspeciesTrait.reproduction_strategy_viviparity)//  reproduce_strategy_viviparity
            .AddSubspeciesTrait(S_SubspeciesTrait.gestation_short)//短 gestation_short
            .Stats(S.damage, 20)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 9)//速度
            .Stats(S.health, 110)//血量
            .Stats(S.armor, 0)//防御
            .Stats(S.lifespan, 50);//寿命
        GoblinKnight.SetCamp(KingdomAssets.Goblin)//哥布林骑士
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("cultiway/icons/races/iconGoblin_Knight")
            .SetJumpAnimation(false)
            .SetDefaultWeapons(S_Item.spear_stone)
            .AddTrait(S_Trait.battle_reflexes)//战斗反射
            .AddTrait(S_Trait.greedy)//贪婪
            .AddTrait(S_Trait.savage)//野蛮
            .AddSubspeciesTrait(S_SubspeciesTrait.stomach)//胃
            .AddSubspeciesTrait(S_SubspeciesTrait.diet_omnivore)//eat_omnivore
            .AddSubspeciesTrait(S_SubspeciesTrait.big_stomach)//大胃
            .AddSubspeciesTrait(S_SubspeciesTrait.bioproduct_mushrooms)//生物产品-蘑菇
            .AddSubspeciesTrait(S_SubspeciesTrait.reproduction_sexual)// reproduce_sexual
            .AddSubspeciesTrait(S_SubspeciesTrait.high_fecundity)//高 fecundity
            .AddSubspeciesTrait(S_SubspeciesTrait.reproduction_strategy_viviparity)//  reproduce_strategy_viviparity
            .AddSubspeciesTrait(S_SubspeciesTrait.gestation_short)//短 gestation_short
            .Stats(S.damage, 26)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 13)//速度
            .Stats(S.health, 170)//血量
            .Stats(S.armor, 6)//防御
            .Stats(S.lifespan, 45);//寿命
        GoblinWarrior.SetCamp(KingdomAssets.Goblin)//哥布林战士
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("cultiway/icons/races/iconGoblin_Warrior")
            .SetJumpAnimation(false)
            .SetDefaultWeapons(S_Item.axe_stone)
            .AddTrait(S_Trait.battle_reflexes)//战斗反射
            .AddTrait(S_Trait.greedy)//贪婪
            .AddTrait(S_Trait.savage)//野蛮
            .AddSubspeciesTrait(S_SubspeciesTrait.stomach)//胃
            .AddSubspeciesTrait(S_SubspeciesTrait.diet_omnivore)//eat_omnivore
            .AddSubspeciesTrait(S_SubspeciesTrait.big_stomach)//大胃
            .AddSubspeciesTrait(S_SubspeciesTrait.bioproduct_mushrooms)//生物产品-蘑菇
            .AddSubspeciesTrait(S_SubspeciesTrait.reproduction_sexual)// reproduce_sexual
            .AddSubspeciesTrait(S_SubspeciesTrait.high_fecundity)//高 fecundity
            .AddSubspeciesTrait(S_SubspeciesTrait.reproduction_strategy_viviparity)//  reproduce_strategy_viviparity
            .AddSubspeciesTrait(S_SubspeciesTrait.gestation_short)//短 gestation_short
            .Stats(S.damage, 22)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 11)//速度
            .Stats(S.health, 150)//血量
            .Stats(S.armor, 4)//防御
            .Stats(S.lifespan, 40);//寿命
        Mummy.SetCamp(KingdomAssets.Undead)//木乃伊
            .SetAnimWalk(S_Anim.walk_0, S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("actors/species/other/Cultiway/Mummy/main/walk_0")
            .SetJumpAnimation(false)
            .SetDefaultWeapons(S_Item.sword_silver)// mythril_sword
            .AddTrait(S_Trait.battle_reflexes)//战斗反射
            .AddSubspeciesTrait(S_SubspeciesTrait.gift_of_death)//死亡ift
            .Stats(S.damage, 15)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 6)//速度
            .Stats(S.health, 200)//血量
            .Stats(S.armor, 10)//防御
            .Stats(S.lifespan, 999);//寿命
        Pharaoh.SetCamp(KingdomAssets.Undead)//法老
            .SetAnimWalk(S_Anim.walk_0, S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("actors/species/other/Cultiway/Pharaoh/main/walk_0")
            .SetJumpAnimation(true)
            .SetDefaultWeapons(S_Item.necromancer_staff)//法术法杖
            .AddTrait(S_Trait.arcane_reflexes)//魔力反射
            .AddTrait(S_Trait.wise)//智慧
            .AddTrait(S_Trait.heart_of_wizard)//魔力心
            .AddJob(ActorJobs.SpawnedUnit)//召唤单位
            .AddSubspeciesTrait(S_SubspeciesTrait.gift_of_death)//死亡ift
            .Stats(S.damage, 28)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 8)//速度
            .Stats(S.health, 320)//血量
            .Stats(S.armor, 8)//防御
            .Stats(S.lifespan, 999);//寿命
        Anubis.SetCamp(KingdomAssets.Undead)//阿努比斯
            .SetAnimWalk(S_Anim.walk_0, S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("actors/species/other/Cultiway/Anubis/main/walk_0")
            .SetJumpAnimation(true)
            .SetDefaultWeapons(S_Item.plague_doctor_staff)//瘟疫医生法杖
            .AddTrait(S_Trait.arcane_reflexes)//魔力反射
            .AddTrait(S_Trait.wise)//智慧
            .AddTrait(S_Trait.heart_of_wizard)//魔力心
            .AddSubspeciesTrait(S_SubspeciesTrait.gift_of_death)//死亡ift
            .Stats(S.scale, 0.25f)//大小
            .Stats(S.damage, 42)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 16)//速度
            .Stats(S.health, 420)//血量
            .Stats(S.armor, 15)//防御
            .Stats(S.lifespan, 999);//寿命
        Ossaurus.SetCamp(KingdomAssets.Undead)//灵火骨龙
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5")
            .SetIcon("actors/species/other/Cultiway/Ossaurus/main/walk_0")
            .SetJumpAnimation(false)
            .SetHideHandItem(true)
            .SetDefaultWeapons(S_Item.evil_staff)//法术法杖
            .AddTrait(S_Trait.light_lamp)
            .AddTrait(S_Trait.arcane_reflexes)//魔力反射
            .AddTrait(S_Trait.wise)//智慧
            .AddTrait(S_Trait.pyromaniac)//魔力狂热
            .AddTrait(S_Trait.heart_of_wizard)//魔力心
            .AddSubspeciesTrait(S_SubspeciesTrait.gift_of_death)//死亡ift
            .AddSubspeciesTrait(S_SubspeciesTrait.gift_of_air)//
            .AddSubspeciesTrait(S_SubspeciesTrait.gift_of_fire)//
            .Stats(S.scale, 0.25f)//大小
            .Stats(S.damage, 52)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 22)//速度
            .Stats(S.health, 380)//血量
            .Stats(S.armor, 8)//防御
            .Stats(S.lifespan, 999);//寿命
        SkeletonKnight.SetCamp(KingdomAssets.Undead)//骷髅骑士
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetAnimIdleRaw("walk_0_0,walk_0_1,walk_0_2,walk_0_3")
            .SetIcon("cultiway/icons/races/iconSkeleton_Knight")
            .SetJumpAnimation(false)
            .SetDefaultWeapons(S_Item.spear_silver)//矛
            .AddSubspeciesTrait(S_SubspeciesTrait.gift_of_death)//死亡ift
            .Stats(S.damage, 26)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 14)//速度
            .Stats(S.health, 150)//血量
            .Stats(S.armor, 5)//防御
            .Stats(S.lifespan, 999);//寿命
        Sphinx.SetCamp(KingdomAssets.Undead)//斯芬克斯
            .SetAnimWalk(S_Anim.walk_0, S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("actors/species/other/Cultiway/Sphinx/main/walk_0")
            .SetJumpAnimation(false)
            .AddTrait(S_Trait.arcane_reflexes)//魔力反射
            .AddTrait(S_Trait.wise)//智慧
            .AddTrait(S_Trait.heart_of_wizard)//魔力心
            .AddSubspeciesTrait(S_SubspeciesTrait.gift_of_death)//死亡ift
            .Stats(S.scale, 0.25f)//大小
            .Stats(S.damage, 40)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 10)//速度
            .Stats(S.health, 550)//血量
            .Stats(S.armor, 20)//防御
            .Stats(S.lifespan, 999);//寿命   
        Diplodocus.SetCamp(KingdomAssets.VegetarianDinosaur)//梁龙
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("cultiway/icons/races/iconDiplodocus")
            .SetJumpAnimation(true)
            .SetStandWhileSleeping(true)
            .Stats(S.damage, 35)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 10)//速度
            .Stats(S.health, 600)//血量
            .Stats(S.armor, 12)//防御
            .Stats(S.lifespan, 150);//寿命
        Dreadnoughtus.SetCamp(KingdomAssets.VegetarianDinosaur)//无畏巨龙
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("cultiway/icons/races/iconDreadnoughtus")
            .SetJumpAnimation(true)
            .SetStandWhileSleeping(true)
            .Stats(S.damage, 55)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 7)//速度
            .Stats(S.health, 480)//血量
            .Stats(S.armor, 10)//防御
            .Stats(S.lifespan, 300);//寿命
        Triceratops.SetCamp(KingdomAssets.VegetarianDinosaur)//三角龙
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("cultiway/icons/races/iconTriceratops")
            .SetJumpAnimation(true)
            .SetStandWhileSleeping(true)
            .Stats(S.damage, 45)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 8)//速度
            .Stats(S.health, 450)//血量
            .Stats(S.armor, 18)//防御
            .Stats(S.lifespan, 130);//寿命
        TyrannosaurusRex.SetCamp(KingdomAssets.CarnivorousDinosaur)//暴龙
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("cultiway/icons/races/iconTyrannosaurus_Rex")
            .SetJumpAnimation(true)
            .SetAnimWalkSpeed(5f)//移动速度
            .SetAnimIdleSpeed(5f)//待机速度
            .SetAnimSwimSpeed(5f)//游动速度
            .Stats(S.damage, 65)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 16)//速度
            .Stats(S.health, 520)//血量
            .Stats(S.armor, 9)//防御
            .Stats(S.lifespan, 110);//寿命
        Pterodactyl.SetCamp(KingdomAssets.CarnivorousDinosaur)//翼龙
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("cultiway/icons/races/iconPterodactyl")
            .SetJumpAnimation(true)
            .SetAnimWalkSpeed(5f)//移动速度
            .SetAnimIdleSpeed(5f)//待机速度
            .SetAnimSwimSpeed(5f)//游动速度
            .Stats(S.damage, 25)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 26)//速度
            .Stats(S.health, 160)//血量
            .Stats(S.armor, 1)//防御
            .Stats(S.lifespan, 70);//寿命
        Velociraptor.SetCamp(KingdomAssets.CarnivorousDinosaur)//迅猛龙
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("cultiway/icons/races/iconVelociraptor")
            .SetJumpAnimation(false)
            .SetAnimWalkSpeed(5f)//移动速度
            .SetAnimIdleSpeed(5f)//待机速度
            .SetAnimSwimSpeed(5f)//游动速度
            .Stats(S.damage, 30)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 30)//速度
            .Stats(S.health, 180)//血量
            .Stats(S.armor, 3)//防御
            .Stats(S.lifespan, 65);//寿命
        IceGiant.SetCamp(KingdomAssets.Titan)//冰石泰坦
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("actors/species/other/Cultiway/IceGiant/main/walk_0")
            .SetJumpAnimation(true)
            .SetStandWhileSleeping(true)
            .AddTrait(S_Trait.cold_aura)//冰光
            .AddTrait(S_Trait.freeze_proof)//冰抗
            .Stats(S.damage, 48)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 4)//速度
            .Stats(S.health, 700)//血量
            .Stats(S.scale, 0.25f)//大小
            .Stats(S.armor, 18)//防御
            .Stats(S.stamina, 220)//耐力
            .Stats(S.lifespan, 2000);//寿命
        LavaGiant.SetCamp(KingdomAssets.Titan)//熔岩泰坦
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("actors/species/other/Cultiway/LavaGiant/main/walk_0")
            .SetJumpAnimation(true)
            .SetStandWhileSleeping(true)
            .AddTrait(S_Trait.light_lamp)
            .AddTrait(S_Trait.pyromaniac)//火魔
            .AddTrait(S_Trait.burning_feet)//燃烧脚
            .AddTrait(S_Trait.fire_blood)//火血
            .AddTrait(S_Trait.fire_proof)//火抗
            .Stats(S.damage, 58)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 5)//速度
            .Stats(S.health, 650)//血量
            .Stats(S.scale, 0.25f)//大小
            .Stats(S.armor, 15)//防御
            .Stats(S.stamina, 220)//耐力
            .Stats(S.lifespan, 1800);//寿命
        RockGiant.SetCamp(KingdomAssets.Titan)//岩石泰坦
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("actors/species/other/Cultiway/RockGiant/main/walk_0")
            .SetJumpAnimation(true)
            .SetStandWhileSleeping(true)
            .Stats(S.damage, 42)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 3)//速度
            .Stats(S.health, 850)//血量
            .Stats(S.scale, 0.25f)//大小
            .Stats(S.armor, 25)//防御
            .Stats(S.stamina, 220)//耐力
            .Stats(S.lifespan, 2500);//寿命
        VolcanicGiant.SetCamp(KingdomAssets.Titan)//火山泰坦
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("actors/species/other/Cultiway/VolcanicGiant/main/walk_0")
            .SetJumpAnimation(true)
            .SetStandWhileSleeping(true)
            .AddTrait(S_Trait.light_lamp)
            .AddTrait(S_Trait.pyromaniac)//火魔
            .AddTrait(S_Trait.hotheaded)//bold
            .AddTrait(S_Trait.burning_feet)//燃烧脚
            .AddTrait(S_Trait.fire_blood)//火血
            .AddTrait(S_Trait.fire_proof)//火抗
            .Stats(S.damage, 65)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 6)//速度
            .Stats(S.health, 600)//血量
            .Stats(S.scale, 0.25f)//大小
            .Stats(S.armor, 12)//防御
            .Stats(S.stamina, 220)//耐力
            .Stats(S.lifespan, 1500);//寿命
        GriffinKnight.SetCamp(KingdomAssets.Superman)//狮鹫骑士
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("actors/species/other/Cultiway/GriffinKnight/main/walk_0")
            .SetJumpAnimation(false)
            .AddTrait(S_SubspeciesTrait.hovering)//悬浮
            .AddSubspeciesTrait(S_SubspeciesTrait.hovering)//悬浮
            .SetDefaultWeapons(S_Item.bow_mythril)//弓
            .Stats(S.damage, 38)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 10)//速度
            .Stats(S.health, 280)//血量
            .Stats(S.armor, 8)//防御
            .Stats(S.stamina, 100)//耐力
            .Stats(S.lifespan, 80);//寿命
        Sorcerer.SetCamp(KingdomAssets.Superman)//紫袍巫师
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("actors/species/other/Cultiway/Sorcerer/main/walk_0")
            .SetJumpAnimation(false)
            .AddTrait(S_Trait.fire_proof)//火抗
            .AddTrait(S_Trait.freeze_proof)//冰抗
            .AddTrait(S_Trait.arcane_reflexes)//魔力反射
            .AddTrait(S_Trait.wise)//智慧
            .AddTrait(S_Trait.heart_of_wizard)//魔力心
            .AddJob(ActorJobs.SpawnedUnit)//召唤单位
            .AddSubspeciesTrait(S_SubspeciesTrait.gift_of_harmony)//谐心
            .SetDefaultWeapons(S_Item.white_staff)//白杖
            .Stats(S.damage, 45)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 14)//速度
            .Stats(S.health, 220)//血量
            .Stats(S.armor, 3)//防御
            .Stats(S.stamina, 100)//耐力
            .Stats(S.lifespan, 200);//寿命
        GuardKnight.SetCamp(KingdomAssets.Superman)//守护骑士
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("actors/species/other/Cultiway/GuardKnight/main/walk_0")
            .SetJumpAnimation(false)
            .SetDefaultWeapons(S_Item.spear_iron)//矛
            .Stats(S.damage, 30)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 10)//速度
            .Stats(S.health, 400)//血量
            .Stats(S.armor, 18)//防御
            .Stats(S.stamina, 100)//耐力
            .Stats(S.lifespan, 90);//寿命
        FireWyvern.SetCamp(KingdomAssets.Superman)//烈焰飞龙
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("cultiway/icons/races/iconFire_Wyvern")
            .SetJumpAnimation(true)
            .SetStandWhileSleeping(true)
            .SetHideHandItem(true)
            .SetDefaultWeapons(S_Item.evil_staff)//法术法杖
            .SetAnimWalkSpeed(1f)//移动速度
            .SetAnimIdleSpeed(1.5f)//待机速度
            .SetAnimSwimSpeed(1.5f)//游动速度
            .AddTrait(S_Trait.regeneration)//回复
            .AddTrait(S_Trait.burning_feet)//着火脚
            .AddTrait(S_Trait.light_lamp)
            .AddTrait(S_Trait.fire_blood)
            .AddTrait(S_Trait.fire_proof)
            .Stats(S.damage, 28)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 28)//速度
            .Stats(S.lifespan, 120)
            .Stats(S.health, 3300)//血量
            .Stats(S.scale, 0.25f)//大小
            .Stats(S.armor, 20)//防御
            .Stats(S.stamina, 1220)//耐力
            .Stats(S.lifespan, 285);//寿命
        VampireHunter.SetCamp(KingdomAssets.Superman)//猎魔人
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("actors/species/other/Cultiway/VampireHunter/main/walk_0")
            .SetJumpAnimation(false)
            .AddTrait(S_Trait.fire_proof)//火抗
            .AddTrait(S_Trait.freeze_proof)//冰抗
            .SetDefaultWeapons(S_Item.shotgun)//霰弹枪
            .Stats(S.damage, 40)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 18)//速度
            .Stats(S.health, 260)//血量
            .Stats(S.armor, 6)//防御
            .Stats(S.stamina, 220)//耐力
            .Stats(S.lifespan, 120);//寿命
        FairyFox.SetCamp(KingdomAssets.Divine)//九尾狐
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("cultiway/icons/races/iconFairy_Fox")
            .SetJumpAnimation(false)
            .SetStandWhileSleeping(false)
            .AddTrait(S_Trait.genius)//天才
            .AddTrait(S_Trait.moonchild)//月之孩子
            .AddTrait(S_Trait.attractive)// attractive
            .AddTrait(S_Trait.shiny)//闪亮
            .AddTrait(S_Trait.light_lamp)
            .AddTrait(S_Trait.regeneration)//回复
            .Stats(S.damage, 35)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 9)//速度
            .Stats(S.health, 320)//血量
            .Stats(S.armor, 8)//防御
            .Stats(S.stamina, 120)//耐力
            .Stats(S.lifespan, 999);//寿命
        FengHuang.SetCamp(KingdomAssets.Divine)//凤凰
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("cultiway/icons/races/iconFeng_Huang")
            .SetJumpAnimation(false)
            .SetStandWhileSleeping(false)
            .SetAnimWalkSpeed(5f)//移动速度
            .SetAnimIdleSpeed(5f)//待机速度
            .SetAnimSwimSpeed(5f)//游动速度
            .AddTrait(S_Trait.shiny)//闪亮
            .AddTrait(S_Trait.light_lamp)
            .AddTrait(S_Trait.regeneration)//回复
            .Stats(S.damage, 50)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 10)//速度
            .Stats(S.health, 400)//血量
            .Stats(S.armor, 10)//防御
            .Stats(S.stamina, 120)//耐力
            .Stats(S.lifespan, 4500);//寿命
        JinWu.SetCamp(KingdomAssets.Divine)//金乌
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("cultiway/icons/races/iconJin_Wu")
            .SetJumpAnimation(false)
            .SetStandWhileSleeping(false)
            .SetHideHandItem(true)
            .SetDefaultWeapons(S_Item.evil_staff)//法术法杖
            .AddTrait(S_Trait.shiny)//闪亮
            .AddTrait(S_Trait.light_lamp)
            .AddTrait(S_Trait.regeneration)//回复
            .Stats(S.damage, 55)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 10)//速度
            .Stats(S.health, 350)//血量
            .Stats(S.armor, 7)//防御
            .Stats(S.stamina, 120)//耐力
            .Stats(S.lifespan, 3000);//寿命
        QingLong.SetCamp(KingdomAssets.Divine)//青龙
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("cultiway/icons/races/iconQing_Long")
            .SetJumpAnimation(false)
            .SetStandWhileSleeping(true)
            .SetAnimWalkSpeed(5f)//移动速度
            .SetAnimIdleSpeed(5f)//待机速度
            .SetAnimSwimSpeed(5f)//游动速度
            .AddTrait(S_Trait.shiny)//闪亮
            .AddTrait(S_Trait.light_lamp)
            .AddTrait(S_Trait.regeneration)//回复
            .AddTrait(S_Trait.flower_prints)//花印
            .AddTrait(S_Trait.sunblessed)//阳光祝福
            .AddTrait(S_Trait.poison_immune)//毒抗
            .AddSubspeciesTrait(S_SubspeciesTrait.death_grow_tree)//死亡长树
            .Stats(S.damage, 60)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 18)//速度
            .Stats(S.health, 550)//血量
            .Stats(S.armor, 15)//防御
            .Stats(S.stamina, 120)//耐力
            .Stats(S.lifespan, 5000);//寿命
        QiLin.SetCamp(KingdomAssets.Divine)//麒麟
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("cultiway/icons/races/iconQi_Lin")
            .SetJumpAnimation(false)
            .SetStandWhileSleeping(true)
            .AddTrait(S_Trait.shiny)//闪亮
            .AddTrait(S_Trait.light_lamp)
            .AddTrait(S_Trait.regeneration)//回复
            .AddTrait(S_Trait.tough)// toughen
            .AddTrait(S_Trait.thorns)//荆棘
            .AddTrait(S_Trait.hard_skin)//硬皮
            .AddTrait(S_Trait.acid_proof)//酸抗
            .Stats(S.damage, 45)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 18)//速度
            .Stats(S.health, 500)//血量
            .Stats(S.armor, 20)//防御
            .Stats(S.stamina, 120)//耐力
            .Stats(S.lifespan, 4000);//寿命
        NineColoredDeer.SetCamp(KingdomAssets.Divine)//九色鹿
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("cultiway/icons/races/iconNine_Colored_Deer")
            .SetJumpAnimation(false)
            .SetStandWhileSleeping(true)
            .AddTrait(S_Trait.shiny)//闪亮
            .AddTrait(S_Trait.light_lamp)
            .AddTrait(S_Trait.regeneration)//回复
            .Stats(S.damage, 20)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 18)//速度
            .Stats(S.health, 500)//血量
            .Stats(S.armor, 12)//防御
            .Stats(S.stamina, 120)//耐力
            .Stats(S.lifespan, 3000);//寿命
        WhiteTiger.SetCamp(KingdomAssets.Divine)//白虎
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("cultiway/icons/races/iconWhite_Tiger")
            .SetJumpAnimation(false)
            .SetStandWhileSleeping(true)
            .AddTrait(S_Trait.shiny)//闪亮
            .AddTrait(S_Trait.light_lamp)
            .AddTrait(S_Trait.regeneration)//回复
            .AddTrait(S_Trait.bloodlust)//嗜血
            .AddTrait(S_Trait.agile)//硬皮
            .Stats(S.damage, 65)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 18)//速度
            .Stats(S.health, 380)//血量
            .Stats(S.armor, 13)//防御
            .Stats(S.stamina, 120)//耐力
            .Stats(S.lifespan, 3500);//寿命
        XuanWu.SetCamp(KingdomAssets.Divine)//玄武
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("cultiway/icons/races/iconXuan_Wu")
            .SetJumpAnimation(false)
            .SetStandWhileSleeping(true)
            .AddTrait(S_Trait.shiny)//闪亮
            .AddTrait(S_Trait.light_lamp)
            .AddTrait(S_Trait.regeneration)//回复
            .AddTrait(S_Trait.freeze_proof)//冻结抗
            .AddTrait(S_Trait.hard_skin)//硬皮
            .AddTrait(S_Trait.tough)//toughen
            .AddTrait(S_Trait.titan_lungs)//巨肺
            .AddTrait(S_Trait.cold_aura)//硬皮
            .Stats(S.damage, 38)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 8)//速度
            .Stats(S.health, 480)//血量
            .Stats(S.armor, 30)//防御
            .Stats(S.stamina, 120)//耐力
            .Stats(S.lifespan, 10000);//寿命
        YuChan.SetCamp(KingdomAssets.Divine)//玉蟾
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("cultiway/icons/races/iconYu_Chan")
            .SetJumpAnimation(false)
            .SetStandWhileSleeping(true)
            .AddTrait(S_Trait.shiny)//闪亮
            .AddTrait(S_Trait.regeneration)//回复
            .AddTrait(S_Trait.light_lamp)
            .Stats(S.damage, 32)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 10)//速度
            .Stats(S.health, 420)//血量
            .Stats(S.armor, 16)//防御
            .Stats(S.stamina, 120)//耐力
            .Stats(S.lifespan, 2800);//寿命
        YueTu.SetCamp(KingdomAssets.Divine)//月兔
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("cultiway/icons/races/iconYue_Tu")
            .SetJumpAnimation(false)
            .SetStandWhileSleeping(true)
            .AddTrait(S_Trait.shiny)//闪亮
            .AddTrait(S_Trait.regeneration)//回复
            .AddTrait(S_Trait.light_lamp)
            .Stats(S.damage, 10)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 20)//速度
            .Stats(S.health, 200)//血量
            .Stats(S.armor, 5)//防御
            .Stats(S.stamina, 120)//耐力
            .Stats(S.lifespan, 1500);//寿命
        ZhuQue.SetCamp(KingdomAssets.Divine)//朱雀
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("cultiway/icons/races/iconZhu_Que")
            .SetJumpAnimation(false)
            .SetStandWhileSleeping(true)
            .SetHideHandItem(true)
            .SetAnimWalkSpeed(5f)//移动速度
            .SetAnimIdleSpeed(5f)//待机速度
            .SetAnimSwimSpeed(5f)//游动速度
            .SetDefaultWeapons(S_Item.evil_staff)//法术法杖
            .AddTrait(S_Trait.shiny)//闪亮
            .AddTrait(S_Trait.regeneration)//回复
            .AddTrait(S_Trait.burning_feet)//着火脚
            .AddTrait(S_Trait.light_lamp)
            .AddTrait(S_Trait.fire_blood)
            .AddTrait(S_Trait.fire_proof)
            .Stats(S.damage, 52)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 18)//速度
            .Stats(S.health, 370)//血量
            .Stats(S.armor, 9)//防御
            .Stats(S.stamina, 120)//耐力
            .Stats(S.lifespan, 3200);//寿命
        HalfDeerMan.SetCamp(KingdomAssets.DemiHuman)//半人鹿
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("cultiway/icons/races/iconHalf_Deer_Man")
            .SetJumpAnimation(false)
            .SetStandWhileSleeping(true)
            .SetDefaultWeapons(S_Item.bow_wood)
            .Stats(S.damage, 25)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 16)//速度
            .Stats(S.health, 240)//血量
            .Stats(S.armor, 4)//防御
            .Stats(S.stamina, 120)//耐力
            .Stats(S.lifespan, 150);//寿命
        Mermaid.SetCamp(KingdomAssets.DemiHuman)//美人鱼
            .SetAnimWalk(S_Anim.walk_0, S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("cultiway/icons/races/iconMermaid")
            .SetJumpAnimation(false)
            .SetStandWhileSleeping(true)
            .SetDefaultWeapons(S_Item.spear_silver)
            .AddTrait(S_Trait.deceitful)//欺诈
            .AddTrait(S_Trait.lustful)
            .AddTrait(S_Trait.attractive)// attractive
            .AddTrait(S_Trait.battle_reflexes)//战斗反射
            .AddSubspeciesTrait(S_SubspeciesTrait.fins)//鳍
            .AddSubspeciesTrait(S_SubspeciesTrait.stomach)//胃
            .AddSubspeciesTrait(S_SubspeciesTrait.diet_piscivore)//eat_piscivore
            .AddSubspeciesTrait(S_SubspeciesTrait.aquatic)//水
            .AddSubspeciesTrait(S_SubspeciesTrait.gift_of_water)//水份
            .Stats(S.damage, 18)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 8)//速度
            .Stats(S.health, 180)//血量
            .Stats(S.armor, 2)//防御
            .Stats(S.stamina, 120)//耐力
            .Stats(S.lifespan, 300);//寿命
        Centaur.SetCamp(KingdomAssets.DemiHuman)//半人马
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("cultiway/icons/races/iconCentaur")
            .SetJumpAnimation(false)
            .SetStandWhileSleeping(true)
            .SetDefaultWeapons(S_Item.spear_stone)// spear_stone
            .Stats(S.damage, 34)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 18)//速度
            .Stats(S.health, 290)//血量
            .Stats(S.armor, 7)//防御
            .Stats(S.stamina, 120)//耐力
            .Stats(S.lifespan, 100);//寿命
        KingSlime.SetCamp(KingdomAssets.Monster)//史莱姆王
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetAnimIdleRaw("walk_0_0,walk_0_1,walk_0_2,walk_0_3")
            .SetIcon("cultiway/icons/races/iconKing_Slime")
            .SetJumpAnimation(false)
            .SetStandWhileSleeping(true)
            .AddTrait(S_Trait.mega_heartbeat)//跳动
            .Stats(S.damage, 30)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 5)//速度
            .Stats(S.health, 600)//血量
            .Stats(S.armor, 5)//防御
            .Stats(S.scale, 0.25f)//大小
            .Stats(S.stamina, 120)//耐力
            .Stats(S.lifespan, 500);//寿命
        GiantOctopus.SetCamp(KingdomAssets.Monster)//巨型章鱼
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("cultiway/icons/races/iconGiant_Octopus")
            .SetJumpAnimation(false)
            .SetStandWhileSleeping(true)
            .SetAnimWalkSpeed(5f)//移动速度
            .SetAnimIdleSpeed(5f)//待机速度
            .SetAnimSwimSpeed(5f)//游动速度
            .AddSubspeciesTrait(S_SubspeciesTrait.fins)//鳍
            .AddSubspeciesTrait(S_SubspeciesTrait.stomach)//胃
            .AddSubspeciesTrait(S_SubspeciesTrait.diet_piscivore)//eat_piscivore
            .AddSubspeciesTrait(S_SubspeciesTrait.aquatic)//水
            .AddSubspeciesTrait(S_SubspeciesTrait.gift_of_water)//水份
            .Stats(S.damage, 55)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 15)//速度
            .Stats(S.health, 580)//血量
            .Stats(S.armor, 12)//防御
            .Stats(S.stamina, 120)//耐力
            .Stats(S.lifespan, 180);//寿命
        Werewolf.SetCamp(KingdomAssets.Monster)//狼人
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("cultiway/icons/races/iconWerewolf")
            .SetJumpAnimation(false)
            .SetStandWhileSleeping(true)
            .AddTrait(S_Trait.moonchild)//狼人
            .AddTrait(S_Trait.battle_reflexes)//战斗反射
            .AddTrait(S_Trait.greedy)//贪婪
            .AddTrait(S_Trait.savage)//野蛮
            .AddTrait(S_Trait.regeneration)//回复
            .AddTrait(S_Trait.bloodlust)//嗜血
            .AddTrait(S_Trait.agile)//硬皮
            .Stats(S.damage, 48)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 22)//速度
            .Stats(S.health, 350)//血量
            .Stats(S.armor, 6)//防御
            .Stats(S.stamina, 120)//耐力
            .Stats(S.lifespan, 130);//寿命
        Deer.SetCamp(KingdomAssets.Herbivore)//鹿
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("cultiway/icons/races/iconDeer")
            .SetJumpAnimation(false)
            .SetStandWhileSleeping(true)
            .Stats(S.damage, 8)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 28)//速度
            .Stats(S.health, 120)//血量
            .Stats(S.armor, 0)//防御
            .Stats(S.stamina, 120)//耐力
            .Stats(S.lifespan, 20);//寿命
        Horse.SetCamp(KingdomAssets.Herbivore)//马
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("cultiway/icons/races/iconHorse")
            .SetJumpAnimation(false)
            .SetStandWhileSleeping(true)
            .Stats(S.damage, 10)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 30)//速度
            .Stats(S.health, 160)//血量
            .Stats(S.armor, 2)//防御
            .Stats(S.stamina, 120)//耐力
            .Stats(S.lifespan, 35);//寿命
        Panda.SetCamp(KingdomAssets.Herbivore)//熊猫
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("cultiway/icons/races/iconPanda")
            .SetJumpAnimation(false)
            .SetStandWhileSleeping(true)
            .Stats(S.damage, 35)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 8)//速度
            .Stats(S.health, 300)//血量
            .Stats(S.armor, 10)//防御
            .Stats(S.stamina, 120)//耐力
            .Stats(S.lifespan, 60);//寿命
        Pig.SetCamp(KingdomAssets.Herbivore)//猪
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("cultiway/icons/races/iconPig")
            .SetJumpAnimation(false)
            .SetStandWhileSleeping(true)
            .Stats(S.damage, 6)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 10)//速度
            .Stats(S.health, 150)//血量
            .Stats(S.armor, 1)//防御
            .Stats(S.stamina, 120)//耐力
            .Stats(S.lifespan, 25);//寿命
        WildBoar.SetCamp(KingdomAssets.Herbivore)//野猪
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("cultiway/icons/races/iconWild_Boar")
            .SetJumpAnimation(false)
            .SetStandWhileSleeping(true)
            .Stats(S.damage, 26)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 16)//速度
            .Stats(S.health, 240)//血量
            .Stats(S.armor, 8)//防御
            .Stats(S.stamina, 120)//耐力
            .Stats(S.lifespan, 30);//寿命
        Rooster.SetCamp(KingdomAssets.Herbivore)//公鸡
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("cultiway/icons/races/iconRooster")
            .SetJumpAnimation(false)
            .SetStandWhileSleeping(true)
            .Stats(S.damage, 7)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 14)//速度
            .Stats(S.health, 80)//血量
            .Stats(S.armor, 0)//防御
            .Stats(S.stamina, 120)//耐力
            .Stats(S.lifespan, 25);//寿命
        Mallard.SetCamp(KingdomAssets.Herbivore)//绿头鸭
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("actors/species/other/Cultiway/Mallard/main/walk_0")
            .SetJumpAnimation(false)
            .SetStandWhileSleeping(true)
            .AddTrait(S_Trait.agile)//敏捷
            .AddSubspeciesTrait(S_SubspeciesTrait.stomach)//胃
            .AddSubspeciesTrait(S_SubspeciesTrait.reproduction_sexual)//性别 reproduction_sexual
            .AddSubspeciesTrait(S_SubspeciesTrait.reproduction_strategy_oviparity)//Strategy_oviparity
            .AddSubspeciesTrait(S_SubspeciesTrait.nocturnal_dormancy)//暗睡
            .AddSubspeciesTrait(S_SubspeciesTrait.polyphasic_sleep)//暗睡
            .AddSubspeciesTrait(S_SubspeciesTrait.high_fecundity)// fecundity
            .AddSubspeciesTrait(S_Egg.egg_shell_spotted)//蛋壳
            .AddSubspeciesTrait(S_SubspeciesTrait.diet_carnivore)//食肉
            .Stats(S.damage, 6)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 10)//速度
            .Stats(S.health, 80)//血量
            .Stats(S.armor, 0)//防御
            .Stats(S.stamina, 120)//耐力
            .Stats(S.lifespan, 25);//寿命
        Lion.SetCamp(KingdomAssets.Carnivorous)//狮子
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("cultiway/icons/races/iconLion")
            .SetJumpAnimation(false)
            .SetStandWhileSleeping(true)
            .Stats(S.damage, 40)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 20)//速度
            .Stats(S.health, 280)//血量
            .Stats(S.armor, 5)//防御
            .Stats(S.stamina, 120)//耐力
            .Stats(S.lifespan, 40);//寿命
            Eagle.SetCamp(KingdomAssets.Carnivorous)//雕
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            //.SetAnimIdleRaw("walk_0_0,walk_0_1,walk_0_2,walk_0_3")
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("actors/species/other/Cultiway/Eagle/main/walk_0")
            .SetJumpAnimation(false)
            .SetStandWhileSleeping(true)
            .AddTrait(S_Trait.agile)//敏捷
            .AddSubspeciesTrait(S_SubspeciesTrait.stomach)//胃
            .AddSubspeciesTrait(S_SubspeciesTrait.reproduction_sexual)//性别 reproduction_sexual
            .AddSubspeciesTrait(S_SubspeciesTrait.reproduction_strategy_oviparity)//Strategy_oviparity
            .AddSubspeciesTrait(S_SubspeciesTrait.nocturnal_dormancy)//暗睡
            .AddSubspeciesTrait(S_SubspeciesTrait.polyphasic_sleep)//暗睡
            .AddSubspeciesTrait(S_SubspeciesTrait.high_fecundity)// fecundity
            .AddSubspeciesTrait(S_Egg.egg_shell_spotted)//蛋壳
            .AddSubspeciesTrait(S_SubspeciesTrait.diet_carnivore)//食肉
            .Stats(S.damage, 8)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 20)//速度
            .Stats(S.health, 80)//血量
            .Stats(S.armor, 0)//防御
            .Stats(S.stamina, 120)//耐力
            .Stats(S.lifespan, 30);//寿命
        Tiger.SetCamp(KingdomAssets.Carnivorous)//老虎
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7, S_Anim.walk_8)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("cultiway/icons/races/iconTiger")
            .SetJumpAnimation(false)
            .SetStandWhileSleeping(true)
            .Stats(S.damage, 45)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 24)//速度
            .Stats(S.health, 300)//血量
            .Stats(S.armor, 6)//防御
            .Stats(S.stamina, 120)//耐力
            .Stats(S.lifespan, 45);//寿命
        UncleanCreature.SetCamp(KingdomAssets.Nurgle)//不洁生物
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("actors/species/other/Cultiway/UncleanCreature/main/walk_0")
            .SetJumpAnimation(false)
            .SetDefaultWeapons(S_Item.sword_iron)//武器
            .AddTrait(S_Trait.battle_reflexes)//战斗反射
            .AddTrait(S_Trait.regeneration)//回复
            .AddTrait(S_Trait.bloodlust)//嗜血
            .AddTrait(S_Trait.immune)//免疫
            .AddTrait(S_Trait.blessed)//祝福
            .AddTrait(S_Trait.contagious)//传染
            .AddTrait(S_Trait.acid_blood)//酸血
            .AddTrait(S_Trait.acid_proof)//酸性
            .AddTrait(S_Trait.acid_touch)//酸性
            .AddSubspeciesTrait(S_SubspeciesTrait.stomach)//胃
            .AddSubspeciesTrait(S_SubspeciesTrait.diet_omnivore)//eat_omnivore
            .AddSubspeciesTrait(S_SubspeciesTrait.reproduction_soulborne)// Soulborne reproduction
            .Stats(S.damage, 20)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 8)//速度
            .Stats(S.health, 2200)//血量
            .Stats(S.armor, 9)//防御
            .Stats(S.stamina, 120)//耐力
            .Stats(S.lifespan, 77);//寿命
        NurgleSpirit.SetCamp(KingdomAssets.Nurgle)//纳垢灵
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("actors/species/other/Cultiway/NurgleSpirit/main/walk_0")
            .SetJumpAnimation(false)
            .AddTrait(S_Trait.battle_reflexes)//战斗反射
            .AddTrait(S_Trait.regeneration)//回复
            .AddTrait(S_Trait.bloodlust)//嗜血
            .AddTrait(S_Trait.immune)//免疫
            .AddTrait(S_Trait.blessed)//祝福
            .AddTrait(S_Trait.contagious)//传染
            .AddTrait(S_Trait.acid_blood)//酸血
            .AddTrait(S_Trait.acid_proof)//酸性
            .AddTrait(S_Trait.acid_touch)//酸性
            .AddTrait(S_Trait.immortal)//不死
            .AddSubspeciesTrait(S_SubspeciesTrait.reproduction_soulborne)// Soulborne reproduction
            .Stats(S.damage, 10)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 14)//速度
            .Stats(S.health, 800)//血量
            .Stats(S.armor, 2)//防御
            .Stats(S.stamina, 120)//耐力
            .Stats(S.lifespan, 77);//寿命
        NurgleDiseaseCarrier.SetCamp(KingdomAssets.Nurgle)//纳垢携疫者
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("actors/species/other/Cultiway/NurgleDiseaseCarrier/main/walk_0")
            .SetJumpAnimation(false)
            .SetDefaultWeapons(S_Item.hammer_iron)//武器
            .AddTrait(S_Trait.battle_reflexes)//战斗反射
            .AddTrait(S_Trait.regeneration)//回复
            .AddTrait(S_Trait.bloodlust)//嗜血
            .AddTrait(S_Trait.immune)//免疫
            .AddTrait(S_Trait.blessed)//祝福
            .AddTrait(S_Trait.contagious)//传染
            .AddTrait(S_Trait.acid_blood)//酸血
            .AddTrait(S_Trait.acid_proof)//酸性
            .AddTrait(S_Trait.acid_touch)//酸性
            .AddTrait(S_Trait.immortal)//不死
            .AddSubspeciesTrait(S_SubspeciesTrait.stomach)//胃
            .AddSubspeciesTrait(S_SubspeciesTrait.diet_omnivore)//eat_omnivore
            .AddSubspeciesTrait(S_SubspeciesTrait.big_stomach)//大胃
            .AddSubspeciesTrait(S_SubspeciesTrait.reproduction_soulborne)// Soulborne reproduction
            .Stats(S.damage, 35)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 6)//速度
            .Stats(S.health, 4000)//血量
            .Stats(S.armor, 15)//防御
            .Stats(S.stamina, 120)//耐力
            .Stats(S.lifespan, 77);//寿命
        PlagueBringer.SetCamp(KingdomAssets.Nurgle)//瘟疫使者
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetAnimIdleRaw("walk_0_0,walk_0_1")
            .SetIcon("actors/species/other/Cultiway/PlagueBringer/main/walk_0_0")
            .SetJumpAnimation(false)
            .SetHideHandItem(true)
            .SetDefaultWeapons(S_Item.spear_iron)//武器
            .AddTrait(S_Trait.battle_reflexes)//战斗反射
            .AddTrait(S_Trait.regeneration)//回复
            .AddTrait(S_Trait.bloodlust)//嗜血
            .AddTrait(S_Trait.plague)//瘟疫
            .AddTrait(S_Trait.blessed)//祝福
            .AddTrait(S_Trait.acid_blood)//酸血
            .AddTrait(S_Trait.acid_proof)//酸性
            .AddTrait(S_Trait.acid_touch)//酸性
            .AddSubspeciesTrait(S_SubspeciesTrait.stomach)//胃
            .AddSubspeciesTrait(S_SubspeciesTrait.diet_hematophagy)//hematophagy
            .AddSubspeciesTrait(S_SubspeciesTrait.hovering)//悬浮
            .AddSubspeciesTrait(S_SubspeciesTrait.reproduction_soulborne)// Soulborne reproduction
            .Stats(S.damage, 25)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 22)//速度
            .Stats(S.health, 10000)//血量
            .Stats(S.armor, 5)//防御
            .Stats(S.stamina, 120)//耐力
            .Stats(S.lifespan, 77);//寿命
        PlagueToad.SetCamp(KingdomAssets.Nurgle)//瘟疫蟾蜍
            .SetAnimWalk(S_Anim.walk_0, S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("actors/species/other/Cultiway/PlagueToad/main/walk_0")
            .SetJumpAnimation(true)
            .SetHideHandItem(true)
            .SetDefaultWeapons(S_Item.druid_staff)
            .AddTrait(S_Trait.battle_reflexes)//战斗反射
            .AddTrait(S_Trait.regeneration)//回复
            .AddTrait(S_Trait.bloodlust)//嗜血
            .AddTrait(S_Trait.immune)//免疫
            .AddTrait(S_Trait.contagious)//传染
            .AddTrait(S_Trait.blessed)//祝福
            .AddTrait(S_Trait.acid_blood)//酸血
            .AddTrait(S_Trait.acid_proof)//酸性
            .AddTrait(S_Trait.acid_touch)//酸性
            .AddSubspeciesTrait(S_SubspeciesTrait.stomach)//胃
            .AddSubspeciesTrait(S_SubspeciesTrait.diet_omnivore)//eat_omnivore
            .AddSubspeciesTrait(S_SubspeciesTrait.reproduction_soulborne)// Soulborne reproduction
            .Stats(S.damage, 40)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 5)//速度
            .Stats(S.attack_speed, 7)//攻击速度
            .Stats(S.health, 3500)//血量
            .Stats(S.armor, 12)//防御
            .Stats(S.stamina, 120)//耐力
            .Stats(S.lifespan, 77);//寿命
        GreatUncleanOneButcher.SetCamp(KingdomAssets.Nurgle)//大不净者-屠夫
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("actors/species/other/Cultiway/GreatUncleanOneButcher/main/walk_0")
            .SetJumpAnimation(true)
            .SetHideHandItem(true)
            .SetDefaultWeapons(S_Item.sword_iron)
            .AddTrait(S_Trait.battle_reflexes)//战斗反射
            .AddTrait(S_Trait.regeneration)//回复
            .AddTrait(S_Trait.giant)//大人
            .AddTrait(S_Trait.bloodlust)//嗜血
            .AddTrait(S_Trait.immune)//免疫
            .AddTrait(S_Trait.contagious)//传染
            .AddTrait(S_Trait.blessed)//祝福
            .AddTrait(S_Trait.acid_blood)//酸血
            .AddTrait(S_Trait.acid_proof)//酸性
            .AddTrait(S_Trait.acid_touch)//酸性
            .AddSubspeciesTrait(S_SubspeciesTrait.stomach)//胃
            .AddSubspeciesTrait(S_SubspeciesTrait.big_stomach)//大胃
            .AddSubspeciesTrait(S_SubspeciesTrait.diet_omnivore)//eat_omnivore
            .AddSubspeciesTrait(S_SubspeciesTrait.reproduction_soulborne)// Soulborne reproduction
            .Stats(S.damage, 80)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 3)//速度
            .Stats(S.health, 12000)//血量
            .Stats(S.armor, 30)//防御
            .Stats(S.stamina, 120)//耐力
            .Stats(S.lifespan, 777);//寿命
        GreatUncleanOneBellRinger.SetCamp(KingdomAssets.Nurgle)//大不净者-丧钟
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("actors/species/other/Cultiway/GreatUncleanOneBellRinger/main/walk_0")
            .SetJumpAnimation(true)
            .SetHideHandItem(true)
            .SetDefaultWeapons(S_Item.axe_iron)
            .AddTrait(S_Trait.battle_reflexes)//战斗反射
            .AddTrait(S_Trait.regeneration)//回复
            .AddTrait(S_Trait.giant)//大人
            .AddTrait(S_Trait.bloodlust)//嗜血
            .AddTrait(S_Trait.immune)//免疫
            .AddTrait(S_Trait.contagious)//传染
            .AddTrait(S_Trait.blessed)//祝福
            .AddTrait(S_Trait.acid_blood)//酸血
            .AddTrait(S_Trait.acid_proof)//酸性
            .AddTrait(S_Trait.acid_touch)//酸性
            .AddTrait(S_Trait.mega_heartbeat)//巨心
            .AddSubspeciesTrait(S_SubspeciesTrait.stomach)//胃
            .AddSubspeciesTrait(S_SubspeciesTrait.big_stomach)//大胃
            .AddSubspeciesTrait(S_SubspeciesTrait.diet_omnivore)//eat_omnivore
            .AddSubspeciesTrait(S_SubspeciesTrait.reproduction_soulborne)// Soulborne reproduction
            .Stats(S.damage, 70)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 3)//速度
            .Stats(S.health, 11000)//血量
            .Stats(S.armor, 25)//防御
            .Stats(S.stamina, 120)//耐力
            .Stats(S.lifespan, 777);//寿命
        GreatUncleanOneRainFather.SetCamp(KingdomAssets.Nurgle)//大不净者-雨父
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("actors/species/other/Cultiway/GreatUncleanOneRainFather/main/walk_0")
            .SetJumpAnimation(true)
            .SetHideHandItem(true)
            .SetDefaultWeapons(S_Item.white_staff)
            .AddTrait(S_Trait.arcane_reflexes)//魔力反射
            .AddTrait(S_Trait.wise)//智慧
            .AddTrait(S_Trait.heart_of_wizard)//魔力心
            .AddTrait(S_Trait.regeneration)//回复
            .AddTrait(S_Trait.giant)//大人
            .AddTrait(S_Trait.bloodlust)//嗜血
            .AddTrait(S_Trait.immune)//免疫
            .AddTrait(S_Trait.contagious)//传染
            .AddTrait(S_Trait.healing_aura)//回合治疗
            .AddTrait(S_Trait.blessed)//祝福
            .AddTrait(S_Trait.acid_blood)//酸血
            .AddTrait(S_Trait.acid_proof)//酸性
            .AddTrait(S_Trait.acid_touch)//酸性
            .AddJob(ActorJobs.SpawnedUnit)//召唤单位
            .AddSubspeciesTrait(S_SubspeciesTrait.stomach)//胃
            .AddSubspeciesTrait(S_SubspeciesTrait.big_stomach)//大胃
            .AddSubspeciesTrait(S_SubspeciesTrait.diet_omnivore)//eat_omnivore
            .AddSubspeciesTrait(S_SubspeciesTrait.reproduction_soulborne)// Soulborne reproduction
            .Stats(S.damage, 60)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 3)//速度
            .Stats(S.attack_speed, 4)//攻击速度
            .Stats(S.health, 10000)//血量
            .Stats(S.armor, 20)//防御
            .Stats(S.stamina, 120)//耐力
            .Stats(S.lifespan, 777);//寿命
        Daemonette.SetCamp(KingdomAssets.Slaanesh)//色孽欲魔
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("actors/species/other/Cultiway/Daemonette/main/walk_0")
            .SetJumpAnimation(false)
            .SetDefaultWeapons(S_Item.sword_adamantine)
            .AddTrait(S_Trait.regeneration)//回复
            .AddTrait(S_Trait.agile)//敏捷
            .AddTrait(S_Trait.blessed)//祝福
            .AddTrait(S_Trait.fast)//敏捷
            .AddTrait(S_Trait.fertile)//生长
            .AddTrait(S_Trait.thorns)//荆棘
            .AddTrait(S_Trait.shiny)//闪亮
            .AddTrait(S_Trait.attractive)//吸引
            .AddTrait(S_Trait.lustful)// lustful
            .AddTrait(S_Trait.battle_reflexes)//战斗反射
            .AddTrait(S_Trait.thief)//偷窃
            .AddTrait(S_Trait.venomous)//剧毒
            .AddTrait(S_Trait.poisonous)//剧毒
            .AddTrait(S_Trait.poison_immune)//毒抗
            .AddTrait(S_Trait.bloodlust)//嗜血
            .AddSubspeciesTrait(S_SubspeciesTrait.stomach)//胃
            .AddSubspeciesTrait(S_SubspeciesTrait.diet_omnivore)//eat_omnivore
            .AddSubspeciesTrait(S_SubspeciesTrait.reproduction_soulborne)// Soulborne reproduction
            .Stats(S.damage, 35)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 28)//速度
            .Stats(S.health, 150)//血量
            .Stats(S.armor, 3)//防御
            .Stats(S.stamina, 120)//耐力
            .Stats(S.lifespan, 66);//寿命
        Hellflayer.SetCamp(KingdomAssets.Slaanesh)//色孽磨魂者 
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("actors/species/other/Cultiway/Hellflayer/main/walk_0")
            .SetJumpAnimation(true)
            .SetHideHandItem(true)
            .SetDefaultWeapons(S_Item.shotgun)
            .AddTrait(S_Trait.regeneration)//回复
            .AddTrait(S_Trait.agile)//敏捷
            .AddTrait(S_Trait.blessed)//祝福
            .AddTrait(S_Trait.fertile)//生长
            .AddTrait(S_Trait.thorns)//荆棘
            .AddTrait(S_Trait.shiny)//闪亮
            .AddTrait(S_Trait.attractive)//吸引
            .AddTrait(S_Trait.lustful)// lustful
            .AddTrait(S_Trait.battle_reflexes)//战斗反射
            .AddTrait(S_Trait.thief)//偷窃
            .AddTrait(S_Trait.venomous)//剧毒
            .AddTrait(S_Trait.poisonous)//剧毒
            .AddTrait(S_Trait.poison_immune)//毒抗
            .AddTrait(S_Trait.bloodlust)//嗜血
            .AddSubspeciesTrait(S_SubspeciesTrait.stomach)//胃
            .AddSubspeciesTrait(S_SubspeciesTrait.diet_omnivore)//eat_omnivore
            .AddSubspeciesTrait(S_SubspeciesTrait.reproduction_soulborne)// Soulborne reproduction
            .Stats(S.damage, 50)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 6)//速度
            .Stats(S.attack_speed, 3)//攻击速度
            .Stats(S.health, 320)//血量
            .Stats(S.armor, 10)//防御
            .Stats(S.stamina, 120)//耐力
            .Stats(S.lifespan, 66);//寿命
        SlaaneshSeeker.SetCamp(KingdomAssets.Slaanesh)//色孽寻觅者
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("actors/species/other/Cultiway/SlaaneshSeeker/main/walk_0")
            .SetJumpAnimation(true)
            .SetHideHandItem(true)
            .SetAnimWalkSpeed(5f)//移动速度
            .SetAnimIdleSpeed(5f)//待机速度
            .SetAnimSwimSpeed(5f)//游动速度
            .SetDefaultWeapons(S_Item.spear_adamantine)
            .AddTrait(S_Trait.regeneration)//回复
            .AddTrait(S_Trait.agile)//敏捷
            .AddTrait(S_Trait.blessed)//祝福
            .AddTrait(S_Trait.fast)//敏捷
            .AddTrait(S_Trait.fertile)//生长
            .AddTrait(S_Trait.thorns)//荆棘
            .AddTrait(S_Trait.shiny)//闪亮
            .AddTrait(S_Trait.attractive)//吸引
            .AddTrait(S_Trait.lustful)// lustful
            .AddTrait(S_Trait.battle_reflexes)//战斗反射
            .AddTrait(S_Trait.thief)//偷窃
            .AddTrait(S_Trait.venomous)//剧毒
            .AddTrait(S_Trait.poisonous)//剧毒
            .AddTrait(S_Trait.poison_immune)//毒抗
            .AddTrait(S_Trait.bloodlust)//嗜血
            .AddSubspeciesTrait(S_SubspeciesTrait.stomach)//胃
            .AddSubspeciesTrait(S_SubspeciesTrait.diet_omnivore)//eat_omnivore
            .AddSubspeciesTrait(S_SubspeciesTrait.reproduction_soulborne)// Soulborne reproduction
            .Stats(S.damage, 40)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 18)//速度
            .Stats(S.health, 180)//血量
            .Stats(S.armor, 2)//防御
            .Stats(S.stamina, 120)//耐力
            .Stats(S.lifespan, 66);//寿命
        SlaaneshMistress.SetCamp(KingdomAssets.Slaanesh)//色孽女术士
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("actors/species/other/Cultiway/SlaaneshMistress/main/walk_0")
            .SetJumpAnimation(false)
            .SetHideHandItem(true)
            .SetDefaultWeapons(S_Item.necromancer_staff)//法术法杖
            .AddTrait(S_Trait.arcane_reflexes)//魔力反射
            .AddTrait(S_Trait.wise)//智慧
            .AddTrait(S_Trait.heart_of_wizard)//魔力心
            .AddTrait(S_Trait.regeneration)//回复
            .AddTrait(S_Trait.agile)//敏捷
            .AddTrait(S_Trait.blessed)//祝福
            .AddTrait(S_Trait.fertile)//生长
            .AddTrait(S_Trait.thorns)//荆棘
            .AddTrait(S_Trait.shiny)//闪亮
            .AddTrait(S_Trait.attractive)//吸引
            .AddTrait(S_Trait.lustful)// lustful
            .AddTrait(S_Trait.thief)//偷窃
            .AddTrait(S_Trait.venomous)//剧毒
            .AddTrait(S_Trait.poisonous)//剧毒
            .AddTrait(S_Trait.poison_immune)//毒抗
            .AddTrait(S_Trait.bloodlust)//嗜血
            .AddSubspeciesTrait(S_SubspeciesTrait.stomach)//胃
            .AddSubspeciesTrait(S_SubspeciesTrait.diet_omnivore)//eat_omnivore
            .AddSubspeciesTrait(S_SubspeciesTrait.reproduction_soulborne)// Soulborne reproduction
            .Stats(S.damage, 55)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 12)//速度
            .Stats(S.attack_speed, 4)//攻击速度
            .Stats(S.health, 200)//血量
            .Stats(S.armor, 1)//防御
            .Stats(S.stamina, 120)//耐力
            .Stats(S.lifespan, 66);//寿命
        SlaaneshFiend.SetCamp(KingdomAssets.Slaanesh)//色孽兽
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("actors/species/other/Cultiway/SlaaneshFiend/main/walk_0")
            .SetJumpAnimation(false)
            .SetHideHandItem(true)
            .SetAnimWalkSpeed(5f)//移动速度
            .SetAnimIdleSpeed(5f)//待机速度
            .SetAnimSwimSpeed(5f)//游动速度
            .SetDefaultWeapons(S_Item.axe_adamantine)
            .AddTrait(S_Trait.regeneration)//回复
            .AddTrait(S_Trait.agile)//敏捷
            .AddTrait(S_Trait.blessed)//祝福
            .AddTrait(S_Trait.fast)//敏捷
            .AddTrait(S_Trait.fertile)//生长
            .AddTrait(S_Trait.thorns)//荆棘
            .AddTrait(S_Trait.shiny)//闪亮
            .AddTrait(S_Trait.attractive)//吸引
            .AddTrait(S_Trait.lustful)// lustful
            .AddTrait(S_Trait.battle_reflexes)//战斗反射
            .AddTrait(S_Trait.thief)//偷窃
            .AddTrait(S_Trait.venomous)//剧毒
            .AddTrait(S_Trait.poisonous)//剧毒
            .AddTrait(S_Trait.poison_immune)//毒抗
            .AddTrait(S_Trait.bloodlust)//嗜血
            .AddSubspeciesTrait(S_SubspeciesTrait.stomach)//胃
            .AddSubspeciesTrait(S_SubspeciesTrait.diet_omnivore)//eat_omnivore
            .AddSubspeciesTrait(S_SubspeciesTrait.reproduction_soulborne)// Soulborne reproduction
            .Stats(S.damage, 45)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 18)//速度
            .Stats(S.health, 240)//血量
            .Stats(S.armor, 6)//防御
            .Stats(S.stamina, 120)//耐力
            .Stats(S.lifespan, 66);//寿命
        KeeperSecrets.SetCamp(KingdomAssets.Slaanesh)//守密者
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("actors/species/other/Cultiway/KeeperSecrets/main/walk_0")
            .SetJumpAnimation(true)
            .SetHideHandItem(true)
            .SetAnimWalkSpeed(5f)//移动速度
            .SetAnimIdleSpeed(5f)//待机速度
            .SetAnimSwimSpeed(5f)//游动速度
            .SetDefaultWeapons(S_Item.sword_adamantine)//剑
            .AddTrait(S_Trait.battle_reflexes)//战斗反射
            .AddTrait(S_Trait.wise)//智慧
            .AddTrait(S_Trait.giant)//大人
            .AddTrait(S_Trait.heart_of_wizard)//魔力心
            .AddTrait(S_Trait.regeneration)//回复
            .AddTrait(S_Trait.agile)//敏捷
            .AddTrait(S_Trait.blessed)//祝福
            .AddTrait(S_Trait.fertile)//生长
            .AddTrait(S_Trait.thorns)//荆棘
            .AddTrait(S_Trait.shiny)//闪亮
            .AddTrait(S_Trait.attractive)//吸引
            .AddTrait(S_Trait.lustful)// lustful
            .AddTrait(S_Trait.thief)//偷窃
            .AddTrait(S_Trait.venomous)//剧毒
            .AddTrait(S_Trait.poisonous)//剧毒
            .AddTrait(S_Trait.poison_immune)//毒抗
            .AddTrait(S_Trait.bloodlust)//嗜血
            .AddSubspeciesTrait(S_SubspeciesTrait.stomach)//胃
            .AddSubspeciesTrait(S_SubspeciesTrait.diet_omnivore)//eat_omnivore
            .AddSubspeciesTrait(S_SubspeciesTrait.reproduction_soulborne)// Soulborne reproduction
            .Stats(S.damage, 75)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 6)//速度
            .Stats(S.attack_speed, 4)//攻击速度
            .Stats(S.health, 6000)//血量
            .Stats(S.armor, 15)//防御
            .Stats(S.stamina, 120)//耐力
            .Stats(S.lifespan, 666);//寿命
        KeeperSecretsNakari.SetCamp(KingdomAssets.Slaanesh)//守密者纳卡里
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("actors/species/other/Cultiway/KeeperSecretsNakari/main/walk_0")
            .SetJumpAnimation(true)
            .SetHideHandItem(true)
            .SetDefaultWeapons(S_Item.sword_adamantine)//剑
            .AddTrait(S_Trait.battle_reflexes)//战斗反射
            .AddTrait(S_Trait.wise)//智慧
            .AddTrait(S_Trait.giant)//大人
            .AddTrait(S_Trait.heart_of_wizard)//魔力心
            .AddTrait(S_Trait.regeneration)//回复
            .AddTrait(S_Trait.agile)//敏捷
            .AddTrait(S_Trait.blessed)//祝福
            .AddTrait(S_Trait.fertile)//生长
            .AddTrait(S_Trait.thorns)//荆棘
            .AddTrait(S_Trait.shiny)//闪亮
            .AddTrait(S_Trait.attractive)//吸引
            .AddTrait(S_Trait.lustful)// lustful
            .AddTrait(S_Trait.thief)//偷窃
            .AddTrait(S_Trait.venomous)//剧毒
            .AddTrait(S_Trait.poisonous)//剧毒
            .AddTrait(S_Trait.poison_immune)//毒抗
            .AddTrait(S_Trait.bloodlust)//嗜血
            .AddSubspeciesTrait(S_SubspeciesTrait.stomach)//胃
            .AddSubspeciesTrait(S_SubspeciesTrait.diet_omnivore)//eat_omnivore
            .AddSubspeciesTrait(S_SubspeciesTrait.reproduction_soulborne)// Soulborne reproduction
            .Stats(S.damage, 80)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 6)//速度
            .Stats(S.attack_speed, 4)//攻击速度
            .Stats(S.health, 7000)//血量
            .Stats(S.armor, 16)//防御
            .Stats(S.stamina, 120)//耐力
            .Stats(S.lifespan, 666);//寿命
        ExaltedKeeperSecrets.SetCamp(KingdomAssets.Slaanesh)//神尊守密者
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("actors/species/other/Cultiway/ExaltedKeeperSecrets/main/walk_0")
            .SetJumpAnimation(true)
            .SetHideHandItem(true)
            .SetDefaultWeapons(S_Item.sword_adamantine)//剑
            .AddTrait(S_Trait.battle_reflexes)//战斗反射
            .AddTrait(S_Trait.wise)//智慧
            .AddTrait(S_Trait.giant)//大人
            .AddTrait(S_Trait.heart_of_wizard)//魔力心
            .AddTrait(S_Trait.regeneration)//回复
            .AddTrait(S_Trait.agile)//敏捷
            .AddTrait(S_Trait.blessed)//祝福
            .AddTrait(S_Trait.fertile)//生长
            .AddTrait(S_Trait.thorns)//荆棘
            .AddTrait(S_Trait.shiny)//闪亮
            .AddTrait(S_Trait.attractive)//吸引
            .AddTrait(S_Trait.lustful)// lustful
            .AddTrait(S_Trait.thief)//偷窃
            .AddTrait(S_Trait.venomous)//剧毒
            .AddTrait(S_Trait.poisonous)//剧毒
            .AddTrait(S_Trait.poison_immune)//毒抗
            .AddTrait(S_Trait.bloodlust)//嗜血
            .AddJob(ActorJobs.SpawnedUnit)//召唤单位
            .AddSubspeciesTrait(S_SubspeciesTrait.stomach)//胃
            .AddSubspeciesTrait(S_SubspeciesTrait.diet_omnivore)//eat_omnivore
            .AddSubspeciesTrait(S_SubspeciesTrait.reproduction_soulborne)// Soulborne reproduction
            .Stats(S.damage, 90)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 6)//速度
            .Stats(S.attack_speed, 4)//攻击速度
            .Stats(S.health, 8500)//血量
            .Stats(S.armor, 18)//防御
            .Stats(S.stamina, 120)//耐力
            .Stats(S.lifespan, 666);//寿命
        CravingManifestation.SetCamp(KingdomAssets.Crimson)//  渴求具象体
               .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
               .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
               .SetIcon("actors/species/other/Cultiway/CravingManifestation/main/walk_0")
               .SetJumpAnimation(true)
               .SetStandWhileSleeping(true)
               .AddTrait(S_Trait.bloodlust)//嗜血
               .AddSubspeciesTrait(S_SubspeciesTrait.stomach)//胃
               .AddSubspeciesTrait(S_SubspeciesTrait.diet_hematophagy)//hematophagy
               .AddSubspeciesTrait(S_SubspeciesTrait.gift_of_blood)//吸血
               .AddSubspeciesTrait(S_SubspeciesTrait.circadian_drift)//循环
               .AddSubspeciesTrait(S_SubspeciesTrait.energy_preserver)//保护
               .AddSubspeciesTrait(S_SubspeciesTrait.reproduction_metamorph)//蜕变
               .Stats(S.damage, 30)//伤害
               .Stats(S.damage_range, 0.12f)//伤害范围
               .Stats(S.speed, 18)//速度
               .Stats(S.health, 220)//血量
               .Stats(S.armor, 0)//防御
               .Stats(S.lifespan, 110);//寿命
        CrimsonScion.SetCamp(KingdomAssets.Crimson)//  猩红衍生物
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("actors/species/other/Cultiway/CrimsonScion/main/walk_0")
            .SetJumpAnimation(true)
            .AddTrait(S_Trait.bloodlust)//嗜血
            .AddSubspeciesTrait(S_SubspeciesTrait.stomach)//胃
            .AddSubspeciesTrait(S_SubspeciesTrait.diet_hematophagy)//hematophagy
            .AddSubspeciesTrait(S_SubspeciesTrait.gift_of_blood)//吸血
            .AddSubspeciesTrait(S_SubspeciesTrait.circadian_drift)//循环
            .AddSubspeciesTrait(S_SubspeciesTrait.energy_preserver)//保护
            .AddSubspeciesTrait(S_SubspeciesTrait.reproduction_metamorph)//蜕变
            .Stats(S.damage, 45)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 12)//速度
            .Stats(S.health, 400)//血量
            .Stats(S.armor, 5)//防御
            .Stats(S.lifespan, 150);//寿命
        CrimsonArbiter.SetCamp(KingdomAssets.Crimson)//  猩红判罚者
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("actors/species/other/Cultiway/CrimsonArbiter/main/walk_0")
            .SetJumpAnimation(true)
            .SetStandWhileSleeping(true)
            .AddTrait(S_Trait.bloodlust)//嗜血
            .AddSubspeciesTrait(S_SubspeciesTrait.stomach)//胃
            .AddSubspeciesTrait(S_SubspeciesTrait.diet_hematophagy)//hematophagy
            .AddSubspeciesTrait(S_SubspeciesTrait.gift_of_blood)//吸血
            .AddSubspeciesTrait(S_SubspeciesTrait.circadian_drift)//循环
            .AddSubspeciesTrait(S_SubspeciesTrait.energy_preserver)//保护
            .AddSubspeciesTrait(S_SubspeciesTrait.reproduction_metamorph)//蜕变
            .AddSubspeciesTrait(S_SubspeciesTrait.hovering)//悬浮
            .AddJob(ActorJobs.SpawnedUnit)//召唤单位
            .Stats(S.damage, 70)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 14)//速度
            .Stats(S.health, 6500)//血量
            .Stats(S.armor, 20)//防御
            .Stats(S.lifespan, 120);//寿命
        ServoSkull.SetCamp(KingdomAssets.Superman)//伺服颅骨
            .SetAnimWalk(S_Anim.walk_0, S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("actors/species/other/Cultiway/ServoSkull/main/walk_0")
            .SetJumpAnimation(false)
            .SetHideHandItem(true)
            .AddTrait(S_SubspeciesTrait.hovering)//悬浮
            .AddSubspeciesTrait(S_SubspeciesTrait.hovering)//悬浮
            .SetDefaultWeapons(S_Item.spear_adamantine)//武器
            .Stats(S.damage, 10)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 22)//速度
            .Stats(S.health, 60)//血量
            .Stats(S.armor, 0)//防御
            .Stats(S.stamina, 100)//耐力
            .Stats(S.lifespan, 999);//寿命
        Cherub.SetCamp(KingdomAssets.Superman)//智天使
            .SetAnimWalk(S_Anim.walk_0, S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("actors/species/other/Cultiway/Cherub/main/walk_5")
            .SetJumpAnimation(false)
            .SetHideHandItem(true)
            .AddTrait(S_SubspeciesTrait.hovering)//悬浮
            .AddSubspeciesTrait(S_SubspeciesTrait.hovering)//悬浮
            .SetDefaultWeapons(S_Item.bow_bronze)//弓
            .SetAnimWalkSpeed(1f)//移动速度
            .SetAnimIdleSpeed(1.5f)//待机速度
            .SetAnimSwimSpeed(1.5f)//游动速度
            .Stats(S.damage, 52)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 18)//速度
            .Stats(S.health, 280)//血量
            .Stats(S.armor, 8)//防御
            .Stats(S.stamina, 100)//耐力
            .Stats(S.lifespan, 999);//寿命
        TechPriests.SetCamp(KingdomAssets.Superman)//技术神甫
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("actors/species/other/Cultiway/TechPriests/main/walk_0")
            .SetJumpAnimation(false)
            .SetDefaultWeapons(S_Item.plague_doctor_staff)
            .Stats(S.damage, 35)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 10)//速度
            .Stats(S.health, 320)//血量
            .Stats(S.armor, 12)//防御
            .Stats(S.stamina, 100)//耐力
            .Stats(S.lifespan, 300);//寿命
        Emperor.SetCamp(KingdomAssets.Superman)//泰拉帝皇化身
            .SetAnimWalk(S_Anim.walk_1, S_Anim.walk_2, S_Anim.walk_3, S_Anim.walk_4, S_Anim.walk_5, S_Anim.walk_6, S_Anim.walk_7)
            .SetAnimSwimRaw("swim_0,swim_1,swim_2,swim_3,swim_4,swim_5,swim_6,swim_7")
            .SetIcon("actors/species/other/Cultiway/Emperor/main/walk_0")
            .SetJumpAnimation(false)
            .SetHideHandItem(true)
            .SetDefaultWeapons(S_Item.axe_adamantine)
            .Stats(S.damage, 80)//伤害
            .Stats(S.damage_range, 0.12f)//伤害范围
            .Stats(S.speed, 20)//速度
            .Stats(S.health, 3200)//血量
            .Stats(S.armor, 12)//防御
            .Stats(S.stamina, 100)//耐力
            .Stats(S.lifespan, 999);//寿命
            
            
            
            
            
            
    }
}