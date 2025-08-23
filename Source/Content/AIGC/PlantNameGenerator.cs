using System.IO;
using System.Text;
using Cultiway.Core.AIGCLib;
using HarmonyLib;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content.AIGC;

public class PlantNameGenerator : PromptNameGenerator<PlantNameGenerator>
{
    protected override string NameDictPath { get; } = Path.Combine(Application.persistentDataPath, "Cultiway_PlantNameDict.json");
    [Hotfixable]
    protected override string GetSystemPrompt()
    {
        return "你需要为用户给出的灵植命名，不要有任何符号，不要给出思考过程，仅给出一个答案。\nInput example:\n为具有雷灵根的灵植命名\nOutput example:\n惊雷草";
    }

    protected override string GetDefaultName(string[] param)
    {
        return param.Last()
            .Replace("灵根", "灵草")
            .Replace("金丹", "灵草");
    }

    protected override bool IsValid(string name)
    {
        return name.Length < 10;
    }
    [Hotfixable]
    protected override string GetPrompt(string[] param)
    {
        StringBuilder sb = new();
        sb.Append("为具有");
        for (int i = 0; i < param.Length; i += 2)
        {
            sb.Append($"“{param[i]}”");
            if (i + 1 < param.Length)
            {
                sb.Append($"和“{param[i + 1]}”");
            }
        }
        sb.Append("的灵植命名");
        return sb.ToString();
    }
}