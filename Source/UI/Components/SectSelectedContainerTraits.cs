using System.Collections.Generic;
using Cultiway.Const;
using Cultiway.Core;
using Cultiway.Core.Libraries;
using Cultiway.Utils.Extension;

namespace Cultiway.UI.Components;

public class SectSelectedContainerTraits : SelectedContainerTraits<SectTrait, SectTraitButton, SectTraitsContainer, SectTraitsEditor>
{
    public override MetaType meta_type => MetaTypeExtend.Sect.Back();

    public override IReadOnlyCollection<SectTrait> getTraits()
    {
        Sect sect = WorldboxGame.I.SelectedSect;
        return sect == null || sect.isRekt() ? System.Array.Empty<SectTrait>() : sect.getTraits();
    }

    public override bool canEditTraits()
    {
        return true;
    }
}
