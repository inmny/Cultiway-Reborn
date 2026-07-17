namespace Cultiway.Core.SkillLibV3.Editor;

/// <summary>
/// 技能编辑器内部的兼容性判定键。它们只描述控件组合约束，不参与玩法语义查询。
/// </summary>
public static class SkillEditorCompatibilityKeys
{
    public const string Instant = "editor.motion.instant";
    public const string Static = "editor.motion.static";
    public const string Travel = "editor.motion.travel";
    public const string Speed = "editor.modifier.speed";
    public const string OnTravel = "editor.modifier.on_travel";
}
