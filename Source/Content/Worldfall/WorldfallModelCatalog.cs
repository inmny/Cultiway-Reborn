#if S3COND2_WORLDFALL
using System;
using System.Collections.Generic;

namespace Cultiway.Content.Worldfall;

/// <summary>单位按实际身体结构共用的模型类别。</summary>
internal enum WorldfallBody
{
    /// <summary>直立双足生物。</summary>
    Biped,
    /// <summary>有枝干和树冠的树人。</summary>
    Tree,
    /// <summary>四足兽。</summary>
    Quadruped,
    /// <summary>鸟和翼龙。</summary>
    Bird,
    /// <summary>带翼直立生物。</summary>
    WingedBiped,
    /// <summary>长躯干东方龙。</summary>
    LongDragon,
    /// <summary>带翼的西方龙。</summary>
    WingedDragon,
    /// <summary>龟甲和蛇尾。</summary>
    Turtle,
    /// <summary>宽口短腿的蛙。</summary>
    Frog,
    /// <summary>长耳短尾的兔。</summary>
    Rabbit,
    /// <summary>软体团块。</summary>
    Blob,
    /// <summary>八条触手的章鱼。</summary>
    Octopus,
    /// <summary>四足下身和人形上身。</summary>
    Centaur,
    /// <summary>鱼尾和人形上身。</summary>
    Mermaid,
    /// <summary>机械底盘和炮台。</summary>
    Machine,
    /// <summary>悬浮灵体。</summary>
    Ghost,
    /// <summary>悬浮头骨。</summary>
    Skull,
    /// <summary>长颈四足恐龙。</summary>
    Sauropod,
    /// <summary>双足长尾恐龙。</summary>
    Theropod,
    /// <summary>带颈盾和角的四足恐龙。</summary>
    Triceratops,
    /// <summary>扁平游动生物。</summary>
    Ray,
    /// <summary>车厢式机车。</summary>
    Train
}

/// <summary>建筑按主体外形共用的模型类别。</summary>
internal enum WorldfallBuilding
{
    /// <summary>有柱梁和四面屋顶的殿堂。</summary>
    Hall,
    /// <summary>多层藏经或宝阁。</summary>
    Pagoda,
    /// <summary>有顶部装饰的塔。</summary>
    Tower,
    /// <summary>主楼和角楼组成的城堡。</summary>
    Castle,
    /// <summary>分层金字塔。</summary>
    Pyramid,
    /// <summary>贴地法阵。</summary>
    Portal,
    /// <summary>站台和棚顶。</summary>
    Station,
    /// <summary>树木和大型群系物件。</summary>
    Tree,
    /// <summary>小型花草和群系物件。</summary>
    Plant,
    /// <summary>沿水面的码头。</summary>
    Docks,
    /// <summary>带可动风叶的风车。</summary>
    Windmill,
    /// <summary>带背板和护卫柱的王座。</summary>
    Throne,
    /// <summary>晶体和能源核心。</summary>
    Crystal
}

/// <summary>明确列出不同外形的单位，未列出的直立奇幻单位使用双足骨架。</summary>
internal static class WorldfallModelCatalog
{
    /// <summary>与有效单位资产名称一致的身体分类。</summary>
    private static readonly Dictionary<string, WorldfallBody> Bodies = CreateBodies();

    /// <summary>建立非通用双足单位的分类表。</summary>
    /// <returns>按资产名称索引的分类。</returns>
    private static Dictionary<string, WorldfallBody> CreateBodies()
    {
        Dictionary<string, WorldfallBody> result = new(StringComparer.Ordinal);
        Add(WorldfallBody.Tree, "AcaciaTreants BanyanTreants CoconutTreants OakTreants SycamoreTreants DeathTreants FireTreants");
        Add(WorldfallBody.Quadruped, "BloodBeast Sphinx FairyFox QiLin NineColoredDeer WhiteTiger Deer Horse Panda Pig WildBoar Lion Tiger FleshHoundKhorne BloodcrusherKhorne SlaaneshSeeker SlaaneshFiend Hellflayer GriffinKnight");
        Add(WorldfallBody.Bird, "FengHuang JinWu ZhuQue Rooster Eagle Mallard Pterodactyl");
        Add(WorldfallBody.LongDragon, "QingLong");
        Add(WorldfallBody.WingedDragon, "FireWyvern Ossaurus");
        Add(WorldfallBody.WingedBiped, "Bloodthirster AnggrathUnbound ExaltedBloodthirster LordChange KairosFateweaver ExaltedLordChange Cherub");
        Add(WorldfallBody.Turtle, "XuanWu");
        Add(WorldfallBody.Frog, "YuChan PlagueToad");
        Add(WorldfallBody.Rabbit, "YueTu");
        Add(WorldfallBody.Blob, "KingSlime CravingManifestation");
        Add(WorldfallBody.Octopus, "GiantOctopus");
        Add(WorldfallBody.Centaur, "Centaur HalfDeerMan");
        Add(WorldfallBody.Mermaid, "Mermaid");
        Add(WorldfallBody.Machine, "DestroyRobot FortRobot TankRobot SkullCannonKhorne");
        Add(WorldfallBody.Ghost, "BodilessYuanshen YuanyingSoul GhostFire CandleGenie KnowledgeGenie NurgleSpirit FlamerTzeentch");
        Add(WorldfallBody.Skull, "ServoSkull");
        Add(WorldfallBody.Sauropod, "Diplodocus Dreadnoughtus");
        Add(WorldfallBody.Theropod, "TyrannosaurusRex Velociraptor");
        Add(WorldfallBody.Triceratops, "Triceratops");
        Add(WorldfallBody.Ray, "ScreamersTzeentch");
        Add(WorldfallBody.Train, "Train");
        Add(WorldfallBody.Tree, "Plant");
        return result;

        void Add(WorldfallBody shape, string names)
        {
            foreach (string name in names.Split(' ')) result.Add("Cultiway." + name, shape);
        }
    }

    /// <summary>取得一个已注册单位的身体类别。</summary>
    /// <param name="id">单位资产名称。</param>
    /// <returns>适合该单位的身体结构。</returns>
    internal static WorldfallBody BodyOf(string id) => Bodies.TryGetValue(id, out WorldfallBody shape) ? shape : WorldfallBody.Biped;

    /// <summary>按资产用途选择建筑形体。</summary>
    /// <param name="asset">已注册建筑。</param>
    /// <returns>与该建筑用途匹配的形体。</returns>
    internal static WorldfallBuilding BuildingOf(BuildingAsset asset)
    {
        string id = asset.id;
        if (asset.type == "type_tree") return WorldfallBuilding.Tree;
        if (asset.type == "type_vegetation") return WorldfallBuilding.Plant;
        if (id == Buildings.TeleportArray.id) return WorldfallBuilding.Portal;
        if (id == Buildings.TrainStation.id) return WorldfallBuilding.Station;
        if (id == Buildings.Pyramid.id) return WorldfallBuilding.Pyramid;
        if (id == Buildings.GoldenThrone.id) return WorldfallBuilding.Throne;
        if (id == Buildings.CrimsonCore.id) return WorldfallBuilding.Crystal;
        if (id == Buildings.BloodCastle.id || id == Buildings.SkavenBlight.id) return WorldfallBuilding.Castle;
        if (id == Buildings.VivisomaticArbor.id) return WorldfallBuilding.Tree;
        if (id == Buildings.SectScripturePavilion.id || id == Buildings.SectTreasurePavilion.id) return WorldfallBuilding.Pagoda;
        if (id.Contains("docks_")) return WorldfallBuilding.Docks;
        if (id.Contains("windmill_")) return WorldfallBuilding.Windmill;
        if (id.Contains("Tower") || id.Contains("watch_tower_")) return WorldfallBuilding.Tower;
        return WorldfallBuilding.Hall;
    }
}
#endif