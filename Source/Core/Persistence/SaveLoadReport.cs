using System.Collections.Generic;

namespace Cultiway.Core.Persistence;

/// <summary>一次世界读取的转换与局部恢复结果；详细内容随原档备份保留。</summary>
public sealed class SaveLoadReport
{
    /// <summary>成功执行版本转换的资料数量。</summary>
    public int Upgraded;
    /// <summary>没有恢复的资料及原因。</summary>
    public readonly List<string> Skipped = new();
    /// <summary>原世界副本所在目录；没有转换或丢弃时不创建。</summary>
    public string BackupDirectory;
    /// <summary>备份未成功时的原因；原保存位置会被保护。</summary>
    public string BackupError;
    /// <summary>是否需要保存原世界副本。</summary>
    public bool RequiresBackup => Upgraded > 0 || Skipped.Count > 0;

    /// <summary>记录可局部隔离的读取问题，保留人物或世界的可读位置。</summary>
    public void Skip(string owner, string section, string reason)
        => Skipped.Add($"{owner} / {section}：{reason}");
}
