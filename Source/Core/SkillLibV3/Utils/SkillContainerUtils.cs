using Cultiway.Core.SkillLibV3.Components;
using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3.Utils;

public static class SkillContainerUtils
{
    /// <summary>
    /// 判断两个法术容器是否相似, 要求必须有<see cref="SkillContainer"/>组件
    /// </summary>
    public static bool IsSimilar(Entity a, Entity b)
    {
        var a_skill_container = a.GetComponent<SkillContainer>();
        var b_skill_container = b.GetComponent<SkillContainer>();
        if (a_skill_container.Asset != b_skill_container.Asset)
        {
            return false;
        }

        if (a_skill_container.OnEffectObj != b_skill_container.OnEffectObj)
        {
            return false;
        }

        if (a_skill_container.OnTravel != b_skill_container.OnTravel)
        {
            return false;
        }

        if (a_skill_container.OnSetup != b_skill_container.OnSetup)
        {
            return false;
        }

        if (a.HasName && b.HasName)
        {
            return a.Name.value == b.Name.value;
        }
        return true;
    }
}