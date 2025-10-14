using System.IO;
using System.Text;
using Cultiway.Core.AIGCLib;
using HarmonyLib;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content.AIGC;

public class IngredientNameGenerator : PromptNameGenerator<IngredientNameGenerator>
{
    protected override string NameDictPath { get; } =
        Path.Combine(Application.persistentDataPath, "Cultiway_IngredientNameDict.json");
    [Hotfixable]
    protected override string GetSystemPrompt()
    {
        return "你需要为用户给出的材料命名，并且要符合材料来源的特性，不要有任何符号，不要给出思考过程，仅给出一个答案。\\nInput example:\\n为拥有火灵根，金煌金丹的龙掉落的材料命名。\\nOutput example:\\n赤金龙鳞";
    }

    protected override string GetDefaultName(string[] param)
    {
        return param.Last();
    }

    protected override bool IsValid(string name)
    {
        return name.Length is > 1 and < 10;
    }

    [Hotfixable]
    protected override string GetPrompt(string[] param)
    {
        StringBuilder sb = new();

        sb.Append("为拥有");
        for (int i = 1; i < param.Length; i++)
        {
            if (i > 1)
            {
                sb.Append('，');
            }
            sb.Append('“');
            sb.Append(param[i]);
            sb.Append('”');
        }

        sb.Append("的");
        sb.Append(param[0]);
        sb.Append("掉落的材料命名");


        return sb.ToString();
    }
}