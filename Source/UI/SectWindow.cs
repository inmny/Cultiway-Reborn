using Cultiway.Const;
using Cultiway.Core;
using Cultiway.Utils.Extension;

namespace Cultiway.UI;

public class SectWindow : WindowMetaGeneric<Sect, SectData>
{
    public override MetaType meta_type => MetaTypeExtend.Sect.Back();
    public override Sect meta_object => WorldboxGame.I.SelectedSect;
}