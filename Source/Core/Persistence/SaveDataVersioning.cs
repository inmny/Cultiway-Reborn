using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Cultiway.Core.Persistence;

/// <summary>全局资料和世界资料共用的逐版本转换规则；转换不修改输入。</summary>
public static class SaveDataVersioning
{
    /// <summary>登记时检查每个相邻版本都有且只有一个转换。</summary>
    public static void Validate(string id, int currentVersion, IReadOnlyList<ISaveMigration> migrations)
    {
        if (currentVersion < 1) throw new ArgumentOutOfRangeException(nameof(currentVersion));
        if (migrations.Any(item => item == null || item.FromVersion < 1 || item.FromVersion >= currentVersion))
            throw new InvalidOperationException($"存档 {id} 包含范围外或空的转换");
        for (int version = 1; version < currentVersion; version++)
            if (migrations.Count(item => item.FromVersion == version) != 1)
                throw new InvalidOperationException($"存档 {id} 缺少唯一的 V{version} → V{version + 1} 转换");
    }

    /// <summary>复制原始数据并连续转换到当前版本；不把新版数据交给旧版读取。</summary>
    public static JObject Upgrade(string id, int currentVersion, IReadOnlyList<ISaveMigration> migrations,
        JObject original, int version)
    {
        if (version < 1 || original == null) throw new InvalidDataException("存档缺少有效版本或数据");
        if (version > currentVersion)
            throw new SaveVersionTooNewException(id, SaveVersionKind.Data, version, currentVersion);
        var data = (JObject)original.DeepClone();
        while (version < currentVersion)
        {
            ISaveMigration migration = migrations.Single(item => item.FromVersion == version);
            try
            {
                data = migration.Apply(data) ?? throw new InvalidDataException("转换返回了空数据");
            }
            catch (Exception error)
            {
                throw new InvalidDataException($"存档 {id} V{version} → V{version + 1} 转换失败", error);
            }
            version++;
        }
        return data;
    }
}
