using System;

namespace Cultiway.Core.SubWorlds.Model;

/// <summary>
/// 定义小世界固定时钟的步长、局部倍率和单帧推进限制。
/// </summary>
public sealed class SubWorldClockProfileAsset : Asset
{
    /// <summary>每个逻辑 tick 推进的局部时间，单位为秒。</summary>
    public float fixed_step;

    /// <summary>把未缩放真实时间转换为局部时间的基础倍率。</summary>
    public float default_local_rate;

    /// <summary>实例允许选择的局部速度；值 0 表示暂停。</summary>
    public float[] allowed_local_speed_options = [];

    /// <summary>主世界暂停时是否继续累计该实例的局部时间。</summary>
    public bool runs_while_parent_paused;

    /// <summary>单个实例在一个渲染帧内最多执行的完整 tick 数。</summary>
    public int max_ticks_per_frame;

    /// <summary>验证时钟配置是否可以用于创建实例。</summary>
    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(id)) throw new InvalidOperationException("SubWorld ClockProfile 缺少 ID");
        if (fixed_step <= 0f)
            throw new InvalidOperationException($"SubWorld 固定步长必须大于 0: {id}");
        if (default_local_rate <= 0f)
            throw new InvalidOperationException($"SubWorld 默认速率必须大于 0: {id}");
        if (allowed_local_speed_options == null || allowed_local_speed_options.Length == 0)
            throw new InvalidOperationException($"SubWorld ClockProfile 缺少局部速度选项: {id}");
        if (!AllowsLocalSpeed(0f) || !AllowsLocalSpeed(1f))
            throw new InvalidOperationException($"SubWorld ClockProfile 必须支持暂停和 1x: {id}");
        if (max_ticks_per_frame <= 0)
            throw new InvalidOperationException($"SubWorld 单帧 tick 上限必须大于 0: {id}");
    }

    /// <summary>
    /// 判断指定局部速度是否由此配置允许。
    /// </summary>
    /// <param name="localSpeed">待检查的局部速度倍率。</param>
    /// <returns>允许使用时为 <see langword="true"/>。</returns>
    internal bool AllowsLocalSpeed(float localSpeed)
    {
        for (int i = 0; i < allowed_local_speed_options.Length; i++)
        {
            if (allowed_local_speed_options[i] == localSpeed) return true;
        }
        return false;
    }
}
