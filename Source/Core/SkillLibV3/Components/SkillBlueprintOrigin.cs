using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3.Components;

/// <summary>
/// 记录当前运行时法术容器来自哪个万法阁蓝图版本，仅用于赐法版本冲突判断，不参与角色存读档。
/// </summary>
public struct SkillBlueprintOrigin : IComponent
{
    public string BlueprintId;
    public int Revision;
    public string Signature;
}
