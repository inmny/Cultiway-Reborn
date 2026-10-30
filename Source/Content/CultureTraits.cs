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
        /// <summary>厅火之邑文化特性的编号。</summary>
        public const string HallHearthId = "Cultiway.HallHearth";

        /// <summary>护城河文化特性的编号。</summary>
        public const string MoatId = "Cultiway.Moat";

        /// <summary>文化建筑风格特性。</summary>
        public static CultureTrait CultureSkin { get; private set; }

        /// <summary>厅火之邑城市布局特性。</summary>
        public static CultureTrait HallHearth { get; private set; }

        /// <summary>允许城市修建护城河的结构特性。</summary>
        public static CultureTrait Moat { get; private set; }

        /// <summary>允许自动注册本类声明的文化特性。</summary>
        protected override bool AutoRegisterAssets() => true;

        /// <summary>设置文化特性的分类、图标和互斥关系。</summary>
        protected override void OnInit()
        {
            CultureSkin.group_id = S_TraitGroup.miscellaneous;
            CultureSkin.path_icon = "cultiway/icons/traits/iconCultureSkin";

            // 原版“结构”栏目对应 architecture；护城河独立于城市布局，可以与任意布局同时选择。
            Moat.group_id = "architecture";
            Moat.path_icon = "ui/Icons/culture_traits/culture_trait_city_layout_rings";

            // 厅火之邑：鬼族 / 东方人族的城市布局标记 trait（自动 id Cultiway.HallHearth）。
            // 纯标记——不设 town_layout_plan 谓词（=> 不触发区过滤，城市密集填满），
            // 也不映射 tile 放置（=> 默认 Random 放置）。实际行为由 PatchCityBehBuild 的
            // transpiler 检测本 trait 后改写建筑聚集锚点（篝火→大厅）实现。
            // 与其他城市布局及「追寻独处」互斥，保证密集填满 + Random + 聚集开启。
            HallHearth.group_id = "town_plan";
            HallHearth.path_icon = "cultiway/icons/traits/iconHallHearth";
            foreach (var trait in AssetList)
            {
                if (trait != HallHearth && trait != Moat && trait.group_id == "town_plan")
                    AddOppositePair(HallHearth, trait);
            }
            AddOppositePair(HallHearth, Get("solitude_seekers"));
        }

        /// <summary>让两个文化特性互相排斥。</summary>
        /// <param name="first">第一个文化特性。</param>
        /// <param name="second">第二个文化特性。</param>
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
