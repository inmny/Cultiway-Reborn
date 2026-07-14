using Cultiway.Abstract;

namespace Cultiway;

public partial class WorldboxGame
{
    public class GodPowers : ExtendLibrary<GodPower, GodPowers>
    {
        public static GodPower WanfaGrant { get; private set; }
        public static GodPower UpgradeRain { get; private set; }
        public static GodPower ElementRootRain { get; private set; }

        protected override bool AutoRegisterAssets() => true;

        protected override void OnInit()
        {
            SetupRainPower(WanfaGrant, "wanfa_grant");
            SetupRainPower(UpgradeRain, "upgrade_rain");
            SetupRainPower(ElementRootRain, "element_root_rain");
        }

        /// <summary>配置一个支持笔刷连续投放、且不强制地图模式的免费雨类 power。</summary>
        private static void SetupRainPower(GodPower power, string localeName)
        {
            power.name = localeName;
            power.force_map_mode = MetaType.None;
            power.force_brush = string.Empty;
            power.ignore_fast_spawn = true;
            power.hold_action = true;
            power.show_tool_sizes = true;
            power.unselect_when_window = true;
            power.click_interval = 0.12f;
            power.can_drag_map = false;
            power.requires_premium = false;
            power.rank = PowerRank.Rank0_free;
        }
    }
}
