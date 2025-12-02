using System;
using System.IO;
using System.Linq;
using System.Text;
using Cultiway.Content.Libraries;
using Cultiway.Core.AIGCLib;
using Cultiway.Utils;
using HarmonyLib;
using NeoModLoader.api.attributes;
using Newtonsoft.Json;
using UnityEngine;

namespace Cultiway.Content.AIGC;

public class ElixirDataGainJsonGenerator : PromptNameGenerator<ElixirDataGainJsonGenerator>
{
    private static readonly string[] AttributeCandidates = ["health", "intelligence", "lifespan"];
    private static string[] OperationCandidates =>
        ModClass.L.OperationLibrary?.list?.Select(x => $"{x.GetName()}({x.id})").ToArray();
    protected override string NameDictPath { get; } =
        Path.Combine(Application.persistentDataPath, "Cultiway_ElixirDataGainJsonDict.json");

    [Hotfixable]
    protected override string GetSystemPrompt()
    {
        var traitList = string.Join(", ", AssetManager.traits.list.Select(t => t.id));
        return
            "你是丹药DataGain效果生成器，先在attribute/trait/one_time三类中选一个填入chosen，再按类别补全字段，最后只输出JSON纯文本，不要Markdown和额外解释。"
            + "公共字段: effect_type固定为\"DataGain\"，effect_description为中文短句描述药效。"
            + $"attribute分支: attributes对象仅允许键 {string.Join(", ", AttributeCandidates)}，数值为正数，可附带max_stack(1-5)限制叠加次数。"
            + $"trait分支: traits数组元素必须取自以下trait id [{traitList}]，可选fallback_attribute对象（键同attributes，用于已有特质时补偿）。"
            + $"one_time分支: operations数组内容只能从 [{string.Join(", ", OperationCandidates)}] 里选，最多2个。"
            + "描述与材料契合，字段命名使用英文引号，避免新增未声明字段。";
    }

    protected override string PostProcess(string name)
    {
        return name.Replace("`", "");
    }

    protected override bool IsValid(string name)
    {
        try
        {
            JsonConvert.DeserializeObject<DataGainEffectDto>(name);
        }
        catch (Exception e)
        {
            if (Config.isEditor)
            {
                ModClass.LogErrorConcurrent(SystemUtils.GetFullExceptionMessage(e));
                ModClass.LogWarningConcurrent("Failed to parse data gain json: " + name);
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
        for (int i = 0; i < param.Length; i++)
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

    private class DataGainEffectDto
    {
        public string effect_type;
        public string chosen;
        public string effect_description;
        public object attributes;
        public object traits;
        public object operations;
    }
}
