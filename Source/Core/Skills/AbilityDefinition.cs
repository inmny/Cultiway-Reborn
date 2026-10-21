using System;

namespace Cultiway.Core.Skills;

/// <summary>可保存和改造的能力构造；不包含网格、人物对象和本次施放进度。</summary>
public sealed class AbilityDefinition
{
    /// <summary>资料格式版本，与模组版本分别维护。</summary>
    public int Version = 1;
    /// <summary>跨存档保持的能力编号。</summary>
    public string Id;
    /// <summary>改造产生的新修订号。</summary>
    public int Revision = 1;
    /// <summary>显示名称。</summary>
    public string Name;
    /// <summary>创造者的长期人物编号，预制资料使用零。</summary>
    public long Creator;
    /// <summary>创造时使用的种子，载入不得重新生成结构。</summary>
    public int Seed;
    /// <summary>一次施放的最大世界秒数。</summary>
    public float Lifetime = 3f;
    /// <summary>由来源玩法解释并支付的资源量。</summary>
    public float Cost = 1f;
    /// <summary>由来源解释的实际资源编号；旧构造默认沿用真元。</summary>
    public string Resource = "wakan";
    /// <summary>独立施放之间的最短世界秒数。</summary>
    public float Cooldown = 2f;
    /// <summary>一次实际接触的基础作用量。</summary>
    public float Power = 1f;
    /// <summary>构造的根部。</summary>
    public ConstructionNode Root;
    /// <summary>借用实际身体的动作；可独立存在，也可与生成形体并存。</summary>
    public BodyActionDefinition Body;
}

/// <summary>一个可嵌套形体；子形体可以在父体挂点集合上分布。</summary>
public sealed class ConstructionNode
{
    /// <summary>本层不重复的部位编号。</summary>
    public string Id;
    /// <summary>共用形体资源编号。</summary>
    public string Form;
    /// <summary>显示表面覆盖，空值保留各部件的默认表面。</summary>
    public string Surface;
    /// <summary>实际材料编号，与仅用于显示的表面分别保存。</summary>
    public string Matter = "physical";
    /// <summary>父体挂点名；末尾星号表示选择具有此前缀的全部挂点。</summary>
    public string Anchor;
    /// <summary>相对父挂点的位置。</summary>
    public float[] Offset = new float[3];
    /// <summary>局部三个方向的角度。</summary>
    public float[] Rotation = new float[3];
    /// <summary>实际尺寸倍率。</summary>
    public float Scale = 1f;
    /// <summary>实际作用开始时点，按整次施放计算。</summary>
    public float ActiveFrom;
    /// <summary>实际作用结束时点，负值表示持续到施放结束。</summary>
    public float ActiveUntil = -1f;
    /// <summary>本形体采用的共用操作。</summary>
    public ConstructionOperation[] Operations = Array.Empty<ConstructionOperation>();
    /// <summary>附着在本体上的子结构。</summary>
    public ConstructionNode[] Children = Array.Empty<ConstructionNode>();
}

/// <summary>首批已经提供执行实现的操作；不是完整招式枚举。</summary>
public enum ConstructionOperationKind
{
    /// <summary>按世界时间连续长成。</summary>
    Grow,
    /// <summary>在局部坐标中连续旋转。</summary>
    Spin,
    /// <summary>解除父子连接并继承脱落点速度。</summary>
    Detach,
    /// <summary>独立运动，转向速度大于零时追踪本次目标。</summary>
    Fly
}

/// <summary>可保存的操作参数，编译时检查用途和时间。</summary>
public sealed class ConstructionOperation
{
    /// <summary>操作种类。</summary>
    public ConstructionOperationKind Kind;
    /// <summary>开始的世界秒数。</summary>
    public float At;
    /// <summary>生长历时；其他操作不使用。</summary>
    public float Seconds;
    /// <summary>旋转角速度或飞行局部速度。</summary>
    public float[] Vector = new float[3];
    /// <summary>飞行追踪时每秒允许转过的角度，零表示不追踪。</summary>
    public float TurnRate;
}
