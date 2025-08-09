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
    private void SetupFantasyCreatureKingdoms()
    { 
        Vampire.addTag(nameof(Vampire));             //血族标签
        Vampire.addFriendlyTag(nameof(Vampire));     //对血族标签友好
        Vampire.setIcon("cultiway/icons/races/iconBloodsucker");
        Vampire.default_kingdom_color = ColorAsset.tryMakeNewColorAsset("#711602");
        AllEnemyWith(nameof(Vampire), SK.civ); // 所有文明阵营都会主动攻击吸血鬼，civ（公民）

    }
}