using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>同一动作时钟中已经发生的阶段；接触仅由实际作用确认。</summary>
public enum AbilityActionEventKind
{
    /// <summary>动作进入蓄势。</summary>
    Preparing,

    /// <summary>动作已经出手，不表示部位必然脱离连接。</summary>
    Released,

    /// <summary>实际作用窗口开始。</summary>
    Active,

    /// <summary>实际人物、世界或法术接触。</summary>
    Contact,

    /// <summary>作用窗口结束并进入收势。</summary>
    Recovering,

    /// <summary>本次动作自然结束。</summary>
    Completed,

    /// <summary>本次动作被中断。</summary>
    Interrupted,

    /// <summary>跨过制作资料中的命名标记，不据此虚构命中。</summary>
    Marker
}

/// <summary>实际接触的类别，不携带游戏对象。</summary>
public enum AbilityActionContactKind
{
    /// <summary>普通阶段或制作标记。</summary>
    None,

    /// <summary>人物接触。</summary>
    Actor,

    /// <summary>世界地块接触。</summary>
    World,

    /// <summary>法术之间的交锋。</summary>
    Ability
}

/// <summary>一次数值推进形成的动作事实，只有预测确认后才能交给外部。</summary>
public readonly struct AbilityActionEvent
{
    /// <summary>实际部位，负一表示借用身体。</summary>
    public readonly int Node;

    /// <summary>同一部位内明确重启动作的次数，普通初始动作为零。</summary>
    public readonly int Instance;

    /// <summary>已经发生的阶段或标记。</summary>
    public readonly AbilityActionEventKind Kind;

    /// <summary>本次制作动作，身体或无制作动作的部位为空。</summary>
    public readonly string Motion;

    /// <summary>制作标记名称，普通阶段为空。</summary>
    public readonly string Marker;

    /// <summary>相对于本次施放开始的实际世界秒数。</summary>
    public readonly float Age;

    /// <summary>本时刻的实际手点、部位或接触位置。</summary>
    public readonly Vector3 Point;

    /// <summary>明确的实际接触类别。</summary>
    public readonly AbilityActionContactKind ContactKind;

    /// <summary>实际人物编号，非人物接触为零。</summary>
    public readonly long TargetId;

    /// <summary>身体恢复或完成所对应的真实结果，普通阶段为空。</summary>
    public readonly BodyActionResult? BodyResult;


    /// <summary>复制数值事实，不保存游戏对象或原始可编辑资料。</summary>
    /// <param name="node">实际部位。</param><param name="instance">动作实例。</param>
    /// <param name="kind">实际阶段。</param><param name="motion">制作动作。</param>
    /// <param name="marker">命名标记。</param><param name="age">施放中的实际时间。</param>
    /// <param name="point">实际位置。</param>
    /// <param name="contactKind">接触类别。</param><param name="targetId">实际人物编号。</param>
    /// <param name="bodyResult">触发身体恢复的真实结果。</param>
    internal AbilityActionEvent(int node, int instance, AbilityActionEventKind kind, string motion,
        string marker, float age, Vector3 point,
        AbilityActionContactKind contactKind = AbilityActionContactKind.None, long targetId = 0,
        BodyActionResult? bodyResult = null)
    {
        Node = node;
        Instance = instance;
        Kind = kind;
        Motion = motion;
        Marker = marker;
        Age = age;
        Point = point;
        ContactKind = contactKind;
        TargetId = targetId;
        BodyResult = bodyResult;
    }
}
