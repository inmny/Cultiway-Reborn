using System.Reflection;
using Cultiway.Abstract;
using Cultiway.Content;
using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>
/// 修炼方式集合（继承ExtendLibrary）
/// </summary>
public class CultivateMethods : ExtendLibrary<CultivateMethodAsset, CultivateMethods>
{
    /// <summary>
    /// 标准闭关修炼方式
    /// </summary>
    public static CultivateMethodAsset Standard { get; private set; }

    protected override bool AutoRegisterAssets() => true;

    protected override void OnInit()
    {
        Standard.TriggerType = CultivateTriggerType.Active;
        Standard.CanCultivate = ae => ae.HasCultisys<Xian>();
        Standard.GetEfficiency = ae => ae.HasElementRoot() ? ae.GetElementRoot().GetStrength() : 1f;
        Standard.GetBehaviourJobId = ae => {
            if (ae.Base.hasHouse())
            {
                return ActorJobs.XianCultivator.id;
            }
            else
            {
                return ActorJobs.PlantXianCultivator.id;
            }
        };
    }
}

