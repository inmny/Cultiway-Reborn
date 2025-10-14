using System.IO;
using System.Text;
using Cultiway.Core.AIGCLib;
using HarmonyLib;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content.AIGC;

public class ElixirNameGenerator : PromptNameGenerator<ElixirNameGenerator>
{
    protected override string NameDictPath { get; } =
        Path.Combine(Application.persistentDataPath, "Cultiway_ElixirNameDict.json");

    protected override string GetSystemPrompt()
    {
        return "为用户提供的丹药根据其药效和药材命名，仅给出一个答案(比如凝元丹)，不要有任何符号。\\nInput example:\\n药效:此丹药由幻梦灵藤与幻影灵草带来虚幻力量，结合烈火灵草的炽热之力，能够使人在战斗中如幻影般难以捉摸，同时速度与攻击速度得到显著提升，该丹药由幻影金丹炼制得到\\nOutput example:\\n幻速丹";
    }

    protected override string GetDefaultName(string[] param)
    {
        return string.Empty;
    }
    [Hotfixable]
    protected override string GetPrompt(string[] param)
    {
        StringBuilder sb = new();

        sb.Append("药效:");
        sb.Append(param[0]);
        sb.Append("，该丹药由");
        for (int i = 1; i < param.Length; i++)
        {
            sb.Append('“');
            sb.Append(param[i]);
            sb.Append('”');
            if (i < param.Length - 1)
            {
                sb.Append('，');
            }
        }

        sb.Append("炼制得到");
        
        return sb.ToString();
    }
}