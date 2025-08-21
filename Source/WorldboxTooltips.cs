using Cultiway.Abstract;
using Cultiway.Core;
using Cultiway.UI.Prefab;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using NeoModLoader.General;
using strings;

namespace Cultiway;

public partial class WorldboxGame
{
    public class Tooltips : ExtendLibrary<TooltipAsset, Tooltips>
    {
        [GetOnly("tip")] public static TooltipAsset Tip { get; private set; }
        [GetOnly(S_Tooltip.book)] public static TooltipAsset Book { get; private set; }
        public static TooltipAsset Sect { get; private set; }
        public static TooltipAsset RawTip { get; private set; }

        public static TooltipAsset SpecialItem { get; private set; }

        protected override void OnInit()
        {
            RegisterAssets();
            SpecialItem.prefab_id = "tooltips/tooltip_cultiway_special_item";
            SpecialItem.callback = ShowSpecialItem;
            SpecialItemTooltip.PatchTo<Tooltip>(SpecialItem.prefab_id);
            Sect.prefab_id = "tooltips/tooltip_cultiway_sect";
            Sect.callback = ShowSect;
            SectTooltip.PatchTo<Tooltip>(Sect.prefab_id);

            Book.callback += ShowCustomBookReadAction;
            
            RawTip.callback = ShowRawTip;
        }

        private void ShowSect(Tooltip tooltip, string type, TooltipData data)
        {
            var sect = I.Sects.get(long.Parse(data.tip_name));
            if (sect == null)
            {
                tooltip.setTitle("ERROR");
                return;
            }
            tooltip.setTitle(sect.name, "sect", sect.getColor().color_text);
            tooltip.addLineIntText("adults", sect.countAdults());
            tooltip.addLineIntText("children", sect.countChildren());
        }

        private void ShowCustomBookReadAction(Tooltip tooltip, string type, TooltipData data)
        {
            var book = data.book;
            var bte = book.getAsset().GetExtend<BookTypeAssetExtend>();
            if (bte.instance_read_action != null)
            {
                tooltip.addLineText($"Cultiway.Book.ReadAction.{bte.instance_read_action.Method.Name}", "");
            }
        }

        private void ShowRawTip(Tooltip tooltip, string type, TooltipData data)
        {
            tooltip.name.text = LM.Has(data.tip_name) ? LM.Get(data.tip_name) : data.tip_name;
            
            if (!string.IsNullOrEmpty(data.tip_description))
            {
                tooltip.setDescription(LM.Has(data.tip_description) ? LM.Get(data.tip_description) : data.tip_description);
            }

            if (!string.IsNullOrEmpty(data.tip_description_2))
            {
                tooltip.setBottomDescription(LM.Has(data.tip_description_2) ? LM.Get(data.tip_description_2) : data.tip_description_2);
            }
        }

        private static void ShowSpecialItem(Tooltip tooltip, string type, TooltipData data = default)
        {
            if (string.IsNullOrEmpty(data.tip_name)) return;
            Entity entity = ModClass.I.W.GetEntityById(int.Parse(data.tip_name));
            if (entity.IsNull) return;
            tooltip.GetComponent<SpecialItemTooltip>()?.Setup(type, entity);
        }
    }
}