using System;
using System.IO;

namespace Cultiway.Core.Skills;

/// <summary>在构造引用边界补上真实字段位置，保留资源自身的缺项说明与原始异常。</summary>
internal static class ConstructionReferences
{
    /// <summary>只转换资源或资料引用错误；程序错误继续按原样报告。</summary>
    /// <typeparam name="T">已经解析的资源或数值。</typeparam>
    /// <param name="path">当前构造中的实际引用字段。</param><param name="read">资源本身的解析。</param>
    internal static T Resolve<T>(string path, Func<T> read)
    {
        try { return read(); }
        catch (Exception error) when (error is InvalidDataException or IOException or ArgumentException)
        { throw new InvalidDataException(path + "：" + error.Message, error); }
    }
}
