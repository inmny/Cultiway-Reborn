using System;
using System.IO;
using System.Text;
using Cultiway.Content.Libraries;
using Cultiway.Core.AIGCLib;
using Cultiway.Utils;
using HarmonyLib;
using NeoModLoader.api.attributes;
using Newtonsoft.Json;
using UnityEngine;

namespace Cultiway.Content.AIGC;

public class ElixirEffectJsonGenerator : PromptNameGenerator<ElixirEffectJsonGenerator>
{
    protected override string NameDictPath { get; } =
        Path.Combine(Application.persistentDataPath, "Cultiway_ElixirEffectJsonDict.json");
    [Hotfixable]
    protected override string GetSystemPrompt()
    {
        return "用户将会提供一组材料名字，你需要仔细分析它们的效果，并生成其制成的丹药药效。要求只输出json格式的结果，以纯文本格式输出，不要以markdown形式输出，并且与以下示例一致(注意使用英文引号)，不能新增字段，并且bonus_stats需要贴合effect_description，effect_description需要贴合丹药材料: {”effect_type”: ”StatusGain”,”effect_description”: ”此丹药由幻梦灵藤与幻影灵草带来虚幻力量，结合烈火灵草的炽热之力，能够使人在战斗中如幻影般难以捉摸，同时速度与攻击速度得到显著提升”,”bonus_stats”: {”speed”: 10,”attack_speed”: 20,”multiplier_attack_speed”: 0.5,”multiplier_speed”:0.2} } bonus_stats可选的key有:" + BaseStatses.AllStatsIds;
    }

    protected override string PostProcess(string name)
    {
        return name.Replace("`", "");
    }

    protected override bool IsValid(string name)
    {
        try
        {
            JsonConvert.DeserializeObject<ElixirEffectGenerator.ElixirEffect>(name);
        }
        catch(Exception e)
        {
            if (Config.isEditor)
            {
                ModClass.LogErrorConcurrent(SystemUtils.GetFullExceptionMessage(e));
                ModClass.LogWarningConcurrent("Failed to parse elixir effect json: " + name);
            }
            return false;
        }

        return true;
    }

    protected override string GetDefaultName(string[] param)
    {
        return string.Empty;
    }

    protected override float Temperature { get; } = 1;

    [Hotfixable]
    protected override string GetPrompt(string[] param)
    {
        StringBuilder sb = new();
        for (int i=0;i<param.Length;i++)
        {
            sb.Append('”');
            sb.Append(param[i]);
            sb.Append('”');
            if (i < param.Length - 1)
            {
                sb.Append(',');
            }
        }
        return sb.ToString();
    }
}