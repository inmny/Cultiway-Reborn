using System;
using System.IO;

namespace Cultiway.Core.Skills;

/// <summary>在构造引用边界补上真实字段位置，保留资源自身的缺项说明与原始异常。</summary>
internal static class ConstructionReferences
{
    /// <summary>保存位置和另一处冲突的声明，界面不拆解面向玩家的错误文字。</summary>
    /// <param name="path">需要定位的字段或操作。</param><param name="reason">编译拒绝原因。</param>
    /// <param name="cause">原异常。</param><param name="related">共同占用的另一项声明。</param>
    /// <param name="part">实际展开部位。</param><param name="declaration">可编辑的原部位。</param>
    internal static InvalidDataException Error(string path, string reason, Exception cause = null, string related = null,
        string part = null, string declaration = null)
    {
        string position = part == null || path.StartsWith(part + "/", StringComparison.Ordinal) ? path : part + " · " + path;
        var error = new InvalidDataException(position + "：" + reason + (related == null ? "" : "；另一处：" + related), cause);
        error.Data[typeof(ConstructionIssue)] = new ConstructionIssue(path, related, part, declaration);
        return error;
    }

    /// <summary>只转换资源或资料引用错误；程序错误继续按原样报告。</summary>
    /// <typeparam name="T">已经解析的资源或数值。</typeparam>
    /// <param name="path">当前构造中的实际引用字段。</param><param name="read">资源本身的解析。</param>
    internal static T Resolve<T>(string path, Func<T> read)
    {
        try { return read(); }
        catch (Exception error) when (error is InvalidDataException or IOException or ArgumentException)
        {
            if (ConstructionIssue.From(error) != null) throw;
            string prefix = path + "：";
            string reason = error.Message.StartsWith(prefix, StringComparison.Ordinal) ? error.Message.Substring(prefix.Length) : error.Message;
            throw Error(path, reason, error);
        }
    }
}


/// <summary>只描述构造检查失败的位置，不持有计划、草稿或运行对象。</summary>
internal sealed class ConstructionIssue
{
    /// <summary>优先定位的部位、字段或操作。</summary>
    internal readonly string Path;
    /// <summary>占用同一作用的另一处声明。</summary>
    internal readonly string Related;
    /// <summary>实际展开位置及可编辑声明；不明确时不猜测。</summary>
    internal readonly string Part, Declaration;

    /// <summary>位置信息随原有资料异常传递。</summary>
    /// <param name="path">当前路径。</param><param name="related">另一项路径。</param>
    /// <param name="part">实际展开部位。</param><param name="declaration">原声明部位。</param>
    internal ConstructionIssue(string path, string related, string part, string declaration)
    { Path = path; Related = related; Part = part; Declaration = declaration; }

    /// <summary>普通文件及输入异常可以没有可定位的构造位置。</summary>
    /// <param name="error">正式检查给出的失败。</param>
    internal static ConstructionIssue From(Exception error) => error.Data[typeof(ConstructionIssue)] as ConstructionIssue;
}
