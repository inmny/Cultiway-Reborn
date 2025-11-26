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
    
    /// <summary>
    /// 水中修炼方式
    /// </summary>
    public static CultivateMethodAsset WaterMeditation { get; private set; }
    
    /// <summary>
    /// 战斗修炼方式
    /// </summary>
    public static CultivateMethodAsset BattleCultivate { get; private set; }
    
    /// <summary>
    /// 杀戮吸收修炼方式（魔道）
    /// </summary>
    public static CultivateMethodAsset KillAbsorb { get; private set; }

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
        
        WaterMeditation.TriggerType = CultivateTriggerType.Active;
        WaterMeditation.CanCultivate = ae => ae.HasCultisys<Xian>();
        WaterMeditation.GetEfficiency = ae => {
            if (!ae.Base.current_tile.IsWater()) return 0.5f; // 不在水中效率减半
            if (!ae.HasElementRoot()) return 1.5f;
            // 水中效率提升，且与水系灵根强度成正比
            var elementRoot = ae.GetElementRoot();
            return 1.5f * (1f + elementRoot.Water);
        };
        WaterMeditation.GetBehaviourJobId = ae => ActorJobs.WaterCultivator.id;
        
        // 战斗修炼
        BattleCultivate.TriggerType = CultivateTriggerType.Passive;
        BattleCultivate.PassiveTriggerEvents.Add(PassiveTriggerEvents.OnAttack);
        BattleCultivate.PassiveTriggerEvents.Add(PassiveTriggerEvents.OnBeAttacked);
        BattleCultivate.CanCultivate = ae => ae.HasCultisys<Xian>();
        BattleCultivate.GetEfficiency = ae => {
            // 基础效率1.0，可以根据战斗状态调整
            // 暂时简化处理，后续可以根据战斗强度、敌人数量等因素调整
            return 1.0f;
        };
        BattleCultivate.OnSideEffect = (ae, wakanGained) => {
            // 负面效果暂时不用管
        };
        
        // 杀戮吸收（魔道）
        KillAbsorb.TriggerType = CultivateTriggerType.Passive;
        KillAbsorb.PassiveTriggerEvents.Add(PassiveTriggerEvents.OnKill);
        KillAbsorb.CanCultivate = ae => ae.HasCultisys<Xian>();
        KillAbsorb.GetEfficiency = ae => {
            // 基础效率1.0，可以根据杀人数调整
            // 暂时简化处理，后续可以根据累计杀人数调整
            return 1.0f;
        };
        KillAbsorb.OnSideEffect = (ae, wakanGained) => {
            // 负面效果暂时不用管
        };
    }
}

