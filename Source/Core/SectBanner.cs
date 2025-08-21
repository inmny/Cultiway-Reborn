using Cultiway.Const;
using Cultiway.Utils.Extension;

namespace Cultiway.Core;

public class SectBanner : BannerGeneric<Sect, SectData>
{
    public override MetaType meta_type => MetaTypeExtend.Sect.Back();
}