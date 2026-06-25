using Cultiway.Abstract;
using System;

namespace Cultiway;

public partial class WorldboxGame
{
    public class NameGenerators : ExtendLibrary<NameGeneratorAsset, NameGenerators>
    {
        private const string GeoRegionBiomePool = "青草,苍林,翠叶,金沙,霜原,荒石,红枫,花野,雨林,沼泽";
        private const string PrimaryTemplates = "prefix,core,type|core,type|prefix,core,type,suffix";
        private const string LandmassTemplates = "biome,prefix,type|prefix,core,type|biome,core,type,suffix";
        private const string MorphologyTemplates = "prefix,core,type|biome,prefix,type|prefix,core,type,suffix";

        private const string PrimaryPool = "苍,玄,灵,碧,赤,霁,岚,潮,澜,云,霜,星|渚,汀,泽,湾,浦,岙,屿,崖,川,陂,岬,潮|境,域,地带,海隅,沿岸";
        private const string LandformPool = "苍,玄,青,赤,寒,云,星,龙,玉,峻|岭,梁,冈,坳,阜,峪,岗,垣,塬,谷|地,地带,山系,地界";
        private const string LandmassPool = "灵,苍,玄,碧,丹,银,霁,岚,潮,霜|洲,屿,垠,岬,汀,岙,涯,浦,堤,滩|界,域,地,沿岸";
        private const string MorphologyPool = "回,断,双,玉,玄,苍,潮,云,霜,灵|门,隘,湾,链,列,桥,脉,汊,角,岬|带,域,线,区";
        private const string LavaPool = "赤,炎,焰,烬,熔,灼,炽,焦|渊,脉,谷,壑,池,流,原,口|熔原,火境,地带";
        private const string GooPool = "腐,瘴,灰,毒,浊,疫,黯,蚀|泽,池,沼,沟,原,洼|疫境,泥沼,污地";
        private const string MountainPool = "苍,玄,云,寒,峻,铁,青,霜|峰,岭,岳,脉,峦,崖,冈|群峰,山系,岭";
        private const string GrasslandPool = "青,翠,苍,风,晴,牧,芳,绿|原,野,甸,坪,坡,丘,畴|绿野,牧野,草场";
        private const string ForestPool = "苍,翠,青,森,松,枫,幽|林,森,木,荫,麓,谷|林地,密林,森林";
        private const string JunglePool = "莽,翠,雨,藤,热,幽,绿|林,谷,泽,藤,荫,岭|雨林,密林,丛林";
        private const string SwampPool = "泥,雾,腐,幽,绿,苔,湿|沼,泽,洼,淖,池,沟|湿地,泥沼,沼泽";
        private const string DesertPool = "黄,金沙,炎,赤,旱,灼,燧|沙,丘,漠,原,海,垄|沙海,旱原,沙地";
        private const string BeachPool = "白,金沙,潮,晴,贝,珊,碧|滩,岸,湾,沙,汀,渚|沙岸,海滩,潮岸";
        private const string TundraPool = "霜,雪,寒,白,冻,凛|原,野,岭,川,坡,地|冻土,雪境,寒原";
        private const string HighlandsPool = "高,云,苍,风,青,峻|台,原,岭,塬,坡,岗|高原,高地,台地";
        private const string WastelandPool = "荒,枯,焦,灰,寂,断|原,野,地,丘,坡,沟|废土,荒地,荒原";
        private const string SpecialPool = "奇,幻,秘,灵,异,玄|境,域,地,谷,渊,界|秘境,异域,奇境";

        public static NameGeneratorAsset Cultibook { get; private set; }
        public static NameGeneratorAsset Sect { get; private set; }
        public static NameGeneratorAsset GeoRegion { get; private set; }

        public static NameGeneratorAsset PrimarySea { get; private set; }
        public static NameGeneratorAsset PrimaryLake { get; private set; }
        public static NameGeneratorAsset PrimaryRiver { get; private set; }
        public static NameGeneratorAsset PrimaryLava { get; private set; }
        public static NameGeneratorAsset PrimaryGoo { get; private set; }
        public static NameGeneratorAsset PrimaryMountains { get; private set; }
        public static NameGeneratorAsset PrimaryGrassland { get; private set; }
        public static NameGeneratorAsset PrimaryForest { get; private set; }
        public static NameGeneratorAsset PrimaryJungle { get; private set; }
        public static NameGeneratorAsset PrimarySwamp { get; private set; }
        public static NameGeneratorAsset PrimaryDesert { get; private set; }
        public static NameGeneratorAsset PrimaryBeach { get; private set; }
        public static NameGeneratorAsset PrimaryTundra { get; private set; }
        public static NameGeneratorAsset PrimaryHighlands { get; private set; }
        public static NameGeneratorAsset PrimaryWasteland { get; private set; }
        public static NameGeneratorAsset PrimarySpecial { get; private set; }
        public static NameGeneratorAsset LandformPlain { get; private set; }
        public static NameGeneratorAsset LandformMountain { get; private set; }
        public static NameGeneratorAsset LandformCanyon { get; private set; }
        public static NameGeneratorAsset LandformBasin { get; private set; }
        public static NameGeneratorAsset LandmassIsland { get; private set; }
        public static NameGeneratorAsset LandmassMainland { get; private set; }
        public static NameGeneratorAsset Peninsula { get; private set; }
        public static NameGeneratorAsset Strait { get; private set; }
        public static NameGeneratorAsset Archipelago { get; private set; }

        protected override bool AutoRegisterAssets() => true;

        protected override void OnInit()
        {
            Cultibook.use_dictionary = true;
            Cultibook.addDictPart("basic", "引气,纳元,聚灵,淬体,锻筋,养气,炼神,化虚");
            Cultibook.addDictPart("element", "焚天,御水,青木,厚土,踏雷,风影");
            Cultibook.addDictPart("nature", "鹤鸣,龙腾,星衍,月轮,云游,山岳");
            Cultibook.addDictPart("postfix", "诀,经,法,录,术,典,秘录,要术,宝鉴,真解,玄功,功");
            Cultibook.addTemplate("basic,postfix");
            Cultibook.addTemplate("element,postfix");
            Cultibook.addTemplate("nature,postfix");

            Sect.use_dictionary = true;
            Sect.addDictPart("basic", "引气,纳元,聚灵,淬体,锻筋,养气,炼神,化虚");
            Sect.addDictPart("element", "焚天,御水,青木,厚土,踏雷,风影");
            Sect.addDictPart("nature", "鹤鸣,龙腾,星衍,月轮,云游,山岳");
            Sect.addDictPart("postfix", string.Join(",", "宗门教派宫殿楼阁轩府寺观山谷洞庄盟会堂帮".ToCharArray()));
            Sect.addTemplate("basic,postfix");
            Sect.addTemplate("element,postfix");
            Sect.addTemplate("nature,postfix");

            GeoRegion.use_dictionary = true;
            GeoRegion.addDictPart("prefix", "幽,灵,玄,苍,赤,青,翠,金,玉,碧,雷,云,炎,暮,澄,皓,逐,飞,落,瑶,天,渊,星,炎,风,晨,暮,浮,归,灵,玉,紫,虹,恒,寒,霜,清,岚,琅,晴,烟,雪,渺,泠,照,隐,澜,冥,明");
            GeoRegion.addDictPart("base", "谷,川,岗,泉,山,林,原,丘,岸,滩,岛,岭,涧,池,沼,森,湾,湿地,崖,河,洞,丘陵,平原,海,台,坡,岗,湖,洲,隘,壑,泽,峡,脉,林地,崖壁,荒原,沟,滩,流,溪,沙地,盆,围");
            GeoRegion.addDictPart("postfix", "地,境,域,地带,边,丘,岭,界,野,地界,苍,涧,涂,岸,原,地脉,渊");
            GeoRegion.addTemplate("prefix,base");
            GeoRegion.addTemplate("prefix,base,postfix");
            GeoRegion.addTemplate("base,postfix");

            ConfigureGeoRegionNameGenerator(PrimarySea, "海", PrimaryPool, PrimaryTemplates);
            ConfigureGeoRegionNameGenerator(PrimaryLake, "湖", PrimaryPool, PrimaryTemplates);
            ConfigureGeoRegionNameGenerator(PrimaryRiver, "河", PrimaryPool, PrimaryTemplates);
            ConfigureGeoRegionNameGenerator(PrimaryLava, "熔岩地带", LavaPool, PrimaryTemplates);
            ConfigureGeoRegionNameGenerator(PrimaryGoo, "灰疫之地", GooPool, PrimaryTemplates);
            ConfigureGeoRegionNameGenerator(PrimaryMountains, "山脉", MountainPool, PrimaryTemplates);
            ConfigureGeoRegionNameGenerator(PrimaryGrassland, "草原", GrasslandPool, PrimaryTemplates);
            ConfigureGeoRegionNameGenerator(PrimaryForest, "森林", ForestPool, PrimaryTemplates);
            ConfigureGeoRegionNameGenerator(PrimaryJungle, "丛林", JunglePool, PrimaryTemplates);
            ConfigureGeoRegionNameGenerator(PrimarySwamp, "沼泽", SwampPool, PrimaryTemplates);
            ConfigureGeoRegionNameGenerator(PrimaryDesert, "沙漠", DesertPool, PrimaryTemplates);
            ConfigureGeoRegionNameGenerator(PrimaryBeach, "海滩", BeachPool, PrimaryTemplates);
            ConfigureGeoRegionNameGenerator(PrimaryTundra, "雪原", TundraPool, PrimaryTemplates);
            ConfigureGeoRegionNameGenerator(PrimaryHighlands, "高地", HighlandsPool, PrimaryTemplates);
            ConfigureGeoRegionNameGenerator(PrimaryWasteland, "荒原", WastelandPool, PrimaryTemplates);
            ConfigureGeoRegionNameGenerator(PrimarySpecial, "奇境", SpecialPool, PrimaryTemplates);
            ConfigureGeoRegionNameGenerator(LandformPlain, "平原", LandformPool, PrimaryTemplates);
            ConfigureGeoRegionNameGenerator(LandformMountain, "山地", MountainPool, PrimaryTemplates);
            ConfigureGeoRegionNameGenerator(LandformCanyon, "峡谷", LandformPool, PrimaryTemplates);
            ConfigureGeoRegionNameGenerator(LandformBasin, "盆地", LandformPool, PrimaryTemplates);
            ConfigureGeoRegionNameGenerator(LandmassIsland, "岛", LandmassPool, LandmassTemplates);
            ConfigureGeoRegionNameGenerator(LandmassMainland, "大陆", LandmassPool, LandmassTemplates);
            ConfigureGeoRegionNameGenerator(Peninsula, "半岛", MorphologyPool, MorphologyTemplates);
            ConfigureGeoRegionNameGenerator(Strait, "海峡", MorphologyPool, PrimaryTemplates);
            ConfigureGeoRegionNameGenerator(Archipelago, "群岛", MorphologyPool, MorphologyTemplates);
        }

        private static void ConfigureGeoRegionNameGenerator(NameGeneratorAsset generator, string type, string pool, string templates)
        {
            if (generator == null)
            {
                throw new InvalidOperationException("GeoRegion 命名器资产尚未初始化");
            }

            string[] poolParts = pool.Split('|');
            if (poolParts.Length != 3)
            {
                throw new InvalidOperationException($"GeoRegion 命名器词池格式错误: generator={generator.id}");
            }

            generator.use_dictionary = true;
            generator.addDictPart("prefix", poolParts[0]);
            generator.addDictPart("core", poolParts[1]);
            generator.addDictPart("suffix", poolParts[2]);
            generator.addDictPart("type", type);
            generator.addDictPart("biome", GeoRegionBiomePool);

            foreach (string template in templates.Split('|'))
            {
                generator.addTemplate(template);
            }
        }
    }
}
