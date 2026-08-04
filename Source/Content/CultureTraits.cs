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
        public const string HallHearthId = "Cultiway.HallHearth";

        public static CultureTrait CultureSkin { get; private set; }
        public static CultureTrait HallHearth { get; private set; }
        protected override bool AutoRegisterAssets() => true;
        protected override void OnInit()
        {
            CultureSkin.group_id = S_TraitGroup.miscellaneous;
            CultureSkin.path_icon = "cultiway/icons/traits/iconCultureSkin";

            // 厅火之邑：鬼族 / 东方人族的城市布局标记 trait（自动 id Cultiway.HallHearth）。
            // 纯标记——不设 town_layout_plan 谓词（=> 不触发区过滤，城市密集填满），
            // 也不映射 tile 放置（=> 默认 Random 放置）。实际行为由 PatchCityBehBuild 的
            // transpiler 检测本 trait 后改写建筑聚集锚点（篝火→大厅）实现。
            // 与其他城市布局及「追寻独处」互斥，保证密集填满 + Random + 聚集开启。
            HallHearth.group_id = "town_plan";
            HallHearth.path_icon = "cultiway/icons/traits/iconHallHearth";
            foreach (var trait in AssetList)
            {
                if (trait != HallHearth && trait.group_id == "town_plan") AddOppositePair(HallHearth, trait);
            }
            AddOppositePair(HallHearth, Get("solitude_seekers"));
        }

        private static void AddOppositePair(CultureTrait first, CultureTrait second)
        {
            if (first == null || second == null) return;
            if (first.opposite_list == null || !first.opposite_list.Contains(second.id)) first.addOpposite(second.id);
            if (second.opposite_list == null || !second.opposite_list.Contains(first.id)) second.addOpposite(first.id);
            first.opposite_traits?.Add(second);
            second.opposite_traits?.Add(first);
        }
    }
}
