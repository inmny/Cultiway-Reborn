using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cultiway.Abstract;
using strings;

namespace Cultiway.Content
{
    public class CultureTraits : ExtendLibrary<CultureTrait, CultureTraits>
    {
        public static CultureTrait CultureSkin { get; private set; }
        protected override bool AutoRegisterAssets() => true;
        protected override void OnInit()
        {
            CultureSkin.group_id = S_TraitGroup.miscellaneous;
            CultureSkin.path_icon = "cultiway/icons/traits/iconCultureSkin";
        }
    }
}