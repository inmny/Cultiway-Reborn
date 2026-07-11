using System;
using System.IO;
using System.Text;

namespace Cultiway.Core.Persistence;

/// <summary>
/// 将 Mod 全局文档存放在持久化目录中的文件存储后端。
/// </summary>
public sealed class GlobalFileSaveStorage : ISaveStorage
{
    private readonly string _root;
    private readonly string _rootPrefix;

    public GlobalFileSaveStorage(string root)
    {
        _root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        _rootPrefix = _root + Path.DirectorySeparatorChar;
    }

    public bool Exists(string storageKey, SaveCopy copy)
    {
        return File.Exists(ResolvePath(storageKey, copy));
    }

    public string Read(string storageKey, SaveCopy copy)
    {
        return File.ReadAllText(ResolvePath(storageKey, copy), Encoding.UTF8);
    }

    public void WriteAtomic(string storageKey, string content, bool mirrorBackup)
    {
        var path = ResolvePath(storageKey, SaveCopy.Primary);
        var backupPath = ResolvePath(storageKey, SaveCopy.Backup);
        var tempPath = path + ".tmp";
        Directory.CreateDirectory(Path.GetDirectoryName(path));

        using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
        {
            writer.Write(content);
            writer.Flush();
            stream.Flush(true);
        }

        if (File.Exists(path))
        {
            File.Replace(tempPath, path, backupPath, true);
        }
        else
        {
            File.Move(tempPath, path);
        }

        if (mirrorBackup)
        {
            File.Copy(path, backupPath, true);
        }
    }

    public void PreserveLegacy(string storageKey, SaveCopy copy)
    {
        var path = ResolvePath(storageKey, copy);
        if (!File.Exists(path)) return;
        var primaryPath = ResolvePath(storageKey, SaveCopy.Primary);
        File.Copy(path, primaryPath + ".legacy.bak", true);
    }

    private string ResolvePath(string storageKey, SaveCopy copy)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            throw new ArgumentException("存储键不能为空", nameof(storageKey));
        }

        var relativePath = storageKey.Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar) + ".json";
        var path = Path.GetFullPath(Path.Combine(_root, relativePath));
        if (!path.StartsWith(_rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"存储键越出持久化目录: {storageKey}");
        }

        return copy == SaveCopy.Backup ? path + ".bak" : path;
    }
}
