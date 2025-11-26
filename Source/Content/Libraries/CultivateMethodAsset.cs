using System;
using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Components;
using UnityEngine;

namespace Cultiway.Content.Libraries;

/// <summary>
/// 修炼触发类型
/// </summary>
public enum CultivateTriggerType
{
    Active,     // 主动修炼（需要专门的修炼行为）
    Passive,    // 被动修炼（在特定事件时触发）
    Continuous  // 持续修炼（如国运修炼，不影响其他行为）
}

/// <summary>
/// 修炼方式 Asset
/// </summary>
public class CultivateMethodAsset : Asset
{
    // ========== 核心委托 ==========
    
    /// <summary>
    /// 检查是否可以使用此修炼方式
    /// </summary>
    /// <param name="actor">修炼者</param>
    /// <returns>是否满足修炼条件</returns>
    public Func<ActorExtend, bool> CanCultivate;
    
    /// <summary>
    /// 计算修炼效率系数
    /// </summary>
    /// <param name="actor">修炼者</param>
    /// <returns>效率系数（1.0为标准）</returns>
    public Func<ActorExtend, float> GetEfficiency;
    
    /// <summary>
    /// 修炼副作用（如魔道的杀业积累）
    /// </summary>
    /// <param name="ae">修炼者扩展</param>
    /// <param name="wakanGained">本次获得的灵力</param>
    public Action<ActorExtend, float> OnSideEffect;
    
    // ========== AI行为相关 ==========
    
    /// <summary>
    /// 对应的行为任务ID（用于替换标准修炼行为）
    /// </summary>
    public Func<ActorExtend, string> GetBehaviourJobId;
    
    // ========== 触发条件 ==========
    
    /// <summary>
    /// 修炼触发类型
    /// </summary>
    public CultivateTriggerType TriggerType = CultivateTriggerType.Active;
    
    /// <summary>
    /// 被动触发时的事件类型（如战斗修炼在攻击时触发）
    /// </summary>
    public string PassiveTriggerEvent;
}

