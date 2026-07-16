using Cultiway.Core;

namespace Cultiway.Content;

/// <summary>
/// 骑士 9 级毕业钩子：修到 9 级（成为血脉始祖）时的扩展点。
/// 当前为空操作占位；未来用于"9 级后转入其他体系"（保留 vs 让位、目标体系待那些体系动工再定）。
/// </summary>
public static class KnightGraduation
{
    public static void OnGraduated(ActorExtend ae)
    {
        // TODO: 未来实现"9 级后转入其他体系"。当前为占位空操作。
    }
}
