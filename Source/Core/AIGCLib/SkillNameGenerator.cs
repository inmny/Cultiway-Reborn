using System.IO;
using System.Text;
using System.Threading.Tasks;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Modifiers;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.AIGCLib;

public class SkillNameGenerator : EntityNameGenerator<SkillNameGenerator>
{
    protected override string NameDictPath { get; } =
    Path.Combine(Application.persistentDataPath, "Cultiway_SkillNameDict.json");
    protected override string GetSystemPrompt()
    {
        return "为用户提供的法术生成名称，要求符合玄幻风格，并且必须简洁（2-5个字），优先简洁而非完整表达所有词条含义。用户会提供一个法术实体名字以及附加在这个法术上的一系列词条，仅给出一个答案，不要有任何符号。\nInput example:\n为产生\"雷\"的法术，请生成一个名称。这个法术有以下词条：连射:5;放大:2;冰冻:3\nOutput example:\n连霜雷";
    }

    protected override string GetDefaultName(string[] param)
    {
        return param[0] + "术";
    }

    protected override string GetPrompt(string[] param)
    {
        var sb = new StringBuilder();
        sb.Append("为产生“");
        sb.Append(param[0]);
        sb.Append("”的法术，请生成一个名称。这个法术有以下词条：");
        foreach (var modifier in param)
        {
            sb.Append(modifier);
            sb.Append(';');
        }

        sb.Remove(sb.Length - 1);
        return sb.ToString();
    }

    public void GenerateFor(Entity skill_container_entity)
    {
        var component_types = skill_container_entity.GetComponentTypes();

        var skill_container = skill_container_entity.GetComponent<SkillContainer>();
        var param_list = UnityEngine.Pool.ListPool<string>.Get();
        param_list.Add(skill_container.Asset.id.Localize());
        foreach (var component_type in component_types)
        {
            if (!typeof(IModifier).IsAssignableFrom(component_type))
            {
                continue;
            }

            var modifier = (IModifier)skill_container_entity.GetComponent(component_type);
            var key = modifier.GetKey();
            //var value = modifier.GetValue();
            
            param_list.Add(key);
        }

        NewNameGenerateRequest(param_list.ToArray(), skill_container_entity);
        UnityEngine.Pool.ListPool<string>.Release(param_list);
    }
}