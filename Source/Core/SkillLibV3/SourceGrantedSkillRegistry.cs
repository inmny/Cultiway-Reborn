using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3;

/// <summary>
/// 通过一个世界级主实体持有来源授予技能，避免只读模板被未掌握技能回收系统删除。
/// </summary>
public static class SourceGrantedSkillRegistry
{
    private static Entity root;

    /// <summary>让世界级根实体 master 指定技能容器；重复调用不会创建重复关系。</summary>
    public static void Master(Entity skillContainer)
    {
        if (skillContainer.IsNull || !skillContainer.HasComponent<Components.SkillContainer>()) return;
        EnsureRoot();
        root.AddRelation(new SkillMasterRelation { SkillContainer = skillContainer });
    }

    /// <summary>创建当前世界唯一的来源授予技能根实体。</summary>
    private static void EnsureRoot()
    {
        if (!root.IsNull) return;
        root = ModClass.I.W.CreateEntity(new SourceGrantedSkillRoot());
    }
}
