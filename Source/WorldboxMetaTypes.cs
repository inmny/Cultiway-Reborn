using System.Collections.Generic;
using System.Collections.ObjectModel;
using Cultiway.Abstract;
using Cultiway.Core;
using Cultiway.UI;
using Cultiway.Utils.Extension;
using strings;

namespace Cultiway;

public partial class WorldboxGame
{
    public class MetaTypes : ExtendLibrary<MetaTypeAsset, MetaTypes>
    {
        public static MetaTypeAsset Sect { get; private set; }
        protected override void OnInit()
        {
            RegisterAssets();
            Sect.window_name = Sect.id;
            Sect.window_action_clear = () => I.SelectedSect = null;
            Sect.GetExtend<MetaTypeAssetExtend>().ExtendWindowHistoryActionUpdate = (data) =>
            {
                data.StoredObj[Sect.id] = I.SelectedSect;
            };
            Sect.GetExtend<MetaTypeAssetExtend>().ExtendWindowHistoryActionRestore = (data) =>
            {
                I.SelectedSect = data.StoredObj[Sect.id] as Sect;
            };
            Sect.get_list = () => I.Sects;
            Sect.custom_sorted_list = () =>
            {
                var list = new ListPool<NanoObject>(64);
                foreach (var sect in I.Sects)
                {
                    if (sect.isFavorite())
                        list.Add(sect);
                }

                return list;
            };
            Sect.has_any = () => I.Sects.hasAny();
            Sect.get_selected = () => I.SelectedSect;
            Sect.set_selected = (sect) => I.SelectedSect = sect as Sect;
            Sect.get = (id) => I.Sects.get(id);
            Sect.stat_hover = (id, field) =>
            {
                var sect = I.Sects.get(id);
                if (sect.isRekt()) return;
                Tooltip.show(field, Tooltips.Sect.id, new TooltipData()
                {
                    tip_description = id.ToString()
                });
            };
            Sect.stat_click = (id, _) =>
            {
                var sect = I.Sects.get(id);
                if (sect.isRekt()) return;
                I.SelectedSect = sect;
                //ScrollWindow.showWindow();
            };
        }
    }
}