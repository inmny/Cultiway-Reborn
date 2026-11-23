using System;
using System.Diagnostics;
using UnityEngine;

namespace Cultiway.Core.Performance;

/// <summary>由开发测量临时订阅的帧内工作记录。游玩时不保存样本。</summary>
internal static class FrameWorkTrace
{
    /// <summary>按显示帧接收分组、阶段与主线程经过的毫秒。</summary>
    internal static Action<int, string, string, double> StageCompleted { get; set; }

    /// <summary>按显示帧接收视角、实例数、绘制命令数及上传字节数。</summary>
    internal static Action<int, string, int, int, int> Submitted { get; set; }

    /// <summary>沿用调度器量到的阶段耗时。</summary>
    internal static void Record(string group, string stage, double milliseconds) =>
        StageCompleted?.Invoke(Time.frameCount, group, stage, milliseconds);

    /// <summary>读取实际构造批次，提交前的空帧无需记录。</summary>
    internal static void RecordSubmission(string view, int instances, int draws, int bytes)
    {
        if (instances > 0 || draws > 0 || bytes > 0) Submitted?.Invoke(Time.frameCount, view, instances, draws, bytes);
    }

    /// <summary>从实际方法入口量到方法返回，无订阅时不读时钟。</summary>
    internal static StageScope Measure(string group, string stage) =>
        new(group, stage, StageCompleted == null ? 0 : Stopwatch.GetTimestamp());

    /// <summary>在提前返回和正常返回时都结束同一阶段。</summary>
    internal readonly struct StageScope : IDisposable
    {
        private readonly string group, stage;
        private readonly long started;

        internal StageScope(string group, string stage, long started)
        {
            this.group = group;
            this.stage = stage;
            this.started = started;
        }

        public void Dispose()
        {
            if (started == 0) return;
            Record(group, stage, (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency);
        }
    }
}
