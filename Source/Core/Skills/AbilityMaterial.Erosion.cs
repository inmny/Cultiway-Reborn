using System;
using System.IO;
using Newtonsoft.Json;

namespace Cultiway.Core.Skills;

/// <summary>独立材料可保存的有限侵蚀过程，经过时间不依赖原施放。</summary>
public struct MaterialErosionProcess
{
    /// <summary>世界内的稳定过程编号，零为没有等待关系的原过程。</summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public long Id;
    /// <summary>本段已经记录的实际损耗，旧过程从升级后计数。</summary>
    public double LostVolume, SpentResource;
    /// <summary>原定历时和投入份额。</summary>
    public float Seconds, Fraction;
    /// <summary>已累计的有效侵蚀秒数及已经扣除到的进度。</summary>
    public double Elapsed, Progress;
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)] private ConstructionProcessWindow[] TimeWindows;
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)] private double WorldElapsed;
    private CompiledProcessTime pace;
    /// <summary>材料变化或收束已经中断，读档不重新开始。</summary>
    public bool Interrupted;
    /// <summary>只有已声明且尚未完成、中断的过程继续推进。</summary>
    [JsonIgnore]
    public bool Active => Seconds > 0 && Progress < 1 && !Interrupted;

    /// <summary>从边界已经核对的配置开始。</summary>
    /// <param name="settings">真实留置配置。</param>
    internal MaterialErosionProcess(CompiledErosion settings)
    {
        this = default;
        Seconds = settings.Seconds; Fraction = settings.Fraction;
        pace = settings.Pace;
        TimeWindows = pace?.Describe();
    }

    /// <summary>以材料自己的世界时间换算推进量；到达终点时钳住实际进度。</summary>
    internal void AdvanceTime(float seconds)
    {
        if (pace == null) { Elapsed = Math.Min(Seconds, Elapsed + seconds); return; }
        WorldElapsed = Math.Min(pace.Completion, WorldElapsed + seconds);
        Elapsed = WorldElapsed >= pace.Completion ? Seconds : Math.Min(Seconds, pace.WorkAt(WorldElapsed));
    }

    /// <summary>只在正式资料读取边界检查进度，不用旧世界绝对时点补算。</summary>
    /// <param name="matter">保存时的实际材料。</param><param name="volume">实际余量。</param>
    internal void Validate(AbilityMatter matter, double volume)
    {
        bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        if (Id < 0 || !Finite(LostVolume) || LostVolume < 0 || !Finite(SpentResource) || SpentResource < 0 || !Finite(Seconds) || Seconds <= 0 || Seconds > 1000000000 || !Finite(Fraction) || Fraction <= 0 || Fraction > 1 ||
            !Finite(Elapsed) || Elapsed < 0 || Elapsed > Seconds || !Finite(Progress) || Progress < 0 || Progress > 1 ||
            Progress > Elapsed / Seconds + 1e-9 || Active && (matter.ErosionFactor <= 0 || volume <= 0))
            throw new InvalidDataException("世界材料的侵蚀历时、进度或实际材料无效");
        if (TimeWindows == null && WorldElapsed == 0) return;
        if (!(WorldElapsed >= 0) || TimeWindows is not { Length: > 0 })
            throw new InvalidDataException("世界材料的侵蚀变速时间无效");
        pace = new CompiledProcessTime(TimeWindows, Seconds, 1000000000, "世界材料侵蚀变速");
        double work = WorldElapsed >= pace.Completion ? Seconds : Math.Min(Seconds, pace.WorkAt(WorldElapsed));
        if (WorldElapsed > pace.Completion || Math.Abs(Elapsed - work) > 1e-9 * Math.Max(1d, Seconds))
            throw new InvalidDataException("世界材料的侵蚀变速时间与已累计进度不符");
    }

    /// <summary>随真实材料分开记录，空接收层不重复继承过程。</summary>
    /// <param name="fraction">交接的实际比例。</param>
    internal MaterialErosionProcess Share(double fraction)
    {
        if (fraction == 0) return default;
        var result = this;
        result.LostVolume *= fraction;
        result.SpentResource *= fraction;
        return result;
    }
}

/// <summary>独立材料的侵蚀、历史份额与实际几何一起维护。</summary>
public sealed partial class AbilityMaterial
{
    /// <summary>已经发生的实际侵蚀，来源与独立余量共同继承。</summary>
    public AbilityErosion Weathering;
    /// <summary>独立维护的有限过程，默认没有侵蚀。</summary>
    public MaterialErosionProcess Erosion;

    /// <summary>真实交接时开始，内部调用者已经核对当时材料。</summary>
    /// <param name="settings">已编译的历时及份额。</param>
    internal void BeginErosion(CompiledErosion settings) => Erosion = new MaterialErosionProcess(settings);

    /// <summary>中断保持损失和进度，完成后不改写事实。</summary>
    internal void InterruptErosion()
    {
        if (Erosion.Active) Erosion.Interrupted = true;
    }

    /// <summary>先累计共同时间，再按当前材料从当前余量扣除，最后登记真实损失。</summary>
    /// <param name="seconds">本次有效世界秒数。</param>
    private void AdvanceErosion(float seconds)
    {
        if (!Erosion.Active) return;
        if (Matter.ErosionFactor <= 0 || Volume <= 0) { InterruptErosion(); return; }
        Erosion.AdvanceTime(seconds);
        double progress = Erosion.Elapsed / Erosion.Seconds;
        if (progress == Erosion.Progress) return;
        float retained = (float)CompiledErosion.Retained(Erosion.Fraction * (double)Matter.ErosionFactor, Erosion.Progress, progress);
        bool changes = Flow == null && Volume * retained < Volume;
        if (Flow != null)
            foreach (var water in Flow.Water)
                if (water.x * retained < water.x) { changes = true; break; }
        if (!changes)
        {
            if (progress >= 1) InterruptErosion();
            return;
        }
        double before = Volume, resource = Resource;
        Retain(retained, recovery: false);
        Weathering.Add(before, before - Volume, resource - Resource);
        Erosion.LostVolume += before - Volume;
        Erosion.SpentResource += resource - Resource;
        Erosion.Progress = progress;
    }
}
