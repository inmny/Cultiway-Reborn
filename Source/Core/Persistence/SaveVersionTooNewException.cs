using System;

namespace Cultiway.Core.Persistence;

public enum SaveVersionKind
{
    Format,
    Data
}

/// <summary>
/// 表示存档由更高版本的持久化协议或模块数据格式写入。
/// </summary>
public sealed class SaveVersionTooNewException : Exception
{
    public string DocumentId { get; }
    public SaveVersionKind VersionKind { get; }
    public int FoundVersion { get; }
    public int SupportedVersion { get; }

    public SaveVersionTooNewException(string documentId, SaveVersionKind versionKind,
        int foundVersion, int supportedVersion)
        : base(BuildMessage(documentId, versionKind, foundVersion, supportedVersion))
    {
        DocumentId = documentId;
        VersionKind = versionKind;
        FoundVersion = foundVersion;
        SupportedVersion = supportedVersion;
    }

    private static string BuildMessage(string documentId, SaveVersionKind versionKind,
        int foundVersion, int supportedVersion)
    {
        var kind = versionKind == SaveVersionKind.Format ? "持久化协议" : "数据";
        return $"持久化文档 {documentId} 的{kind}版本 {foundVersion} 高于当前支持版本 {supportedVersion}";
    }
}
