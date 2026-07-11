using Cultiway.Abstract;

namespace Cultiway;

public partial class WorldboxGame
{
    public class GodPowers : ExtendLibrary<GodPower, GodPowers>
    {
        public static GodPower WanfaGrant { get; private set; }

        protected override bool AutoRegisterAssets() => true;

        protected override void OnInit()
        {
            WanfaGrant.name = "wanfa_grant";
            WanfaGrant.force_map_mode = MetaType.None;
            WanfaGrant.force_brush = string.Empty;
            WanfaGrant.ignore_fast_spawn = true;
            WanfaGrant.hold_action = true;
            WanfaGrant.show_tool_sizes = true;
            WanfaGrant.unselect_when_window = true;
            WanfaGrant.click_interval = 0.12f;
            WanfaGrant.can_drag_map = false;
            WanfaGrant.requires_premium = false;
            WanfaGrant.rank = PowerRank.Rank0_free;
        }
    }
}
