using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Cultiway.Content.Names;

/// <summary>
/// 某个东人服饰风格的名称词库。
/// 字段名与 <c>Content/Names/EasternHuman/&lt;风格&gt;.json</c> 的键一一对应，缺失的词组反序列化后为 null。
/// </summary>
internal sealed class NameStyleData
{
    /// <summary>风格 key，与 <see cref="Const.EasternHumanSkinStyles"/> 的 key 对应（如 "1"、tang、fusang）。</summary>
    public string style;

    /// <summary>人名词组（姓氏与男女名）。</summary>
    public NameWordGroup unit;

    /// <summary>地名词组（前缀 + 后缀）。</summary>
    public NameWordGroup city;

    /// <summary>国名词组（国号 + 可选后缀）。</summary>
    public NameWordGroup kingdom;

    /// <summary>氏族词组（后缀，姓氏复用 unit.surnames）。</summary>
    public NameWordGroup clan;

    /// <summary>家族词组（后缀，姓氏复用 unit.surnames）。</summary>
    public NameWordGroup family;

    /// <summary>文化名词组（词干，拼接固定后缀“文化”）。</summary>
    public NameWordGroup culture;

    /// <summary>语言名词组（词干，拼接固定后缀“语”）。</summary>
    public NameWordGroup language;

    /// <summary>宗教词组（名 + 后缀）。</summary>
    public NameWordGroup religion;

    /// <summary>亚种词组（名 + 后缀），用于“XX人/XX族”式的亚种名。</summary>
    public NameWordGroup subspecies;
}

/// <summary>词库中的一组词表；不同名称类型只使用其中相关的几张表，其余为 null。</summary>
internal sealed class NameWordGroup
{
    /// <summary>完整的名字列表（文化/语言/王国/宗教名等）。</summary>
    public List<string> names;

    /// <summary>姓氏列表（人名/氏族/家族共用，可含复姓）。</summary>
    public List<string> surnames;

    /// <summary>男性名列表。</summary>
    public List<string> given_male;

    /// <summary>女性名列表。</summary>
    public List<string> given_female;

    /// <summary>地名前缀列表。</summary>
    public List<string> prefix;

    /// <summary>单词后缀列表（地名等）。</summary>
    public List<string> suffix;

    /// <summary>多选一后缀列表（国名/宗教/氏族/家族等）。</summary>
    public List<string> suffixes;
}

/// <summary>
/// 东人按服饰风格的名称词库加载器。
/// 在内容初始化时读取 <c>Content/Names/EasternHuman/</c> 下全部 JSON 到内存，
/// 供 <see cref="EasternHumanNameComposer"/> 按“文化的种族是东人 + 当前服饰风格”查询。
/// </summary>
internal static class EasternHumanNameLibrary
{
    /// <summary>词库目录相对模组根目录的路径。</summary>
    private const string RelativeDirectory = "Content/Names/EasternHuman";

    /// <summary>已加载的全部词库，key 为风格 key。</summary>
    private static readonly Dictionary<string, NameStyleData> Styles = new(StringComparer.Ordinal);

    /// <summary>
    /// 读取词库目录下全部 JSON 文件；单个文件解析失败只记日志并跳过，不影响其他风格。
    /// </summary>
    internal static void Load()
    {
        Styles.Clear();
        string directory = Path.Combine(ModClass.I.GetDeclaration().FolderPath, RelativeDirectory);
        if (!Directory.Exists(directory))
        {
            ModClass.LogWarning($"({nameof(EasternHumanNameLibrary)}) 词库目录不存在: {directory}");
            return;
        }

        foreach (string file in Directory.GetFiles(directory, "*.json"))
        {
            try
            {
                NameStyleData data = JsonConvert.DeserializeObject<NameStyleData>(File.ReadAllText(file));
                if (string.IsNullOrEmpty(data?.style))
                {
                    ModClass.LogWarning($"({nameof(EasternHumanNameLibrary)}) 词库缺少 style 字段，跳过: {file}");
                    continue;
                }

                Styles[data.style] = data;
            }
            catch (Exception e)
            {
                ModClass.LogWarning($"({nameof(EasternHumanNameLibrary)}) 词库解析失败，跳过: {file}\n{e.Message}");
            }
        }

        ModClass.LogInfo($"({nameof(EasternHumanNameLibrary)}) 已加载 {Styles.Count} 个风格的名称词库");
    }

    /// <summary>按风格 key 查询词库；未加载或缺失时返回 false，由调用方回退原版命名。</summary>
    internal static bool TryGet(string styleKey, out NameStyleData data)
    {
        if (string.IsNullOrEmpty(styleKey))
        {
            data = null;
            return false;
        }

        return Styles.TryGetValue(styleKey, out data);
    }
}
