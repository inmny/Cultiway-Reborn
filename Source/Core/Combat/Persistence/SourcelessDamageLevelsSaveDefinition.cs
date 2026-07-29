using System;
using Cultiway.Core.Persistence;
using UnityEngine;

namespace Cultiway.Core.Combat;

/// <summary>
/// <see cref="SourcelessDamageLevels"/> 的持久化文档定义，参考万法阁/百宝阁的 SaveDocument 模式。
/// 文件位于 <c>&lt;persistentDataPath&gt;/Cultiway/Saves/global/sourceless_damage_levels.json</c>。
/// </summary>
internal static class SourcelessDamageLevelsSaveDefinition
{
    public const string DocumentId = "sourceless_damage_levels";
    public const int CurrentVersion = 1;

    public static SaveDocumentDefinition<SourcelessDamageLevelsData> Create()
    {
        return new SaveDocumentDefinition<SourcelessDamageLevelsData>(
            DocumentId,
            "Saves/global/sourceless_damage_levels",
            CurrentVersion,
            () => new SourcelessDamageLevelsData(),
            Array.Empty<ISaveMigration>(),
            Normalize);
    }

    /// <summary>写入前把等级夹取并取整到 [0, <see cref="SourcelessDamageLevels.MaxLevel"/>]。</summary>
    private static void Normalize(SourcelessDamageLevelsData data)
    {
        if (data.Levels == null)
        {
            data.Levels = Array.Empty<float>();
            return;
        }

        for (int i = 0; i < data.Levels.Length; i++)
        {
            data.Levels[i] = Mathf.RoundToInt(Mathf.Clamp(data.Levels[i], 0f, SourcelessDamageLevels.MaxLevel));
        }
    }
}
