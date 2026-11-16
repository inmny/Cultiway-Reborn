using System.IO;

namespace Cultiway.Core.Skills;

/// <summary>真实接触后交换当前控制人物和接触人物的地面位置。</summary>
public sealed class ConstructionExchange
{
    /// <summary>每次真实命中独立触发的概率；零值不改变位置。</summary>
    public float Chance = 1f;

    /// <summary>双方换位后停止旧行动并等待的世界秒数。</summary>
    public float WaitSeconds = .2f;
}

/// <summary>独立复制的接触换位资料，编译和预测不抽取概率。</summary>
public sealed class CompiledConstructionExchange
{
    /// <summary>已经确定的触发概率。</summary>
    public float Chance { get; }

    /// <summary>已经确定的换位后停步时间。</summary>
    public float WaitSeconds { get; }

    /// <summary>创建、编辑和恢复边界共用一次检查。</summary>
    /// <param name="source">可编辑资料。</param><param name="path">错误资料位置。</param>
    internal CompiledConstructionExchange(ConstructionExchange source, string path)
    {
        Chance = source.Chance; WaitSeconds = source.WaitSeconds;
        if (float.IsNaN(Chance) || float.IsInfinity(Chance) || Chance < 0f || Chance > 1f ||
            float.IsNaN(WaitSeconds) || float.IsInfinity(WaitSeconds) || WaitSeconds < 0f || WaitSeconds > 3600f)
            throw new InvalidDataException(path + "：换位概率须为0至1，停步须为0至3600世界秒的有限值");
    }
}
