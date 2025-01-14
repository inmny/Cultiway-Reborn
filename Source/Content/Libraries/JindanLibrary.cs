using System.Collections.Generic;
using System.Linq;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.UI.Prefab;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;

namespace Cultiway.Content.Libraries;

public class JindanLibrary : AssetLibrary<JindanAsset>
{
    public override void init()
    {
        SpecialItemTooltip.RegisterSetupAction(((tooltip, type, entity) =>
        {
            if (!entity.HasComponent<Jindan>()) return;
            tooltip.Tooltip.addDescription($"\n{entity.GetComponent<Jindan>().Type.GetName()}");
        }));
    }
    [Hotfixable]
    public JindanAsset GetJindan(ActorExtend ae, ref XianBase xian_base)
    {
        foreach (JindanGroupAsset group in Manager.JindanGroupLibrary.jindanGroups)
        {
            using var jindan_list = new ListPool<JindanAsset>();
            using var score_list = new ListPool<float>();
            var last_score = 0f;
            if (group.jindans.Count > 0 && (group.check?.Invoke(ae, ref xian_base) ?? false))
            {
                foreach (JindanAsset jindan in group.jindans)
                {
                    if (jindan.check?.Invoke(ae, ref xian_base) ?? true)
                    {
                        jindan_list.Add(jindan);
                        var score = jindan.score?.Invoke(ae, ref xian_base) ?? 1;
                        score_list.Add(last_score + score);
                        last_score += score;
                    }
                }

                return ((IList<JindanAsset>)jindan_list)[RdUtils.RandomIndexWithAccumWeight(score_list)];
            }
        }

        return Jindans.Common;
    }
}