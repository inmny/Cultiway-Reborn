using System;
using System.Collections.Generic;
using Cultiway.Core.CollectiveProjects;
using Cultiway.Patch;

namespace Cultiway.Core.Coordination;

/// <summary>
/// 让集体工程的执行令牌等待一个协调行动结果。工程生命周期与行动生命周期仍保持独立。
/// </summary>
public static class CollectiveProjectActivityBridge
{
    private static readonly Dictionary<long, Binding> Bindings = new();
    private static bool initialized;

    /// <summary>绑定行动完成事件与世界清理回调。</summary>
    public static void Initialize()
    {
        if (initialized) return;
        initialized = true;
        CoordinatedActivityService.ActivityEnded += OnActivityEnded;
        PatchMapBox.RegisterActionOnClearWorld(Bindings.Clear);
    }

    /// <summary>
    /// 把一个正在执行的工程令牌绑定到行动；同一行动只允许对应一个工程执行。
    /// </summary>
    public static bool TryBind(
        long activityId,
        long projectId,
        long actorId,
        long executionToken,
        double verificationDelaySeconds,
        bool retryOnFailure)
    {
        if (activityId <= 0 || projectId <= 0 || executionToken <= 0) return false;
        if (Bindings.ContainsKey(activityId)) return false;
        Bindings.Add(
            activityId,
            new Binding(
                projectId,
                actorId,
                executionToken,
                Math.Max(0d, verificationDelaySeconds),
                retryOnFailure));
        return true;
    }

    /// <summary>根据行动最终结果推进工程验收或失败流程。</summary>
    private static void OnActivityEnded(CoordinatedActivityResult result)
    {
        if (!Bindings.TryGetValue(result.ActivityId, out Binding binding)) return;
        Bindings.Remove(result.ActivityId);
        if (result.Reason == CoordinatedActivityEndReason.Completed)
        {
            CollectiveProjectService.TryBeginVerification(
                binding.ProjectId,
                binding.ActorId,
                binding.ExecutionToken,
                binding.VerificationDelaySeconds);
            return;
        }
        CollectiveProjectService.TryFailExecution(
            binding.ProjectId,
            binding.ExecutionToken,
            binding.RetryOnFailure);
    }

    /// <summary>一个协调行动对应的工程执行令牌。</summary>
    private readonly struct Binding
    {
        /// <summary>创建工程绑定。</summary>
        internal Binding(
            long projectId,
            long actorId,
            long executionToken,
            double verificationDelaySeconds,
            bool retryOnFailure)
        {
            ProjectId = projectId;
            ActorId = actorId;
            ExecutionToken = executionToken;
            VerificationDelaySeconds = verificationDelaySeconds;
            RetryOnFailure = retryOnFailure;
        }

        internal long ProjectId { get; }
        internal long ActorId { get; }
        internal long ExecutionToken { get; }
        internal double VerificationDelaySeconds { get; }
        internal bool RetryOnFailure { get; }
    }
}
