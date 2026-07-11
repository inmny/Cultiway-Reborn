using Newtonsoft.Json.Linq;

namespace Cultiway.Core.Persistence;

/// <summary>
/// 将引入统一文档协议前的旧文件转换为带版本的数据节点。
/// </summary>
public interface ISaveLegacyImporter
{
    bool TryImport(JObject root, out int dataVersion, out JObject data);
}

public sealed class LegacySaveSource
{
    public string StorageKey { get; }
    public ISaveLegacyImporter Importer { get; }

    public LegacySaveSource(string storageKey, ISaveLegacyImporter importer)
    {
        StorageKey = storageKey;
        Importer = importer;
    }
}
