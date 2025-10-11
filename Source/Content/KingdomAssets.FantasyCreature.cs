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
    private void SetupFantasyCreatureKingdoms()
    {
        Vampire.addTag(nameof(Vampire));             //血族标签
        Vampire.addFriendlyTag(nameof(Vampire));     //对血族标签友好
        Vampire.setIcon("cultiway/icons/races/iconBloodsucker");
        Vampire.default_kingdom_color = ColorAsset.tryMakeNewColorAsset("#711602");
        AllEnemyWith(nameof(Vampire), SK.civ); // 所有文明阵营都会主动攻击吸血鬼，civ（公民）

        TreantsGood.addTag(nameof(TreantsGood));             //树人-善标签
        TreantsGood.addFriendlyTag(nameof(TreantsGood));     //对树人-善标签友好
        TreantsGood.setIcon("cultiway/icons/races/iconBloodsucker");
        TreantsGood.default_kingdom_color = ColorAsset.tryMakeNewColorAsset("#711602");
        AllEnemyWith(nameof(TreantsGood), SK.civ); // 所有文明阵营都会主动攻击吸血鬼，civ（公民）

        TreantsEvil.addTag(nameof(TreantsEvil));             //树人-恶标签
        TreantsEvil.addFriendlyTag(nameof(TreantsEvil));     //对树人-恶标签友好
        TreantsEvil.setIcon("cultiway/icons/races/iconBloodsucker");
        TreantsEvil.default_kingdom_color = ColorAsset.tryMakeNewColorAsset("#711602");
        AllEnemyWith(nameof(TreantsEvil), SK.civ); // 所有文明阵营都会主动攻击吸血鬼，civ（公民）
    }
    
}