using Friflo.Engine.ECS;

namespace Cultiway.Core.Components;

/// <summary>
/// 师徒关系类型枚举
/// </summary>
public enum MasterApprenticeType
{
    Nominal,   // 记名弟子
    Formal,    // 入室弟子
    Direct,    // 亲传弟子
    Successor  // 衣钵传人
}

/// <summary>
/// 师徒关系组件 - 存储在弟子Entity上，指向师傅
/// </summary>
public struct MasterApprenticeRelation : ILinkRelation
{
    /// <summary>
    /// 关系目标（师傅Entity）
    /// </summary>
    public Entity Master;
    
    /// <summary>
    /// 关系类型
    /// </summary>
    public MasterApprenticeType RelationType;
    
    /// <summary>
    /// 亲密度 (0-100)
    /// </summary>
    public float Intimacy;
    
    /// <summary>
    /// 拜师时间（世界时间戳）
    /// </summary>
    public float ApprenticeTime;
    
    /// <summary>
    /// 已传承的功法数量
    /// </summary>
    public int TransferredCultibookCount;
    
    /// <summary>
    /// 已传承的技能数量
    /// </summary>
    public int TransferredSkillCount;
    
    /// <summary>
    /// 是否为衣钵传人
    /// </summary>
    public bool IsSuccessor;
    
    public Entity GetRelationKey()
    {
        return Master;
    }
}

