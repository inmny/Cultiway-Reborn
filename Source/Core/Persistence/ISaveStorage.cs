namespace Cultiway.Core.Persistence;

public enum SaveCopy
{
    Primary,
    Backup
}

/// <summary>
/// 持久化文档的物理存储后端。
/// </summary>
public interface ISaveStorage
{
    bool Exists(string storageKey, SaveCopy copy);
    string Read(string storageKey, SaveCopy copy);
    void WriteAtomic(string storageKey, string content, bool mirrorBackup);
    void PreserveLegacy(string storageKey, SaveCopy copy);
}
