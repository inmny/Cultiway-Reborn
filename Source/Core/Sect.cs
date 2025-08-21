using Cultiway.Const;
using Cultiway.Utils.Extension;

namespace Cultiway.Core;

public class Sect : MetaObject<SectData>
{
    public override MetaType meta_type => MetaTypeExtend.Sect.Back();

    public void Setup(Actor founder)
    {
        generateNewMetaObject();
        data.FounderActorName = founder.getName();
        data.FounderActorID = founder.data.id;
        // TODO: 调整命名
        data.name = founder.generateName(MetaTypeExtend.Family.Back(), getID());
    }

    public override void generateBanner()
    {
    }

    public override ColorLibrary getColorLibrary()
    {
        // TODO: 添加颜色库
        return AssetManager.families_colors_library;
    }
}    