using System;
using System.IO;
using Cultiway.Utils.Extension;
using Newtonsoft.Json;
using UnityEngine;

namespace Cultiway.Core.Persistence;

/// <summary>协调原版人物、世界的模组资料与恢复报告；不建立第二套世界保存按钮。</summary>
public sealed class WorldSaveCoordinator
{
    /// <summary>人物独立资料的原版自定义数据键。</summary>
    public const string ActorDataKey = "cultiway.actor";
    /// <summary>世界独立资料的原版自定义数据键。</summary>
    public const string WorldDataKey = "cultiway.world";
    /// <summary>人物各玩法的独立版本及恢复顺序。</summary>
    public SaveSectionRegistry<ActorExtend> Actors { get; } = new();
    /// <summary>原版人物与建筑就绪后恢复的世界资料。</summary>
    public SaveSectionRegistry<MapBox> World { get; } = new();
    /// <summary>最近一次读取的结果，供调试和后续详情显示。</summary>
    public SaveLoadReport LastReport { get; private set; } = new();
    /// <summary>备份失败后不能覆盖的原世界路径；另存其他目录不受影响。</summary>
    private string protectedSource;

    /// <summary>只保存已有角色状态，不在保存过程中触发出生或随机成长。</summary>
    public void CaptureActor(ActorExtend actor)
    {
        lock (EntityStoreLock.GlobalLock)
            actor.Base.data.set(ActorDataKey, Actors.Capture(actor, ModClass.I.GetDeclaration().Version));
    }

    /// <summary>在原版读取单位后恢复，使境界属性早于原版后续清理生效。</summary>
    public void RestoreActor(Actor owner)
    {
        owner.data.get(ActorDataKey, out string json, "");
        if (!string.IsNullOrEmpty(json))
            lock (EntityStoreLock.GlobalLock)
                Actors.Restore(owner.GetExtend(), json, LastReport, $"人物 {owner.data.id}");
    }

    /// <summary>写入原版世界自定义资料，分享原版世界文件时自然一起携带。</summary>
    public void CaptureWorld(SavedMap snapshot)
    {
        snapshot.mapStats.custom_data ??= new SaveCustomData();
        lock (EntityStoreLock.GlobalLock)
            snapshot.mapStats.custom_data.set(WorldDataKey, World.Capture(global::World.world,
                ModClass.I.GetDeclaration().Version));
    }

    /// <summary>原版全部对象读取后恢复世界资料，再保留原档和报告。</summary>
    public void FinishLoad(SavedMap snapshot, string sourcePath)
    {
        string json = "";
        if (snapshot.mapStats.custom_data != null) snapshot.mapStats.custom_data.get(WorldDataKey, out json, "");
        lock (EntityStoreLock.GlobalLock)
            World.Restore(global::World.world, json, LastReport, "世界");
        if (!LastReport.RequiresBackup) return;

        // 转换始终只在内存中；原版恢复结束后、玩家能够覆盖保存之前复制原始目录。
        if (!string.IsNullOrEmpty(sourcePath))
        {
            try
            {
                string source = NormalizePath(sourcePath);
                string destination = Path.Combine(Application.persistentDataPath, "Cultiway", "Backups", "worlds",
                    DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N"));
                CopyDirectory(source, destination);
                LastReport.BackupDirectory = destination;
                File.WriteAllText(Path.Combine(destination, "cultiway-load-report.json"),
                    JsonConvert.SerializeObject(LastReport, Formatting.Indented));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                LastReport.BackupError = error.Message;
                protectedSource = NormalizePath(sourcePath);
                ModClass.LogWarning($"原档备份失败，已保护原保存位置：{error}");
            }
        }
        foreach (string issue in LastReport.Skipped) ModClass.LogWarning(issue);
        ShowReport();
    }

    /// <summary>只在原档确实无法备份时阻止覆盖，仍然允许另存。</summary>
    public void EnsureCanSave(string destination)
    {
        if (protectedSource == null || !string.Equals(protectedSource, NormalizePath(destination),
                Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) return;
        string message = "Cultiway.Save.Protected".Localize();
        WorldTip.showNow(message, false, "top", 10f);
        throw new IOException(message);
    }

    /// <summary>换世界时清除上一份恢复结果，不清除已登记的部分。</summary>
    public void ClearWorldState()
    {
        LastReport = new SaveLoadReport();
        protectedSource = null;
    }

    /// <summary>汇总成功转换与跳过的数量，详细原因保存在备份目录的报告中。</summary>
    private void ShowReport()
    {
        string key = LastReport.BackupError != null ? "Cultiway.Save.BackupFailed" :
            LastReport.BackupDirectory != null ? "Cultiway.Save.Recovered" : "Cultiway.Save.ImportRecovered";
        WorldTip.showNow(string.Format(key.Localize(), LastReport.Upgraded, LastReport.Skipped.Count),
            false, "top", 15f);
        if (LastReport.BackupDirectory != null) ModClass.LogInfo($"原档与恢复报告：{LastReport.BackupDirectory}");
    }

    /// <summary>复制原世界的全部文件，包括其他模组随世界保存的资料。</summary>
    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        foreach (string directory in Directory.GetDirectories(source))
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
    }

    /// <summary>把原版传入的有无尾部分隔符的目录统一成同一路径。</summary>
    private static string NormalizePath(string path)
        => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
