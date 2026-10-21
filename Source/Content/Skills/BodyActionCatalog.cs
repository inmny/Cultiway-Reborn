using System.IO;
using Cultiway.Core.Skills;
using Newtonsoft.Json;

namespace Cultiway.Content.Skills;

/// <summary>首批动作只提供可改造的完整资料，执行共用构造系统。</summary>
public static class BodyActionCatalog
{
    /// <summary>保留旧掌击的正式编号，供原有能力入口接入。</summary>
    public const string PalmId = "Cultiway.PalmStrike";
    /// <summary>已经读取的三个基础动作资料。</summary>
    private static AbilityDefinition[] all;

    /// <summary>读取制作资料，不在每次出手时解释文件。</summary>
    internal static AbilityDefinition[] All => all ??= new[] { Load("palm-strike"), Load("sweeping-palm"), Load("sword-thrust") };
    /// <summary>原地直掌的完整资料。</summary>
    public static AbilityDefinition Palm => All[0];

    /// <summary>从内容目录读取一个已制作的动作。</summary>
    /// <param name="name">资料文件名。</param>
    private static AbilityDefinition Load(string name) => JsonConvert.DeserializeObject<AbilityDefinition>(File.ReadAllText(
        Path.Combine(ModClass.I.GetDeclaration().FolderPath, "Content/Skills/Body", name + ".json")));
}
