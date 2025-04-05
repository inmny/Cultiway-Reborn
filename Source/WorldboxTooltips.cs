using Cultiway.Abstract;
using Cultiway.UI.Prefab;
using Friflo.Engine.ECS;
using NeoModLoader.General;

namespace Cultiway;

public partial class WorldboxGame
{
    public class Tooltips : ExtendLibrary<TooltipAsset, Tooltips>
    {
        [GetOnly("tip")] public static TooltipAsset Tip { get; private set; }
        public static TooltipAsset RawTip { get; private set; }

        public static TooltipAsset SpecialItem { get; private set; }

        protected override void OnInit()
        {
            RegisterAssets();
            SpecialItem.prefab_id = "tooltips/tooltip_cultiway_special_item";
            SpecialItem.callback = ShowSpecialItem;
            SpecialItemTooltip.PatchTo<Tooltip>(SpecialItem.prefab_id);
            
            RawTip.callback = ShowRawTip;
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