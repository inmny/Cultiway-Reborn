using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Cultiway.Core.Persistence;
using Newtonsoft.Json.Linq;

namespace Cultiway.Core.CollectiveProjects;

/// <summary>集体工程的长期目标、验收基线和完成历史。</summary>
internal sealed class CollectiveProjectWorldSaveData
{
    /// <summary>已分配最大工程编号。</summary>
    public long NextId;
    /// <summary>工程记录。</summary>
    public List<CollectiveProjectSaveRecord> Projects = new();
    /// <summary>完成历史维持工程额度和相反目标的冷却。</summary>
    public List<CollectiveProjectCompletionSaveRecord> History = new();
}

/// <summary>当前工程的稳定资料，不保存角色人工智能执行句柄。</summary>
internal sealed class CollectiveProjectSaveRecord
{
    /// <summary>发起者类型。</summary>
    public string OwnerProvider;
    /// <summary>发起者编号。</summary>
    public long OwnerId;
    /// <summary>玩法自己的目标与验收基线。</summary>
    public JObject Payload;
    /// <summary>保存工程的 Id。</summary>
    public long Id;
    /// <summary>保存工程的 DefinitionId。</summary>
    public string DefinitionId;
    /// <summary>保存工程的 PlannerId。</summary>
    public string PlannerId;
    /// <summary>保存工程的 DeduplicationKey。</summary>
    public string DeduplicationKey;
    /// <summary>保存工程的 TargetTileId。</summary>
    public int TargetTileId;
    /// <summary>保存工程的 Urgency。</summary>
    public CollectiveProjectUrgency Urgency;
    /// <summary>保存工程的 Priority。</summary>
    public float Priority;
    /// <summary>保存工程的 State。</summary>
    public CollectiveProjectState State;
    /// <summary>保存工程的 CreatedAt。</summary>
    public double CreatedAt;
    /// <summary>保存工程的 LastProposedAt。</summary>
    public double LastProposedAt;
    /// <summary>保存工程的 ClaimedAt。</summary>
    public double ClaimedAt;
    /// <summary>保存工程的 FinishedAt。</summary>
    public double FinishedAt;
    /// <summary>保存工程的 ExecutingActorId。</summary>
    public long ExecutingActorId;
    /// <summary>保存工程的 ExecutionToken。</summary>
    public long ExecutionToken;
    /// <summary>保存工程的 ExecutionExpiresAt。</summary>
    public double ExecutionExpiresAt;
    /// <summary>保存工程的 VerifyAt。</summary>
    public double VerifyAt;
    /// <summary>保存工程的 HistoryTag。</summary>
    public string HistoryTag;
    /// <summary>保存工程的 ConflictingHistoryTags。</summary>
    public string[] ConflictingHistoryTags;
    /// <summary>保存工程的 ConflictWindowSeconds。</summary>
    public double ConflictWindowSeconds;
    /// <summary>保存工程的 ConflictRadius。</summary>
    public float ConflictRadius;
}

/// <summary>已经完成的工程额度记录。</summary>
internal sealed class CollectiveProjectCompletionSaveRecord
{
    /// <summary>发起者类型。</summary>
    public string OwnerProvider;
    /// <summary>发起者编号。</summary>
    public long OwnerId;
    /// <summary>工程定义。</summary>
    public string Definition;
    /// <summary>历史分类。</summary>
    public string Tag;
    /// <summary>目标地块。</summary>
    public int Tile;
    /// <summary>完成时间。</summary>
    public double Completed;
    /// <summary>保留秒数。</summary>
    public double Retention;
}

/// <summary>集体工程随世界保存，读取重建调度索引。</summary>
public static partial class CollectiveProjectService
{
    /// <summary>由具体玩法提供目标记录的采集，通用层不保存玩法对象类型名。</summary>
    /// <param name="capturePayload">玩法提供的目标采集方法。</param>
    internal static CollectiveProjectWorldSaveData CaptureWorldSave(Func<object, JObject> capturePayload)
    {
        lock (Sync)
        {
            var data = new CollectiveProjectWorldSaveData { NextId = _nextProjectId };
            foreach (ProjectRecord value in Projects.Values)
                data.Projects.Add(new CollectiveProjectSaveRecord
                {
                    OwnerProvider = value.Owner.ProviderId, OwnerId = value.Owner.OwnerId,
                    Payload = capturePayload(value.Payload),
                    Id = value.Id,
                    DefinitionId = value.DefinitionId,
                    PlannerId = value.PlannerId,
                    DeduplicationKey = value.DeduplicationKey,
                    TargetTileId = value.TargetTileId,
                    Urgency = value.Urgency,
                    Priority = value.Priority,
                    State = value.State,
                    CreatedAt = value.CreatedAt,
                    LastProposedAt = value.LastProposedAt,
                    ClaimedAt = value.ClaimedAt,
                    FinishedAt = value.FinishedAt,
                    ExecutingActorId = value.ExecutingActorId,
                    ExecutionToken = value.ExecutionToken,
                    ExecutionExpiresAt = value.ExecutionExpiresAt,
                    VerifyAt = value.VerifyAt,
                    HistoryTag = value.HistoryTag,
                    ConflictingHistoryTags = value.ConflictingHistoryTags,
                    ConflictWindowSeconds = value.ConflictWindowSeconds,
                    ConflictRadius = value.ConflictRadius,
                });
            foreach (CompletionRecord value in CompletionHistory)
                data.History.Add(new CollectiveProjectCompletionSaveRecord
                {
                    OwnerProvider = value.Owner.ProviderId, OwnerId = value.Owner.OwnerId, Definition = value.DefinitionId,
                    Tag = value.HistoryTag, Tile = value.TargetTileId, Completed = value.CompletedAt, Retention = value.RetentionSeconds
                });
            return data;
        }
    }

    /// <summary>逐项恢复工程；认领重新调度，已经施放的短时法术按实际世界状态继续验收。</summary>
    /// <param name="data">待保存或恢复的资料。</param>
    /// <param name="restorePayload">玩法提供的目标恢复方法。</param>
    /// <param name="report">本次读取的转换与跳过报告。</param>
    internal static void RestoreWorldSave(CollectiveProjectWorldSaveData data, Func<JObject, object> restorePayload, SaveLoadReport report)
    {
        if (data.Projects.Select(value => value.Id).Distinct().Count() != data.Projects.Count)
            throw new InvalidDataException("工程编号重复");
        lock (Sync)
        {
            foreach (CollectiveProjectSaveRecord value in data.Projects)
            {
                if (!Definitions.ContainsKey(value.DefinitionId) || !OwnerAdapters.TryGetValue(value.OwnerProvider, out var adapter) ||
                    !adapter.TryResolve(value.OwnerId, out _) || value.TargetTileId < -1 || value.TargetTileId >= World.world.tiles_list.Length)
                {
                    report.Skip("集体工程", value.Id.ToString(), "定义、发起者或目标已不存在");
                    continue;
                }
                object payload;
                try { payload = restorePayload(value.Payload); }
                catch (InvalidDataException error)
                {
                    report.Skip("集体工程", value.Id.ToString(), error.Message);
                    continue;
                }
                var project = new ProjectRecord
                {
                    Owner = new CollectiveProjectOwnerKey(value.OwnerProvider, value.OwnerId), Payload = payload,
                    Id = value.Id,
                    DefinitionId = value.DefinitionId,
                    PlannerId = value.PlannerId,
                    DeduplicationKey = value.DeduplicationKey,
                    TargetTileId = value.TargetTileId,
                    Urgency = value.Urgency,
                    Priority = value.Priority,
                    State = value.State,
                    CreatedAt = value.CreatedAt,
                    LastProposedAt = value.LastProposedAt,
                    ClaimedAt = value.ClaimedAt,
                    FinishedAt = value.FinishedAt,
                    ExecutingActorId = value.ExecutingActorId,
                    ExecutionToken = value.ExecutionToken,
                    ExecutionExpiresAt = value.ExecutionExpiresAt,
                    VerifyAt = value.VerifyAt,
                    HistoryTag = value.HistoryTag,
                    ConflictingHistoryTags = value.ConflictingHistoryTags,
                    ConflictWindowSeconds = value.ConflictWindowSeconds,
                    ConflictRadius = value.ConflictRadius,
                };
                if (project.State == CollectiveProjectState.Claimed) project.State = CollectiveProjectState.Planned;
                if (project.State == CollectiveProjectState.Executing) project.State = CollectiveProjectState.Verifying;
                project.ExecutingActorId = 0;
                project.ExecutionToken = 0;
                Projects.Add(project.Id, project);
                ProjectsByDeduplicationKey[project.DeduplicationKey] = project.Id;
                ProjectSweepQueue.Enqueue(project.Id);
            }
            foreach (CollectiveProjectCompletionSaveRecord value in data.History)
                CompletionHistory.Add(new CompletionRecord(new CollectiveProjectOwnerKey(value.OwnerProvider, value.OwnerId),
                    value.Definition, value.Tag, value.Tile, value.Completed, value.Retention));
            _nextProjectId = Math.Max(data.NextId, data.Projects.Select(value => value.Id).DefaultIfEmpty(0).Max());
        }
    }
}
