using Friflo.Engine.ECS;

namespace Cultiway.Core.Components;

/// <summary>
/// 师门风格枚举 - 影响AI行为
/// </summary>
public enum MasterStyle
{
    Strict,      // 严厉型 - 高要求，高成长
    Gentle,      // 温和型 - 亲密度增长快
    Laissez,     // 放任型 - 自由发展
    Demanding,   // 苛求型 - 要求弟子贡献
    Protective   // 保护型 - 为弟子出头
}

/// <summary>
/// 师傅状态组件 - 存储在师傅Entity上
/// </summary>
public struct MasterApprenticeState : IComponent
{
    /// <summary>
    /// 当前弟子数量
    /// </summary>
    public int ApprenticeCount;
    
    /// <summary>
    /// 最大弟子数量
    /// </summary>
    public int MaxApprenticeCount;
    
    /// <summary>
    /// 衣钵传人ID（如果已指定）
    /// </summary>
    public long SuccessorActorId;
    
    /// <summary>
    /// 收徒意愿 (0-100，影响AI主动收徒概率)
    /// </summary>
    public float RecruitWillingness;
    
    /// <summary>
    /// 传授意愿 (0-100，影响AI主动传授概率)
    /// </summary>
    public float TeachWillingness;
    
    /// <summary>
    /// 师门风格
    /// </summary>
    public MasterStyle Style;
}

