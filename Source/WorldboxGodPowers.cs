using Cultiway.Abstract;

namespace Cultiway;

public partial class WorldboxGame
{
    public class GodPowers : ExtendLibrary<GodPower, GodPowers>
    {
        public static GodPower WanfaGrant { get; private set; }
        public static GodPower UpgradeRain { get; private set; }
        public static GodPower ElementRootRain { get; private set; }
        public static GodPower BaibaoArchive { get; private set; }
        public static GodPower BaibaoGrant { get; private set; }

        protected override bool AutoRegisterAssets() => true;

        protected override void OnInit()
        {
            SetupWorldToolPower(WanfaGrant, "wanfa_grant", true);
            SetupWorldToolPower(UpgradeRain, "upgrade_rain", true);
            SetupWorldToolPower(ElementRootRain, "element_root_rain", true);
            SetupWorldToolPower(BaibaoArchive, "baibao_archive", false);
            SetupWorldToolPower(BaibaoGrant, "baibao_grant", true);
        }

        /// <summary>配置一个不强制地图模式的免费世界工具。</summary>
        private static void SetupWorldToolPower(GodPower power, string localeName, bool allowBrush)
        {
            power.name = localeName;
            power.force_map_mode = MetaType.None;
            power.force_brush = string.Empty;
            power.ignore_fast_spawn = true;
            power.hold_action = allowBrush;
            power.show_tool_sizes = allowBrush;
            power.unselect_when_window = true;
            power.click_interval = 0.12f;
            power.can_drag_map = false;
            power.requires_premium = false;
            power.rank = PowerRank.Rank0_free;
        }
    }
}
