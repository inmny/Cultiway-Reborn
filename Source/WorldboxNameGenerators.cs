using Cultiway.Abstract;
using HarmonyLib;

namespace Cultiway;

public partial class WorldboxGame
{
    public class NameGenerators : ExtendLibrary<NameGeneratorAsset, NameGenerators>
    {
        public static NameGeneratorAsset Cultibook { get; private set; }
        public static NameGeneratorAsset Sect { get; private set; }
        public static NameGeneratorAsset GeoRegion { get; private set; }
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
            Sect.addDictPart("postfix", "宗门教派宫殿楼阁轩府寺观山谷洞庄盟会堂帮".Join(c=>c.ToString(), ","));
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
        }
    }
}