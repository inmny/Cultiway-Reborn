using System.Collections.Generic;
using Cultiway.Content.Components;
using Cultiway.UI.Prefab;

namespace Cultiway.Content.Libraries;

/// <summary>保存有限、共享的金丹与元婴组合原子资产。</summary>
public sealed class CoreFormationAtomLibrary : AssetLibrary<CoreFormationAtomAsset>
{
    /// <summary>注册组合体系共用的特殊物品 Tooltip 显示逻辑。</summary>
    public override void init()
    {
        SpecialItemTooltip.RegisterSetupAction((tooltip, type, entity) =>
        {
            if (!entity.HasComponent<Jindan>()) return;
            tooltip.Tooltip.addDescription($"\n{entity.GetComponent<Jindan>().GetName()}");
        });
    }

    /// <summary>向组合器暴露当前已注册原子的只读枚举入口。</summary>
    internal IEnumerable<CoreFormationAtomAsset> All => list;
}
