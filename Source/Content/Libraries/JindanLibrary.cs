using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.UI.Prefab;
using NeoModLoader.api.attributes;

namespace Cultiway.Content.Libraries;

public class JindanLibrary : AssetLibrary<JindanAsset>
{
    public override void init()
    {
        ActorExtend.RegisterActionOnUpdateStats((ae) =>
        {
            if (!ae.HasComponent<Jindan>()) return;
            var jindan = ae.GetComponent<Jindan>();
            if (string.IsNullOrEmpty(jindan.Type.wrapped_skill_id)) return;
            ae.tmp_all_skills.Add(jindan.Type.wrapped_skill_id);
        });
        SpecialItemTooltip.RegisterSetupAction(((tooltip, type, entity) =>
        {
            if (!entity.HasComponent<Jindan>()) return;
            tooltip.Tooltip.addDescription($"\n{entity.GetComponent<Jindan>().Type.GetName()}");
        }));
    }
    [Hotfixable]
    public JindanAsset GetJindan(ActorExtend ae, ref XianBase xian_base)
    {
        ModClass.LogInfo($"Start check for {ae}");
        foreach (JindanGroupAsset group in Manager.JindanGroupLibrary.jindanGroups)
        {
            ModClass.LogInfo($"Check group {group.id}");
            if (group.jindans.Count > 0 && (group.check?.Invoke(ae, ref xian_base) ?? false))
                return group.jindans.GetRandom();
        }

        return Jindans.Common;
    }
}