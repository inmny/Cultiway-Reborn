using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Content.VillageMilitary;

namespace Cultiway.Content.VillageDuties;

/// <summary>居民唯一的日常人员类别，军籍与后备身份另外记录。</summary>
public enum VillageResidentKind
{
    /// <summary>普通现役士兵。</summary>
    Soldier,
    /// <summary>普通居民和劳动者。</summary>
    Worker,
    /// <summary>国王或村庄领主。</summary>
    Manager,
    /// <summary>拥有实际修行体系的居民。</summary>
    Special
}

/// <summary>村庄自己的工作月份，以及本村已应用的国家编制快照。</summary>
[Serializable]
public sealed class VillageDutyPolicy : KingdomMilitaryPolicy
{
    /// <summary>默认二至十一月工作，一月和十二月休息。</summary>
    public const int DefaultWorkMonths = 2046;
    /// <summary>低十二位分别表示一至十二月是否工作。</summary>
    public int WorkMonths = DefaultWorkMonths;
    /// <summary>手动指定的普通职业人数；没有记录的职业由原版按库存、人口和工作需求分配。</summary>
    public Dictionary<string, int> OccupationTargets = new();
    /// <summary>检查一个原版月份是否为工作月。</summary>
    /// <param name="month">一至十二的原版月份。</param>
    public bool IsWorkMonth(int month) => (WorkMonths & (1 << (month - 1))) != 0;

    /// <summary>创建供编辑使用的独立草稿。</summary>
    public new VillageDutyPolicy Copy()
    {
        var copy = (VillageDutyPolicy)MemberwiseClone();
        copy.OccupationTargets = new Dictionary<string, int>(OccupationTargets);
        return copy;
    }

    /// <summary>仅在用户提交和存档读入边界检查配置。</summary>
    public override bool IsValid() => (WorkMonths & ~4095) == 0 && base.IsValid() && OccupationTargets != null &&
        OccupationTargets.All(p => !string.IsNullOrEmpty(p.Key) && p.Key.Length <= 80 && p.Value >= 0) &&
        OccupationTargets.Values.Sum(n => (long)n) <= int.MaxValue;
}
