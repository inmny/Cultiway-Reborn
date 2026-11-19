using System.IO;
using Cultiway.Core.Libraries;
using Newtonsoft.Json;

namespace Cultiway.Core.Skills;

/// <summary>按真实生命和当前持有的已登记状态选择结果，再按实际生死追加一次作用。</summary>
public sealed class ConstructionConditionalContact
{
    /// <summary>目标生命比例不高于此值时选择结果；省略时始终选择结果。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public float? MaximumHealthRatio;

    /// <summary>实际接触人物必须仍持有的共享状态，省略时不检查。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string RequiredStatus;

    /// <summary>条件成立时的普通作用。</summary>
    public ConstructionContactOutcome Outcome;

    /// <summary>条件不成立时的普通作用，省略表示无作用。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionContactOutcome Otherwise;

    /// <summary>本条结果的伤害真正杀死接触人物后的普通作用。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionContactOutcome OnKill;

    /// <summary>本条结果提交了目标伤害，但接触人物仍存活时的普通作用。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionContactOutcome OnSurvive;
}

/// <summary>独立复制的有限分支，真实命中时才读取人物生命及结算结果。</summary>
public sealed class CompiledConstructionConditionalContact
{
    /// <summary>可选的真实目标生命比例上限。</summary>
    public float? MaximumHealthRatio { get; }

    /// <summary>必须由接触人物有效持有的已登记状态。</summary>
    public StatusEffectAsset RequiredStatus { get; }

    /// <summary>条件成立时的结果。</summary>
    public CompiledConstructionContactOutcome Outcome { get; }

    /// <summary>条件不成立时的结果。</summary>
    public CompiledConstructionContactOutcome Otherwise { get; }

    /// <summary>本条伤害真正致死后的结果。</summary>
    public CompiledConstructionContactOutcome OnKill { get; }

    /// <summary>本条伤害未能致死后的结果。</summary>
    public CompiledConstructionContactOutcome OnSurvive { get; }

    /// <summary>是否有可能产生普通作用。</summary>
    public bool HasEffect => Outcome.HasEffect || Otherwise?.HasEffect == true;

    /// <summary>在资料入口检查阈值，并复制普通结果；不采样人物或进行预测结算。</summary>
    /// <param name="source">可保存的有限分支。</param><param name="path">错误资料位置。</param>
    internal CompiledConstructionConditionalContact(ConstructionConditionalContact source, string path)
    {
        if (source == null || source.Outcome == null || source.MaximumHealthRatio.HasValue &&
            (float.IsNaN(source.MaximumHealthRatio.Value) || float.IsInfinity(source.MaximumHealthRatio.Value) || source.MaximumHealthRatio < 0f || source.MaximumHealthRatio > 1f) ||
            !source.MaximumHealthRatio.HasValue && source.RequiredStatus == null && source.Otherwise != null)
            throw new InvalidDataException(path + "：条件作用需要明确结果，生命阈值为零至一的有限值，备选结果需要条件");
        MaximumHealthRatio = source.MaximumHealthRatio;
        if (source.RequiredStatus != null && (string.IsNullOrWhiteSpace(source.RequiredStatus) ||
                                             (RequiredStatus = ModClass.L.StatusEffectLibrary.get(source.RequiredStatus)) == null))
            throw ConstructionReferences.Error(path + "/RequiredStatus", "需要已登记的共享状态编号", resourceKind: "Status", resourceId: source.RequiredStatus);
        Outcome = new CompiledConstructionContactOutcome(source.Outcome, path + "/Outcome");
        if (source.Otherwise != null) Otherwise = new CompiledConstructionContactOutcome(source.Otherwise, path + "/Otherwise");
        if (source.OnKill != null) OnKill = new CompiledConstructionContactOutcome(source.OnKill, path + "/OnKill");
        if (source.OnSurvive != null) OnSurvive = new CompiledConstructionContactOutcome(source.OnSurvive, path + "/OnSurvive");
    }
}
