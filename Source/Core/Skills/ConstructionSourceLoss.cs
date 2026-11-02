namespace Cultiway.Core.Skills;

/// <summary>部位当前控制者失效时的明确规则；缺省保留原资料的结束行为。</summary>
public enum ConstructionSourceLoss
{
    /// <summary>失去控制者便结束，不撤销此前已经确认的结果。</summary>
    End = 0,

    /// <summary>不再依赖来源连接的部分，按已付资源、剩余作用量和寿命继续。</summary>
    Continue = 1
}