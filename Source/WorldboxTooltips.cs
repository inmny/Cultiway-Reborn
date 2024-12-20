using Cultiway.Abstract;
using Cultiway.UI.Prefab;
using Friflo.Engine.ECS;

namespace Cultiway;

public partial class WorldboxGame
{
    public class Tooltips : ExtendLibrary<TooltipAsset, Tooltips>
    {
        [GetOnly("tip")] public static TooltipAsset Tip { get; private set; }

        public static TooltipAsset SpecialItem { get; private set; }

        protected override void OnInit()
        {
            RegisterAssets("Cultiway.Tooltip");
            SpecialItem.prefab_id = "tooltips/tooltip_cultiway_special_item";
            SpecialItem.callback = ShowSpecialItem;
            SpecialItemTooltip.PatchTo<Tooltip>(SpecialItem.prefab_id);
        }

        private static void ShowSpecialItem(Tooltip tooltip, string type, TooltipData data = default)
        {
            if (string.IsNullOrEmpty(data.tip_name)) return;
            Entity entity = ModClass.I.ActorExtendManager.World.GetEntityById(int.Parse(data.tip_name));
            if (entity.IsNull) return;
            tooltip.name.text = entity.Id.ToString();
        }
    }
}