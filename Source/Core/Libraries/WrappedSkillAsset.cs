using Cultiway.Content.Const;
using Friflo.Engine.ECS;

namespace Cultiway.Core.Libraries;

public delegate bool CostCheck(ActorExtend actor, out float strength);
public class WrappedSkillAsset : Asset
{
    public bool[] skill_type = [];
    public CostCheck cost_check;
    public float default_strength = 100;
    public bool HasSkillType(WrappedSkillType type)
    {
        if ((int) type >= skill_type.Length)
        {
            var new_skill_type = new bool[(int) type + 1];
            for (var i = 0; i < skill_type.Length; i++)
            {
                new_skill_type[i] = skill_type[i];
            }
            skill_type = new_skill_type;
        }
        return skill_type[(int) type];
    } 
    public void SetSkillType(WrappedSkillType type)
    {
        if ((int) type >= skill_type.Length)
        {
            var new_skill_type = new bool[(int) type + 1];
            for (var i = 0; i < skill_type.Length; i++)
            {
                new_skill_type[i] = skill_type[i];
            }
            skill_type = new_skill_type;
        }
        skill_type[(int) type] = true;
    }
}