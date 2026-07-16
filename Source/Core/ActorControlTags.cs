namespace Cultiway.Core;

/// <summary>由角色最终属性携带、供所有动作来源共同识别的控制标签。</summary>
public static class ActorControlTags
{
    /// <summary>禁止原版法术、技能容器和其他统一主动能力。</summary>
    public const string Silenced = "cultiway_silenced";

    /// <summary>阻止没有侦破能力的敌方单位获取该角色为攻击目标。</summary>
    public const string Concealed = "cultiway_concealed";
}
