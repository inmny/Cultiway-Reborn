using Cultiway.Abstract;
using Cultiway.Debug;
using NeoModLoader;
using strings;

namespace Cultiway.Content;

public partial class KingdomAssets
{
    /// <summary>
    /// 血族
    /// </summary>
    [CloneSource(KingdomLibrary.TEMPLATE_MOB)]
    public static KingdomAsset Vampire { get; private set; }
    [CloneSource(KingdomLibrary.TEMPLATE_MOB)]
    public static KingdomAsset TreantsGood { get; private set; }
    [CloneSource(KingdomLibrary.TEMPLATE_MOB)]
    public static KingdomAsset TreantsEvil { get; private set; }
    [CloneSource(KingdomLibrary.TEMPLATE_MOB)]
    public static KingdomAsset Robot { get; private set; }
    [CloneSource(KingdomLibrary.TEMPLATE_MOB)]
    public static KingdomAsset FishPeople { get; private set; }
    [CloneSource(KingdomLibrary.TEMPLATE_MOB)]
    public static KingdomAsset Fairy { get; private set; }
    [CloneSource(KingdomLibrary.TEMPLATE_MOB)]
    public static KingdomAsset Spirit { get; private set; }
    [CloneSource(KingdomLibrary.TEMPLATE_MOB)]
    public static KingdomAsset Goblin { get; private set; }
    [CloneSource(KingdomLibrary.TEMPLATE_MOB)]
    public static KingdomAsset VegetarianDinosaur { get; private set; }
    [CloneSource(KingdomLibrary.TEMPLATE_MOB)]
    public static KingdomAsset CarnivorousDinosaur { get; private set; }
    [CloneSource(KingdomLibrary.TEMPLATE_MOB)]
    public static KingdomAsset Titan { get; private set; }
    [CloneSource(KingdomLibrary.TEMPLATE_MOB)]
    public static KingdomAsset Divine { get; private set; }
    [CloneSource(KingdomLibrary.TEMPLATE_MOB)]
    public static KingdomAsset Superman { get; private set; }
    [CloneSource(KingdomLibrary.TEMPLATE_MOB)]
    public static KingdomAsset DemiHuman { get; private set; }
    [CloneSource(KingdomLibrary.TEMPLATE_MOB)]
    public static KingdomAsset Monster { get; private set; }
    [CloneSource(KingdomLibrary.TEMPLATE_MOB)]
    public static KingdomAsset Herbivore { get; private set; }
    [CloneSource(KingdomLibrary.TEMPLATE_MOB)]
    public static KingdomAsset Carnivorous { get; private set; }
    [CloneSource(KingdomLibrary.TEMPLATE_MOB)]
    public static KingdomAsset Nurgle { get; private set; }
    private void SetupFantasyCreatureKingdoms()
    {
        Vampire.addTag(nameof(Vampire));             //血族标签
        Vampire.addFriendlyTag(nameof(Vampire));     //对血族标签友好
        Vampire.setIcon("cultiway/icons/races/iconBloodsucker");
        Vampire.default_kingdom_color = ColorAsset.tryMakeNewColorAsset("#711602");
        AllEnemyWith(nameof(Vampire), SK.civ); // 所有文明阵营都会主动攻击吸血鬼，civ（公民）

        TreantsGood.addTag(nameof(TreantsGood));             //树人-善标签
        TreantsGood.addFriendlyTag(nameof(TreantsGood));     //对树人-善标签友好
        TreantsGood.setIcon("cultiway/icons/races/iconOak_Treants");
        TreantsGood.default_kingdom_color = ColorAsset.tryMakeNewColorAsset("#027109ff");
        TreantsGood.addFriendlyTag(SK.elf);//妖精对精灵友好
        AllFriendWith(nameof(TreantsGood), SK.elf);//所有文明阵营都会主动攻击妖精，elf（精灵）
        AllEnemyWith(nameof(TreantsGood), SK.orc); //所有文明阵营都会主动攻击妖精，orc（兽人）
        AllEnemyWith(nameof(TreantsGood), SK.dwarf);//所有文明阵营都会主动攻击妖精，dwarf（矮人）
        AllEnemyWith(nameof(TreantsGood), SK.human);//所有文明阵营都会主动攻击妖精，human（人类）

        TreantsEvil.addTag(nameof(TreantsEvil));             //树人-恶标签
        TreantsEvil.addFriendlyTag(nameof(TreantsEvil));     //对树人-恶标签友好
        TreantsEvil.setIcon("cultiway/icons/races/iconFire_Treants");
        TreantsEvil.default_kingdom_color = ColorAsset.tryMakeNewColorAsset("#710206ff");
        AllEnemyWith(nameof(TreantsEvil), SK.civ); // 所有文明阵营都会主动攻击恶树，civ（公民）
        
        Robot.addTag(nameof(Robot));             //机器人标签
        Robot.addFriendlyTag(nameof(Robot));     //对机器人标签友好
        Robot.setIcon("cultiway/icons/races/iconDestroy_Robot");
        Robot.default_kingdom_color = ColorAsset.tryMakeNewColorAsset("#710206ff");
        Robot.addFriendlyTag(SK.dwarf);//机器人对矮人友好
        AllFriendWith(nameof(Robot), SK.dwarf);//所有文明阵营都会主动攻击机器人，dwarf（矮人）
        AllEnemyWith(nameof(Robot), SK.orc); //所有文明阵营都会主动攻击妖精，orc（兽人）
        AllEnemyWith(nameof(Robot), SK.elf);//所有文明阵营都会主动攻击机器人，elf（精灵）
        AllEnemyWith(nameof(Robot), SK.human);//所有文明阵营都会主动攻击妖精，human（人类）
                
        FishPeople.addTag(nameof(FishPeople));             //鱼人标签
        FishPeople.addFriendlyTag(nameof(FishPeople));     //对鱼人标签友好
        FishPeople.setIcon("cultiway/icons/races/iconFishPeople_Shaman");
        FishPeople.default_kingdom_color = ColorAsset.tryMakeNewColorAsset("#023b71ff");
        AllEnemyWith(nameof(FishPeople), SK.civ); // 所有文明阵营都会主动攻击鱼人，civ（公民）
                        
        Fairy.addTag(nameof(Fairy));             //妖精标签
        Fairy.addFriendlyTag(nameof(Fairy));     //对妖精标签友好
        Fairy.setIcon("cultiway/icons/races/iconFairy_Druid");
        Fairy.default_kingdom_color = ColorAsset.tryMakeNewColorAsset("#02714cff");
        Fairy.addFriendlyTag(SK.elf);//妖精对精灵友好
        AllFriendWith(nameof(Fairy), SK.elf);//所有文明阵营都会主动攻击妖精，elf（精灵）
        AllEnemyWith(nameof(Fairy), SK.orc); //所有文明阵营都会主动攻击妖精，orc（兽人）
        AllEnemyWith(nameof(Fairy), SK.dwarf);//所有文明阵营都会主动攻击妖精，dwarf（矮人）
        AllEnemyWith(nameof(Fairy), SK.human);//所有文明阵营都会主动攻击妖精，human（人类）
                        
        Spirit.addTag(nameof(Spirit));             //灵族标签
        Spirit.addFriendlyTag(nameof(Spirit));     //对灵族标签友好
        Spirit.setIcon("cultiway/icons/races/iconKnowledge_Genie");
        Spirit.default_kingdom_color = ColorAsset.tryMakeNewColorAsset("#0bdfd8ff");
        AllEnemyWith(nameof(Spirit), SK.civ); // 所有文明阵营都会主动攻击灵族，civ（公民）
                        
        Goblin.addTag(nameof(Goblin));             //哥布林标签
        Goblin.addFriendlyTag(nameof(Goblin));     //对哥布林标签友好
        Goblin.setIcon("cultiway/icons/races/iconGoblin_Warrior");
        Goblin.default_kingdom_color = ColorAsset.tryMakeNewColorAsset("#117102ff");
        Goblin.addFriendlyTag(SK.orc);//哥布林对兽人友好
        AllFriendWith(nameof(Goblin), SK.orc);//所有文明阵营都会主动攻击哥布林，orc（兽人）
        AllEnemyWith(nameof(Goblin), SK.human); //所有文明阵营都会主动攻击哥布林，human（人类）
        AllEnemyWith(nameof(Goblin), SK.dwarf);//所有文明阵营都会主动攻击妖精，dwarf（矮人）
        AllEnemyWith(nameof(Goblin), SK.elf);//所有文明阵营都会主动攻击妖精，elf（精灵）
                        
        VegetarianDinosaur.addTag(nameof(VegetarianDinosaur));             //素食恐龙标签
        VegetarianDinosaur.addFriendlyTag(nameof(VegetarianDinosaur));     //对素食恐龙标签友好
        VegetarianDinosaur.setIcon("cultiway/icons/races/iconTriceratops");
        VegetarianDinosaur.default_kingdom_color = ColorAsset.tryMakeNewColorAsset("#027130ff");
        AllEnemyWith(nameof(VegetarianDinosaur), SK.civ); // 所有文明阵营都会主动攻击素食恐龙，civ（公民）
                        
        CarnivorousDinosaur.addTag(nameof(CarnivorousDinosaur));             //食肉恐龙标签
        CarnivorousDinosaur.addFriendlyTag(nameof(CarnivorousDinosaur));     //对食肉恐龙标签友好
        CarnivorousDinosaur.setIcon("cultiway/icons/races/iconVelociraptor");
        CarnivorousDinosaur.default_kingdom_color = ColorAsset.tryMakeNewColorAsset("#710202ff");
        AllEnemyWith(nameof(CarnivorousDinosaur), SK.civ); // 所有文明阵营都会主动攻击食肉恐龙，civ（公民）
                        
        Titan.addTag(nameof(Titan));             //泰坦标签
        Titan.addFriendlyTag(nameof(Titan));     //对泰坦标签友好
        Titan.setIcon("cultiway/icons/races/iconVolcanic_Giant");
        Titan.default_kingdom_color = ColorAsset.tryMakeNewColorAsset("#667102ff");
        AllEnemyWith(nameof(Titan), SK.civ); // 所有文明阵营都会主动攻击泰坦，civ（公民）
                                
        Divine.addTag(nameof(Divine));             //神圣标签
        Divine.addFriendlyTag(nameof(Divine));     //对神圣标签友好
        Divine.setIcon("cultiway/icons/races/iconFairy_Fox");
        Divine.default_kingdom_color = ColorAsset.tryMakeNewColorAsset("#e9ec0aff");
        AllEnemyWith(nameof(Divine), SK.civ); // 所有文明阵营都会主动攻击神圣，civ（公民）
                                
        Superman.addTag(nameof(Superman));             //超人标签
        Superman.addFriendlyTag(nameof(Superman));     //对超人标签友好
        Superman.setIcon("cultiway/icons/races/iconGriffin_Knight");
        Superman.default_kingdom_color = ColorAsset.tryMakeNewColorAsset("#8e08dbff");
        Superman.addFriendlyTag(SK.human);//超人对人类友好
        AllFriendWith(nameof(Superman), SK.human);//所有文明阵营都会主动攻击超人，human（人类）
        AllEnemyWith(nameof(Superman), SK.orc); //所有文明阵营都会主动攻击妖精，orc（兽人）
        AllEnemyWith(nameof(Superman), SK.dwarf);//所有文明阵营都会主动攻击妖精，dwarf（矮人）
        AllEnemyWith(nameof(Superman), SK.elf);//所有文明阵营都会主动攻击妖精，elf（精灵）
                                
        DemiHuman.addTag(nameof(DemiHuman));             //亚人标签
        DemiHuman.addFriendlyTag(nameof(DemiHuman));     //对亚人标签友好
        DemiHuman.setIcon("cultiway/icons/races/iconHalf_DeerMan");
        DemiHuman.default_kingdom_color = ColorAsset.tryMakeNewColorAsset("#023b71ff");
        AllEnemyWith(nameof(DemiHuman), SK.civ); // 所有文明阵营都会主动攻击亚人，civ（公民）
                                
        Monster.addTag(nameof(Monster));             //怪物标签
        Monster.addFriendlyTag(nameof(Monster));     //对怪物标签友好
        Monster.setIcon("cultiway/icons/races/iconWerewolf");
        Monster.default_kingdom_color = ColorAsset.tryMakeNewColorAsset("#027109ff");
        AllEnemyWith(nameof(Monster), SK.civ); // 所有文明阵营都会主动攻击怪物，civ（公民）
                                        
        Herbivore.addTag(nameof(Herbivore));             //食草标签
        Herbivore.addFriendlyTag(nameof(Herbivore));     //对食草标签友好
        Herbivore.setIcon("cultiway/icons/races/iconWerewolf");
        Herbivore.default_kingdom_color = ColorAsset.tryMakeNewColorAsset("#027109ff");
        AllEnemyWith(nameof(Herbivore), SK.civ); // 所有文明阵营都会主动攻击食草，civ（公民）
                                        
        Carnivorous.addTag(nameof(Carnivorous));             //食肉标签
        Carnivorous.addFriendlyTag(nameof(Carnivorous));     //对食肉标签友好
        Carnivorous.setIcon("cultiway/icons/races/iconWerewolf");
        Carnivorous.default_kingdom_color = ColorAsset.tryMakeNewColorAsset("#027109ff");
        AllEnemyWith(nameof(Carnivorous), SK.civ); // 所有文明阵营都会主动攻击食肉，civ（公民）
                                                
        Nurgle.addTag(nameof(Nurgle));             //纳垢标签
        Nurgle.addFriendlyTag(nameof(Nurgle));     //对纳垢标签友好
        Nurgle.setIcon("cultiway/icons/races/iconWerewolf");
        Nurgle.default_kingdom_color = ColorAsset.tryMakeNewColorAsset("#027109ff");
        AllEnemyWith(nameof(Nurgle), SK.civ); // 所有文明阵营都会主动攻击纳垢，civ（公民）
    }
    
}