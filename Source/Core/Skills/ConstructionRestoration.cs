using System;
using System.IO;
using Cultiway.Core.Libraries;
using Newtonsoft.Json;

namespace Cultiway.Core.Skills;

/// <summary>真实接触恢复的对象；默认接触人物，控制人物指本部位当前实际施法者。</summary>
public enum ConstructionRestorationTarget
{
    /// <summary>本次真正接触的人物。</summary>
    Contact = 0,
    /// <summary>本部位当前有效的控制人物，反射后随控制权交接。</summary>
    Controller = 1
}

/// <summary>按实际状态编号与层数解除共享状态。</summary>
public sealed class ConstructionStatusRemoval
{
    /// <summary>共享状态库中已登记的编号。</summary>
    public string Id;
    /// <summary>同一接触最多解除的实际层数。</summary>
    public int Maximum = 1;
    /// <summary>只解除当前施术者施加的层数。</summary>
    public bool OwnSourceOnly;
}

/// <summary>同一真实接触可以附带的恢复和净化，不自行扩大作用区。</summary>
public sealed class ConstructionRestoration
{
    /// <summary>恢复的实际人物，省略时仍为接触目标。</summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public ConstructionRestorationTarget Target;
    /// <summary>按接触时本体余力折算的即时生命比例，先取整再与固定恢复量相加。</summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public float HealthPerForce;
    /// <summary>接触时立即恢复的生命量。</summary>
    public float Health;
    /// <summary>全局同类最强的每秒恢复量；零表示不施加恢复状态。</summary>
    public float HealthPerSecond;
    /// <summary>恢复状态离开作用面后最多保留的世界秒数。</summary>
    public float Duration = 1.25f;
    /// <summary>按共享负状态优先级最多清除的数量。</summary>
    public int CleanseCount;
    /// <summary>先于通用净化执行的指定解除，省略时没有该作用。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionStatusRemoval RemoveStatus;
}

/// <summary>在创建、编辑及保存读回边界检查并复制的恢复资料。</summary>
public sealed class CompiledConstructionRestoration
{
    /// <summary>本次接触恢复的实际人物。</summary>
    public ConstructionRestorationTarget Target { get; }
    /// <summary>按接触余力折算并取整的即时恢复比例。</summary>
    public float HealthPerForce { get; }
    /// <summary>实际即时恢复量。</summary>
    public float Health { get; }
    /// <summary>实际每秒恢复量。</summary>
    public float HealthPerSecond { get; }
    /// <summary>状态最多保留的世界秒数。</summary>
    public float Duration { get; }
    /// <summary>实际净化数量。</summary>
    public int CleanseCount { get; }
    /// <summary>要解除的已登记状态，省略时不查找。</summary>
    public StatusEffectAsset RemovedStatus { get; }
    /// <summary>每次实际接触可解除的最多层数，未声明时为零。</summary>
    public int RemovalMaximum { get; }
    /// <summary>只解除当前施术者的层数。</summary>
    public bool RemovalOwnSourceOnly { get; }
    /// <summary>是否声明了至少一种人物作用。</summary>
    public bool HasEffect => Health > 0f || HealthPerForce > 0f || HealthPerSecond > 0f || CleanseCount > 0 || RemovalMaximum > 0;

    /// <summary>恢复量必须有限，持续恢复需要正时长，不替用户修正非法资料。</summary>
    /// <param name="source">可保存资料。</param><param name="path">错误位置。</param>
    internal CompiledConstructionRestoration(ConstructionRestoration source, string path)
    {
        if (!Enum.IsDefined(typeof(ConstructionRestorationTarget), source.Target))
            throw new InvalidDataException(path + "：恢复对象必须为实际接触人物或当前控制人物");
        if (!Amount(source.Health) || !Amount(source.HealthPerForce) || !Amount(source.HealthPerSecond) ||
            float.IsNaN(source.Duration) || float.IsInfinity(source.Duration) || source.Duration < 0f ||
            source.Duration > 3600f || source.HealthPerSecond > 0f && source.Duration <= 0f ||
            source.CleanseCount < 0 || source.CleanseCount > 1024)
            throw new InvalidDataException(path + "：恢复量需要0至一百万的有限数值，持续恢复需0至3600内的正时长，净化数量需0至1024");
        Target = source.Target;
        HealthPerForce = source.HealthPerForce;
        Health = source.Health;
        HealthPerSecond = source.HealthPerSecond;
        Duration = source.Duration;
        CleanseCount = source.CleanseCount;
        if (source.RemoveStatus is not { } removal) return;
        if (string.IsNullOrWhiteSpace(removal.Id) ||
            (RemovedStatus = ModClass.L.StatusEffectLibrary.get(removal.Id)) == null)
            throw new InvalidDataException(path + "/RemoveStatus：需要已登记的共享状态编号");
        if (removal.Maximum < 1 || removal.Maximum > 1024)
            throw new InvalidDataException(path + "/RemoveStatus：解除层数需要在1至1024之间");
        RemovalMaximum = removal.Maximum;
        RemovalOwnSourceOnly = removal.OwnSourceOnly;
    }

    /// <summary>只在编译边界检查恢复量。</summary>
    /// <param name="value">实际资料数值。</param>
    private static bool Amount(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f && value <= 1000000f;
}
