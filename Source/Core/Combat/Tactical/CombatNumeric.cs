namespace Cultiway.Core.Combat.Tactical;

/// <summary>
/// 战术层的跨类型数值换算入口。
/// Unity 2022.3 自带的 Mono 运行时在生成机器码时，一旦「整数或浮点转双精度」的转换指令
/// 源操作数被安排进栈内存，就会触发指令长度估算断言并直接中止游戏
/// （崩溃日志特征：mini-amd64.c:7388 fconv_to_r8_x expected 4 got 5）。
/// 因此这类转换不允许写在战术链的大方法体内，必须经由本类的小方法完成：
/// 小方法的参数通过寄存器传入，转换指令不会使用长位移的内存编码。
/// </summary>
internal static class CombatNumeric
{
    /// <summary>把浮点秒数提升为双精度秒数，供与世界时间（双精度）混合运算。</summary>
    internal static double ToDouble(float value)
    {
        return value;
    }

    /// <summary>把高精度计时器的刻度差换算为秒，只在性能采样期间会真正执行。</summary>
    internal static double TicksToSeconds(long elapsedTicks, long ticksPerSecond)
    {
        return elapsedTicks / (double)ticksPerSecond;
    }

    /// <summary>
    /// 四舍五入到整数。替代 Mathf.RoundToInt：后者内部是 (int)Math.Round((double)值)，
    /// 会在调用方编译单元里引入 float→double 转换指令。与 Unity 实现的差异只在
    /// 负数的半值边界（银行家舍入），本模组的权重与数量场景不受影响。
    /// </summary>
    internal static int RoundToInt(float value)
    {
        return value >= 0f ? (int)(value + 0.5f) : (int)(value - 0.5f);
    }

    /// <summary>
    /// 向上取整到整数。替代 Mathf.CeilToInt：后者内部是 (int)Math.Ceiling((double)值)，
    /// 存在与 RoundToInt 相同的内联转换隐患。仅接受非负值（本模组场景均为非负数）。
    /// </summary>
    internal static int CeilToInt(float value)
    {
        var truncated = (int)value;
        return truncated < value ? truncated + 1 : truncated;
    }
}
