using System.Collections.Generic;
using System.IO;
using System.Text;
using Cultiway.Core.AIGCLib;
using HarmonyLib;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content.AIGC;

public class PlantNameGenerator : PromptNameGenerator<PlantNameGenerator>
{
    internal const string TraitPrefix = "特质：";

    protected override string NameDictPath { get; } = Path.Combine(Application.persistentDataPath, "Cultiway_PlantNameDict.json");
    [Hotfixable]
    protected override string GetSystemPrompt()
    {
        return "你需要根据灵植的灵根、金丹、元婴等修行信息以及额外描述的特质为灵植命名，不要有任何符号，不要给出思考过程，仅给出一个答案。\\nInput example:\\n为灵根“雷灵根”、特质“庇护、寄生”的灵植命名\\nOutput example:\\n紫霄护魂藤";
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
        return name.Length < 10;
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
