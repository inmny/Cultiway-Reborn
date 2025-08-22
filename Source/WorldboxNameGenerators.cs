using Cultiway.Abstract;
using HarmonyLib;

namespace Cultiway;

public partial class WorldboxGame
{
    public class NameGenerators : ExtendLibrary<NameGeneratorAsset, NameGenerators>
    {
        public static NameGeneratorAsset Cultibook { get; private set; }
        public static NameGeneratorAsset Sect { get; private set; }
        protected override void OnInit()
        {
            RegisterAssets();
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
        }
    }
}