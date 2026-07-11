using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Cultiway.Core.Persistence;

/// <summary>
/// 管理 Mod 自有持久化文档的加载、连续迁移和原子写入。
/// </summary>
public sealed class ModSaveManager
{
    private readonly ISaveStorage _storage;
    private readonly Dictionary<string, object> _documents = new(StringComparer.Ordinal);
    private readonly HashSet<string> _storageKeys = new(StringComparer.OrdinalIgnoreCase);

    public ModSaveManager(ISaveStorage storage)
    {
        _storage = storage;
    }

    public SaveDocument<TData> Register<TData>(SaveDocumentDefinition<TData> definition)
    {
        ValidateDefinition(definition);
        if (_documents.ContainsKey(definition.Id))
        {
            throw new InvalidOperationException($"持久化文档已注册: {definition.Id}");
        }
        if (_storageKeys.Contains(definition.StorageKey))
        {
            throw new InvalidOperationException($"持久化存储键已注册: {definition.StorageKey}");
        }

        var document = new SaveDocument<TData>(this, definition);
        document.Data = Load(definition);
        _documents.Add(definition.Id, document);
        _storageKeys.Add(definition.StorageKey);
        return document;
    }

    internal void Save<TData>(SaveDocument<TData> document)
    {
        lock (document)
        {
            var definition = document.Definition;
            definition.Normalize?.Invoke(document.Data);
            EnsureValid(definition, document.Data);
            Write(definition, document.Data, false);
        }
    }

    private TData Load<TData>(SaveDocumentDefinition<TData> definition)
    {
        if (_storage.Exists(definition.StorageKey, SaveCopy.Primary))
        {
            try
            {
                LoadEnvelope(definition, SaveCopy.Primary, out var migrated, out var data);
                return CompleteLoad(definition, data, migrated, false);
            }
            catch (SaveVersionTooNewException)
            {
                throw;
            }
            catch (Exception primaryError)
            {
                if (!_storage.Exists(definition.StorageKey, SaveCopy.Backup)) throw;
                try
                {
                    LoadEnvelope(definition, SaveCopy.Backup, out _, out var backupData);
                    var loaded = CompleteLoad(definition, backupData, true, true);
                    ModClass.LogWarning($"持久化文档 {definition.Id} 已从备份恢复: {primaryError.Message}");
                    return loaded;
                }
                catch (Exception backupError)
                {
                    throw new InvalidDataException(
                        $"持久化文档 {definition.Id} 的主文件与备份均无法加载", new AggregateException(primaryError, backupError));
                }
            }
        }

        foreach (var source in definition.LegacySources)
        {
            if (!_storage.Exists(source.StorageKey, SaveCopy.Primary) &&
                !_storage.Exists(source.StorageKey, SaveCopy.Backup)) continue;
            return LoadLegacy(definition, source);
        }

        var created = definition.CreateDefault();
        definition.Normalize?.Invoke(created);
        EnsureValid(definition, created);
        return created;
    }

    private TData LoadLegacy<TData>(SaveDocumentDefinition<TData> definition, LegacySaveSource source)
    {
        Exception primaryError = null;
        if (_storage.Exists(source.StorageKey, SaveCopy.Primary))
        {
            try
            {
                var data = ImportLegacy(source, SaveCopy.Primary, out var version);
                var migrated = Migrate(definition, data, version);
                var loaded = DeserializeCurrent(definition, migrated);
                Write(definition, loaded, false);
                PreserveLegacy(definition.Id, source.StorageKey, SaveCopy.Primary);
                return loaded;
            }
            catch (SaveVersionTooNewException)
            {
                throw;
            }
            catch (Exception error)
            {
                primaryError = error;
            }
        }

        if (!_storage.Exists(source.StorageKey, SaveCopy.Backup)) throw primaryError;
        try
        {
            var data = ImportLegacy(source, SaveCopy.Backup, out var version);
            var migrated = Migrate(definition, data, version);
            var loaded = DeserializeCurrent(definition, migrated);
            Write(definition, loaded, false);
            PreserveLegacy(definition.Id, source.StorageKey, SaveCopy.Backup);
            ModClass.LogWarning($"旧持久化文档 {definition.Id} 已从备份导入: {primaryError?.Message}");
            return loaded;
        }
        catch (Exception backupError)
        {
            var causes = primaryError == null
                ? new[] { backupError }
                : new[] { primaryError, backupError };
            throw new InvalidDataException(
                $"旧持久化文档 {definition.Id} 的主文件与备份均无法导入",
                new AggregateException(causes));
        }
    }

    private void LoadEnvelope<TData>(SaveDocumentDefinition<TData> definition, SaveCopy copy,
        out bool migrated, out JObject data)
    {
        var root = JObject.Parse(_storage.Read(definition.StorageKey, copy));
        var envelope = root.ToObject<SaveEnvelope>();
        if (envelope == null || envelope.Data == null) throw new InvalidDataException("持久化文档缺少数据节点");
        if (envelope.FormatVersion > SaveEnvelope.CurrentFormatVersion)
        {
            throw new SaveVersionTooNewException(definition.Id, SaveVersionKind.Format,
                envelope.FormatVersion, SaveEnvelope.CurrentFormatVersion);
        }
        if (envelope.FormatVersion != SaveEnvelope.CurrentFormatVersion)
        {
            throw new InvalidDataException($"不支持的持久化协议版本: {envelope.FormatVersion}");
        }
        if (!string.Equals(envelope.DocumentId, definition.Id, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"持久化文档标识不匹配: {envelope.DocumentId}");
        }
        if (envelope.DataVersion > definition.CurrentVersion)
        {
            throw new SaveVersionTooNewException(definition.Id, SaveVersionKind.Data,
                envelope.DataVersion, definition.CurrentVersion);
        }

        migrated = envelope.DataVersion < definition.CurrentVersion;
        data = Migrate(definition, envelope.Data, envelope.DataVersion);
    }

    private TData CompleteLoad<TData>(SaveDocumentDefinition<TData> definition, JObject data,
        bool writeBack, bool mirrorBackup)
    {
        var loaded = DeserializeCurrent(definition, data);
        writeBack |= !JToken.DeepEquals(data, JObject.FromObject(loaded));
        if (writeBack) Write(definition, loaded, mirrorBackup);
        return loaded;
    }

    private void PreserveLegacy(string documentId, string storageKey, SaveCopy copy)
    {
        try
        {
            _storage.PreserveLegacy(storageKey, copy);
        }
        catch (Exception error)
        {
            ModClass.LogWarning($"持久化文档 {documentId} 已导入，但保留旧文件副本失败: {error.Message}");
        }
    }

    private JObject ImportLegacy(LegacySaveSource source, SaveCopy copy, out int version)
    {
        var root = JObject.Parse(_storage.Read(source.StorageKey, copy));
        if (!source.Importer.TryImport(root, out version, out var data) || data == null)
        {
            throw new InvalidDataException($"旧文件不符合导入格式: {source.StorageKey}");
        }
        return data;
    }

    private static JObject Migrate<TData>(SaveDocumentDefinition<TData> definition, JObject original, int version)
    {
        if (version < 1) throw new InvalidDataException($"无效的数据版本: {version}");
        if (version > definition.CurrentVersion)
        {
            throw new SaveVersionTooNewException(definition.Id, SaveVersionKind.Data,
                version, definition.CurrentVersion);
        }

        var data = (JObject)original.DeepClone();
        while (version < definition.CurrentVersion)
        {
            var migration = definition.Migrations.Single(item => item.FromVersion == version);
            try
            {
                data = migration.Apply(data) ?? throw new InvalidDataException("迁移返回了空数据");
            }
            catch (Exception error)
            {
                throw new InvalidDataException(
                    $"持久化迁移 {definition.Id} V{version} -> V{version + 1} 失败", error);
            }
            version++;
        }
        return data;
    }

    private static TData DeserializeCurrent<TData>(SaveDocumentDefinition<TData> definition, JObject data)
    {
        var loaded = data.ToObject<TData>();
        if (loaded == null) throw new InvalidDataException($"持久化文档无法反序列化: {definition.Id}");
        definition.Normalize?.Invoke(loaded);
        EnsureValid(definition, loaded);
        return loaded;
    }

    private void Write<TData>(SaveDocumentDefinition<TData> definition, TData data, bool mirrorBackup)
    {
        var envelope = new SaveEnvelope
        {
            DocumentId = definition.Id,
            DataVersion = definition.CurrentVersion,
            Data = JObject.FromObject(data)
        };
        var json = JsonConvert.SerializeObject(envelope, Formatting.Indented);
        _storage.WriteAtomic(definition.StorageKey, json, mirrorBackup);
    }

    private static void EnsureValid<TData>(SaveDocumentDefinition<TData> definition, TData data)
    {
        if (data == null) throw new InvalidDataException($"持久化文档数据为空: {definition.Id}");
        var error = definition.Validate?.Invoke(data);
        if (!string.IsNullOrEmpty(error)) throw new InvalidDataException(error);
    }

    private static void ValidateDefinition<TData>(SaveDocumentDefinition<TData> definition)
    {
        if (definition == null) throw new ArgumentNullException(nameof(definition));
        if (string.IsNullOrWhiteSpace(definition.Id)) throw new ArgumentException("持久化文档标识不能为空");
        if (string.IsNullOrWhiteSpace(definition.StorageKey)) throw new ArgumentException("持久化存储键不能为空");
        if (definition.CurrentVersion < 1) throw new ArgumentOutOfRangeException(nameof(definition.CurrentVersion));
        if (definition.CreateDefault == null) throw new ArgumentException("持久化文档必须提供默认数据工厂");

        if (definition.Migrations.Any(item => item == null))
        {
            throw new InvalidOperationException($"持久化文档 {definition.Id} 包含空迁移步骤");
        }
        if (definition.LegacySources.Any(source => source == null ||
                string.IsNullOrWhiteSpace(source.StorageKey) || source.Importer == null))
        {
            throw new InvalidOperationException($"持久化文档 {definition.Id} 包含无效的旧文件来源");
        }
        if (definition.LegacySources.GroupBy(source => source.StorageKey, StringComparer.OrdinalIgnoreCase)
            .Any(group => group.Count() > 1))
        {
            throw new InvalidOperationException($"持久化文档 {definition.Id} 重复注册了旧文件来源");
        }

        var migrations = definition.Migrations.OrderBy(item => item.FromVersion).ToList();
        for (var version = 1; version < definition.CurrentVersion; version++)
        {
            if (migrations.Count(item => item.FromVersion == version) != 1)
            {
                throw new InvalidOperationException(
                    $"持久化文档 {definition.Id} 缺少唯一的 V{version} -> V{version + 1} 迁移");
            }
        }
        if (migrations.Any(item => item.FromVersion < 1 || item.FromVersion >= definition.CurrentVersion))
        {
            throw new InvalidOperationException($"持久化文档 {definition.Id} 注册了范围外的迁移");
        }
    }
}
