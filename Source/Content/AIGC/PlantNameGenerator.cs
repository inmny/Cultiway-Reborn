using System.Collections.Generic;
using System.IO;
using System.Security.Policy;
using System.Text;
using Cultiway.Core.AIGCLib;
using HarmonyLib;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content.AIGC;

public class PlantNameGenerator : ActorNameGenerator<PlantNameGenerator>
{
    internal const string TraitPrefix = "特质：";

    protected override string NameDictPath { get; } = Path.Combine(Application.persistentDataPath, "Cultiway_PlantNameDict.json");
    [Hotfixable]
    protected override string GetSystemPrompt()
    {
        return "你需要根据灵植的境界、灵根、金丹、元婴等修行信息以及额外描述的特质为灵植命名，不要有任何符号，不要给出思考过程，仅给出一个答案，要求符合玄幻风格、简短凝练，一般三到五个字，不要出现不符合玄幻风格的字词，灵植类型要多样化（草、藤、莲、木、菇、芝、树、花、果、参等等，也不局限于我给出这些），要求命名风格符合灵植境界，不要使用“五行”二字，仅在高阶灵植中出现。\\nInput example:\\n为境界“筑基”、灵根“雷灵根”、特质“庇护、寄生”的灵植命名\\nOutput example:\\n雷佑藤\\nInput example: 为境界“九转金丹”、灵根“冰灵根”、特质“净化、坚韧”的灵植命名\\nOutput example: 九转冰心莲\\nInput example: 为境界“无垢元婴”、灵根“火灵根”、特质“幻化、迅捷”的灵植命名\\nOutput example: 无垢幻风草\\nInput example: 为境界“化神”、灵根“毒灵根”、特质“腐蚀、诅咒”的灵植命名\\nOutput example: 蚀魂幽毒藤\\nInput example: 为境界“筑基”、灵根“风灵根”、特质“隐匿、迷幻”的灵植命名\\nOutput example: 风影叶\\nInput example: 为境界“炼虚”、灵根“光灵根”、特质“驱散、复苏”的灵植命名\\nOutput example: 曜生树。注意不要使用“五行”二字";
    }
    protected override string PostProcess(string name)
    {
        if (name.Length <= 3)
            return name;
        return name.Replace("五行", "").Replace("五灵", "").Replace("五色", "").Replace("五元", "");
    }

    protected override string GetDefaultName(string[] param)
    {
        string descriptor = string.Empty;
        for (int i = param.Length - 1; i >= 0; i--)
        {
            var entry = param[i];
            if (string.IsNullOrEmpty(entry) || entry.StartsWith(TraitPrefix)) continue;
            descriptor = entry;
            break;
        }

        if (string.IsNullOrEmpty(descriptor) && param.Length > 0)
        {
            descriptor = param[0];
        }

        return descriptor
            .Replace("灵根", "灵草")
            .Replace("金丹", "灵草")
            .Replace("元婴", "灵草");
    }

    protected override bool IsValid(string name)
    {
        return name.Length < 10 && name.Length >= 2;
    }
    [Hotfixable]
    protected override string GetPrompt(string[] param)
    {
        List<string> cultivation = new();
        List<string> traits = new();
        for (int i = 0; i < param.Length; i++)
        {
            var value = param[i];
            if (string.IsNullOrEmpty(value)) continue;
            if (value.StartsWith(TraitPrefix))
            {
                traits.Add(value.Substring(TraitPrefix.Length));
                continue;
            }
            cultivation.Add(value);
        }

        StringBuilder sb = new();
        sb.Append("为具有");
        for (int i = 0; i < cultivation.Count; i++)
        {
            if (i > 0) sb.Append('、');
            sb.Append('“');
            sb.Append(cultivation[i]);
            sb.Append('”');
        }
        if (traits.Count > 0)
        {
            sb.Append("，并带有特质“");
            for (int i = 0; i < traits.Count; i++)
            {
                if (i > 0) sb.Append('、');
                sb.Append(traits[i]);
            }
            sb.Append('”');
        }
        sb.Append("的灵植命名，名字需要兼顾灵根与特质的特点");
        return sb.ToString();
    }
}
