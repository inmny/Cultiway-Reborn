using System.Collections.Generic;
using Cultiway.Utils;

namespace Cultiway.Content.Names;

/// <summary>
/// 按词库组合名称。组合规则（与规划文档一致）：
/// 单位=姓+名（按性别）；城市=前缀+后缀；王国=国号+40%概率后缀；
/// 氏族/家族=姓+后缀；文化=词干+“文化”；语言=词干+“语”；宗教=名+后缀；亚种=名+后缀。
/// 任一所需词组缺失时返回 null，由补丁层回退原版命名。
/// </summary>
internal static class EasternHumanNameComposer
{
    /// <summary>名称长度上限，对齐原版 OnomasticsData.MAX_NAME_LENGTH。</summary>
    private const int MaxNameLength = 30;

    /// <summary>按名称类型组合名字；返回 null 表示该类型无法用词库生成。</summary>
    internal static string Compose(NameStyleData data, MetaType type, ActorSex sex)
    {
        string name = type switch
        {
            MetaType.Unit => ComposeUnit(data, sex),
            MetaType.City => Join(Pick(data.city?.prefix), Pick(data.city?.suffix)),
            MetaType.Kingdom => ComposeKingdom(data),
            MetaType.Clan => Join(Pick(data.unit?.surnames), Pick(data.clan?.suffixes)),
            MetaType.Family => Join(Pick(data.unit?.surnames), Pick(data.family?.suffixes)),
            MetaType.Culture => Join(Pick(data.culture?.names), "文化"),
            MetaType.Language => Join(Pick(data.language?.names), "语"),
            MetaType.Religion => Join(Pick(data.religion?.names), Pick(data.religion?.suffixes)),
            MetaType.Subspecies => Join(Pick(data.subspecies?.names), Pick(data.subspecies?.suffixes)),
            _ => null
        };

        if (string.IsNullOrEmpty(name)) return null;
        return name.Length <= MaxNameLength ? name : name[..MaxNameLength];
    }

    /// <summary>人名：姓 + 名，按性别取名表；性别未知按男性处理。</summary>
    private static string ComposeUnit(NameStyleData data, ActorSex sex)
    {
        List<string> givenPool = sex == ActorSex.Female ? data.unit?.given_female : data.unit?.given_male;
        return Join(Pick(data.unit?.surnames), Pick(givenPool));
    }

    /// <summary>国名：国号 + 约 40% 概率追加后缀（朝/国/帝国等）。</summary>
    private static string ComposeKingdom(NameStyleData data)
    {
        string name = Pick(data.kingdom?.names);
        if (name == null) return null;
        return Randy.randomInt(0, 5) < 2 ? Join(name, Pick(data.kingdom?.suffixes)) : name;
    }

    /// <summary>从词表中随机取一个词；词表为空或缺失返回 null。</summary>
    private static string Pick(List<string> pool)
    {
        return pool is { Count: > 0 } ? pool[Randy.randomInt(0, pool.Count)] : null;
    }

    /// <summary>拼接两段名字；任一段为 null 返回 null。</summary>
    private static string Join(string left, string right)
    {
        if (left == null || right == null) return null;
        return left + right;
    }
}
