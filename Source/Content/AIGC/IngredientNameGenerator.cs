using System.IO;
using System.Text;
using Cultiway.Core.AIGCLib;
using HarmonyLib;
using UnityEngine;

namespace Cultiway.Content.AIGC;

public class IngredientNameGenerator : PromptNameGenerator<IngredientNameGenerator>
{
    protected override string NameDictPath { get; } =
        Path.Combine(Application.persistentDataPath, "Cultiway_IngredientNameDict.json");
    protected override string GetDefaultName(string[] param)
    {
        return param.Last();
    }

    protected override string GetPrompt(string[] param)
    {
        StringBuilder sb = new();

        sb.Append("为拥有");
        bool first = true;
        foreach (var p in param)
        {
            if (first)
            {
                first = false;
            }
            else
            {
                sb.Append("，");
            }
            sb.Append('“');
            sb.Append(p);
            sb.Append('”');
        }
        sb.Append("的修士或妖兽掉落的材料命名，仅给出一个答案(比如炎冰魄)，不要有任何符号");


        return sb.ToString();
    }
}