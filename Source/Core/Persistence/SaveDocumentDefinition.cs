using System;
using System.Collections.Generic;

namespace Cultiway.Core.Persistence;

/// <summary>
/// 描述一个由功能模块拥有的独立持久化文档。
/// </summary>
public sealed class SaveDocumentDefinition<TData>
{
    public string Id { get; }
    public string StorageKey { get; }
    public int CurrentVersion { get; }
    public Func<TData> CreateDefault { get; }
    public Action<TData> Normalize { get; }
    public Func<TData, string> Validate { get; }
    public IReadOnlyList<ISaveMigration> Migrations { get; }
    public IReadOnlyList<LegacySaveSource> LegacySources { get; }

    public SaveDocumentDefinition(
        string id,
        string storageKey,
        int currentVersion,
        Func<TData> createDefault,
        IReadOnlyList<ISaveMigration> migrations,
        Action<TData> normalize = null,
        Func<TData, string> validate = null,
        IReadOnlyList<LegacySaveSource> legacySources = null)
    {
        Id = id;
        StorageKey = storageKey;
        CurrentVersion = currentVersion;
        CreateDefault = createDefault;
        Migrations = migrations ?? Array.Empty<ISaveMigration>();
        Normalize = normalize;
        Validate = validate;
        LegacySources = legacySources ?? Array.Empty<LegacySaveSource>();
    }
}
