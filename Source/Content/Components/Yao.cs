using Cultiway.Abstract;
using Cultiway.Core.Libraries;
using Friflo.Engine.ECS;
using Friflo.Json.Fliox;

namespace Cultiway.Content.Components;

/// <summary>妖兽身体方案中一个器官的永久资料。</summary>
public struct YaoOrganRecord
{
    /// <summary>器官占用的身体位置。</summary>
    public string SlotId;

    /// <summary>器官编号。</summary>
    public string OrganId;

    /// <summary>器官等级。</summary>
    public int Rank;

    /// <summary>器官来源。</summary>
    public YaoOrganOrigin Origin;
}

/// <summary>妖兽永久器官的来源。</summary>
public enum YaoOrganOrigin : byte
{
    /// <summary>先天自带的器官。</summary>
    Innate,

    /// <summary>出生时直接遗传的器官。</summary>
    Inherited,

    /// <summary>潜伏血脉表达产生的器官。</summary>
    BloodlineExpressed,

    /// <summary>吞噬炼化所得的器官。</summary>
    Digested,

    /// <summary>大境界固血写入血脉的器官。</summary>
    Solidified,

    /// <summary>返祖显现的器官。</summary>
    Atavistic
}

/// <summary>妖兽的形态类别。</summary>
public enum YaoFormKind : byte
{
    /// <summary>真身：原始兽形或返祖后得到的主要兽形。</summary>
    TrueForm,

    /// <summary>人形：渡过化形劫后获得的固定人身。</summary>
    HumanForm,

    /// <summary>第二真身：只给鲲鹏等少数血脉使用。</summary>
    AlternateTrueForm
}

/// <summary>妖兽的一个永久形态方案。</summary>
public struct YaoFormRecord
{
    /// <summary>形态编号。</summary>
    public string FormId;

    /// <summary>形态类别。</summary>
    public YaoFormKind Kind;

    /// <summary>身体结构编号。</summary>
    public string BodyPlanId;

    /// <summary>固定形态编号。</summary>
    public string MorphId;

    /// <summary>带来源的永久器官资料。</summary>
    public YaoOrganRecord[] Organs;

    /// <summary>进入该形态所需的妖修境界。</summary>
    public int RequiredRealm;

    /// <summary>进入该形态所需的血脉编号；为空表示不限血脉。</summary>
    public string RequiredBloodlineId;

    /// <summary>再次切换前的等待时间（世界秒）。</summary>
    public float Cooldown;
}

/// <summary>
///     妖兽全部永久身体方案的唯一记录。器官来源、形态切换与返祖都先写到这里，
///     再由妖兽身体服务派生当前生效的共用身体。
/// </summary>
public struct YaoBody : IComponent
{
    /// <summary>可使用的全部形态。</summary>
    public YaoFormRecord[] Forms;

    /// <summary>当前正在使用的形态编号。</summary>
    public string ActiveFormId;

    /// <summary>上次切换形态的世界时间。</summary>
    public float LastSwitchAt;

    /// <summary>被封锁到何时的世界时间。</summary>
    public float LockedUntil;

    /// <summary>读取活动形态；记录损坏时返回假。</summary>
    public readonly bool TryGetActiveForm(out YaoFormRecord form)
    {
        return TryGetForm(ActiveFormId, out form);
    }

    /// <summary>按编号读取一个形态。</summary>
    public readonly bool TryGetForm(string formId, out YaoFormRecord form)
    {
        form = default;
        if (Forms == null || string.IsNullOrEmpty(formId)) return false;
        foreach (YaoFormRecord record in Forms)
        {
            if (string.Equals(record.FormId, formId, System.StringComparison.Ordinal))
            {
                form = record;
                return true;
            }
        }

        return false;
    }
}

/// <summary>妖兽基因中一个身体位点的显性与隐性等位。</summary>
public struct YaoGeneLocus
{
    /// <summary>显性等位器官编号；没有内容时为空。</summary>
    public string DominantOrganId;

    /// <summary>隐性等位器官编号；没有内容时为空。</summary>
    public string RecessiveOrganId;

    /// <summary>显性等位权重。</summary>
    public float DominantWeight;

    /// <summary>隐性等位权重。</summary>
    public float RecessiveWeight;

    /// <summary>位点是否已被固血锁定。</summary>
    public bool Locked;
}

/// <summary>
///     一只携带血脉动物的基因资料。潜伏凡兽只保存本组件；
///     启灵后代把它作为遗传潜力的唯一来源。
/// </summary>
public struct YaoGenome : IComponent
{
    /// <summary>基因资料版本。</summary>
    public int Version;

    /// <summary>主血脉编号；没有血脉时为空。</summary>
    public string PrimaryBloodlineId;

    /// <summary>主血脉纯度 0..1。</summary>
    public float PrimaryPurity;

    /// <summary>隐性血脉编号；没有时为空。</summary>
    public string HiddenBloodlineId;

    /// <summary>隐性血脉纯度 0..1。</summary>
    public float HiddenPurity;

    /// <summary>八个遗传位点，对应妖兽八类身体槽。</summary>
    public YaoGeneLocus[] Loci;

    /// <summary>基因代数；固血每次加一。</summary>
    public int GenomeGeneration;

    /// <summary>这只血脉长期使用的稳定种子。</summary>
    public int Seed;

    /// <summary>父母单位编号；没有记录时为 -1。</summary>
    public long ParentId1;

    /// <summary>父母单位编号；没有记录时为 -1。</summary>
    public long ParentId2;

    /// <summary>可见返祖次数。</summary>
    public int VisibleAtavismCount;

    /// <summary>最近一次固血原因的稳定键；没有时为空。</summary>
    public string LastSolidificationReason;

    /// <summary>固血失败抑制到何时的世界时间。</summary>
    public float SolidificationSuppressedUntil;

    /// <summary>确保位点数组存在且长度固定为八。</summary>
    public void EnsureLoci()
    {
        if (Loci != null && Loci.Length == YaoGenomeSettings.LocusCount) return;
        Loci = new YaoGeneLocus[YaoGenomeSettings.LocusCount];
    }

    /// <summary>复制完整基因资料；父母与孩子不共用同一位点数组。</summary>
    public YaoGenome DeepCopy()
    {
        var clone = this;
        clone.EnsureLoci();
        clone.Loci = (YaoGeneLocus[])Loci.Clone();
        return clone;
    }
}

/// <summary>基因资料的共享常量。</summary>
public static class YaoGenomeSettings
{
    /// <summary>基因位点数量，对应妖兽八类身体槽。</summary>
    public const int LocusCount = 8;
}

/// <summary>消化队列条目的阶段。</summary>
public enum YaoDigestionPhase : byte
{
    /// <summary>已领取但尚未开始消化。</summary>
    Queued,

    /// <summary>正在消化。</summary>
    Digesting,

    /// <summary>消化完成等待结算。</summary>
    Ready,

    /// <summary>已经结算为结果。</summary>
    Resolved,

    /// <summary>已按失败或放弃处理。</summary>
    Rejected
}

/// <summary>消化队列的一个条目。</summary>
public struct YaoDigestionEntry
{
    /// <summary>精华碎片编号。</summary>
    public string FragmentId;

    /// <summary>来源单位的编号。</summary>
    public long SourceActorId;

    /// <summary>来源死亡序号；与编号共同表示唯一一次死亡。</summary>
    public int SourceDeathSequence;

    /// <summary>样本强度。</summary>
    public float Strength;

    /// <summary>开始消化的世界时间。</summary>
    public float StartedAt;

    /// <summary>预计完成的世界时间。</summary>
    public float CompleteAt;

    /// <summary>领取时支付的妖力成本。</summary>
    public float Cost;

    /// <summary>当前阶段。</summary>
    public YaoDigestionPhase Phase;

    /// <summary>该条目是否为空位。</summary>
    public readonly bool IsEmpty => string.IsNullOrEmpty(FragmentId);
}

/// <summary>消化产生的静态器官候选。</summary>
public struct YaoOrganCandidate
{
    /// <summary>候选器官编号。</summary>
    public string OrganId;

    /// <summary>候选器官等级。</summary>
    public int Rank;

    /// <summary>准备替换的身体位置；为空表示新增。</summary>
    public string SlotId;

    /// <summary>候选评分。</summary>
    public float Score;

    /// <summary>候选出现原因的稳定键，供只读界面解释。</summary>
    public string Reason;

    /// <summary>该候选是否已被淘汰或使用。</summary>
    public bool Used;
}

/// <summary>一次炼化失败的有限记忆。</summary>
public struct YaoFailureMemory
{
    /// <summary>失败涉及的器官编号。</summary>
    public string OrganId;

    /// <summary>失败类型稳定键。</summary>
    public string FailureKind;

    /// <summary>记忆衰减到期的世界时间。</summary>
    public float Until;

    /// <summary>该记忆是否为空。</summary>
    public readonly bool IsEmpty => string.IsNullOrEmpty(OrganId);
}

/// <summary>妖兽的有上限消化队列；同时只有一格真正推进。</summary>
public struct YaoDigestion : IComponent
{
    /// <summary>队列容量。</summary>
    public const int QueueSize = 3;

    /// <summary>候选容量。</summary>
    public const int CandidateSize = 3;

    /// <summary>失败记忆容量。</summary>
    public const int MemorySize = 4;

    /// <summary>固定三条的消化队列。</summary>
    public YaoDigestionEntry[] Queue;

    /// <summary>最多三个器官候选。</summary>
    public YaoOrganCandidate[] Candidates;

    /// <summary>有限失败历史，随时间衰减。</summary>
    public YaoFailureMemory[] Memories;

    /// <summary>确保数组分配完成。</summary>
    public void EnsureInitialized()
    {
        Queue ??= new YaoDigestionEntry[QueueSize];
        Candidates ??= new YaoOrganCandidate[CandidateSize];
        Memories ??= new YaoFailureMemory[MemorySize];
    }

    /// <summary>统计仍在占用队列空间的条目数。</summary>
    public readonly int CountOccupied()
    {
        int count = 0;
        if (Queue == null) return 0;
        foreach (YaoDigestionEntry entry in Queue)
        {
            if (!entry.IsEmpty && entry.Phase is not (YaoDigestionPhase.Resolved or YaoDigestionPhase.Rejected))
                count++;
        }

        return count;
    }
}

/// <summary>妖丹路线的静态权重说明，由内容定义登记。</summary>
public struct YaoCoreTendency
{
    /// <summary>倾向的语义编号。</summary>
    public string SemanticId;

    /// <summary>倾向权重。</summary>
    public float Weight;
}

/// <summary>妖兽的独立核心；不是普通身体器官，不能被消化队列替换。</summary>
public struct YaoCore : IComponent
{
    /// <summary>品质 0..100。</summary>
    public float Quality;

    /// <summary>妖丹方向编号。</summary>
    public string CorePatternId;

    /// <summary>妖丹强度。</summary>
    public float Strength;

    /// <summary>妖丹稳定度 0..100。</summary>
    public float Stability;

    /// <summary>裂痕数量。</summary>
    public int Cracks;

    /// <summary>凝丹累计次数。</summary>
    public int CondensationCount;

    /// <summary>渡劫累计次数。</summary>
    public int TribulationCount;
}

/// <summary>正在进行的妖丹天劫；结束后立即移除。</summary>
public struct YaoTribulation : IComponent
{
    /// <summary>总波数。</summary>
    public int TotalWaves;

    /// <summary>当前波次。</summary>
    public int CurrentWave;

    /// <summary>开始的世界时间。</summary>
    public float StartedAt;

    /// <summary>超时界限的世界时间。</summary>
    public float ExpiresAt;

    /// <summary>下一次落雷的世界时间。</summary>
    public float NextStrikeAt;

    /// <summary>本波需要证明的承伤量。</summary>
    public float RequiredDamageEvidence;

    /// <summary>本波已经累积的承伤量。</summary>
    public float ReceivedDamageEvidence;

    /// <summary>妖丹完整度 0..1。</summary>
    public float CoreIntegrity;
}

/// <summary>正在进行的涅槃过程；结束时移除。</summary>
public struct Nirvana : IComponent
{
    /// <summary>开始的世界时间。</summary>
    public float StartedAt;

    /// <summary>必须结束的世界时间。</summary>
    public float ExpiresAt;

    /// <summary>涅槃体完整度 0..1。</summary>
    public float BodyIntegrity;
}

/// <summary>凡兽尚未启灵时已经积累的启灵总分。</summary>
public struct YaoAwakeningPotential : IComponent
{
    /// <summary>累计启灵总分：灵气接触按地块灵气浓度慢慢积累，生存与捕食一次加一大截。</summary>
    public float TotalScore;

    /// <summary>濒死幸存判定的冷却截止世界时间。</summary>
    public float NextSurvivalEligibleAt;
}

/// <summary>妖修体系的主体数据。原物种、境界、妖力、身体承载与保命次数都在这里。</summary>
public struct Yao : ICultisysComponent
{
    /// <summary>启灵前的原始物种编号。</summary>
    public string OriginalSpeciesId;

    /// <summary>启灵时间。</summary>
    public float AwakenedAt;

    /// <summary>这只妖兽长期使用的稳定种子。</summary>
    public int Seed;

    /// <summary>当前主境界。</summary>
    public int level;

    /// <summary>当前主境界内的小层次计数（例如淬血次数）。</summary>
    public int MinorLevel;

    /// <summary>当前妖力。</summary>
    public float yao_power;

    /// <summary>身体稳定度 0..100。</summary>
    public float BodyStability;

    /// <summary>妖躯阶段获得的额外结构容量。</summary>
    public int OrganCapacityBonus;

    /// <summary>异变承受力 0..1。</summary>
    public float MutationTolerance;

    /// <summary>恢复类过程的固定代价系数。</summary>
    public float RecoveryCost;

    /// <summary>凤凰涅槃剩余次数。</summary>
    public int PhoenixRevivalUses;

    /// <summary>九尾剩余尾命。</summary>
    public int NineTailLifeUses;

    /// <summary>凝丹准备的开始时间；为负表示没有准备。</summary>
    public float CorePreparationStartedAt;

    /// <summary>凝丹准备需要的精华量。</summary>
    public float CorePreparationRequiredEssence;

    /// <summary>凝丹准备需要的妖力量。</summary>
    public float CorePreparationRequiredYaoPower;

    /// <summary>凝丹准备需要的最低身体稳定度。</summary>
    public float CorePreparationRequiredStability;

    /// <summary>凝丹准备的妖丹方向；为空表示当前没有准备。</summary>
    public string CorePreparationPatternId;

    [Ignore]
    public BaseCultisysAsset Asset => Cultisyses.Yao;

    [Ignore]
    public int CurrLevel
    {
        get => level;
        set => level = value;
    }
}
