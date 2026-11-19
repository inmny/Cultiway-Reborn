using System;
using Newtonsoft.Json;

namespace Cultiway.Content.VillageMilitary;

/// <summary>同一国家各村共用的三层编制名称、人数要求和阵列设置。</summary>
[Serializable]
public class KingdomMilitaryPolicy
{
    /// <summary>第一层名称。</summary>
    public string SquadName = "";
    /// <summary>第二层名称。</summary>
    public string BattalionName = "";
    /// <summary>第三层名称。</summary>
    public string ArmyName = "";
    /// <summary>第一层指挥称谓。</summary>
    public string SquadLeaderTitle = "";
    /// <summary>第二层指挥称谓。</summary>
    public string BattalionLeaderTitle = "";
    /// <summary>原版军队统领称谓。</summary>
    public string ArmyLeaderTitle = "";
    /// <summary>每队总人数，包含指挥者。</summary>
    public int SquadSize = 10;
    /// <summary>每营最多下辖的小队数。</summary>
    public int SquadsPerBattalion = 10;
    /// <summary>每村唯一军队最多下辖的营数。</summary>
    public int BattalionsPerArmy = 10;
    /// <summary>矩形阵列每行成员数，零为按编制自动计算。</summary>
    public int FormationColumns;

    /// <summary>每村军队最多容纳的实际人数。</summary>
    [JsonIgnore] public long Capacity => (long)SquadSize * SquadsPerBattalion * BattalionsPerArmy;

    /// <summary>复制国家设置，不携带村庄的工作月份。</summary>
    public KingdomMilitaryPolicy Copy()
    {
        var copy = new KingdomMilitaryPolicy();
        copy.CopyOrganizationFrom(this);
        return copy;
    }

    /// <summary>只复制国家共用的字段，保留村庄自身的月历。</summary>
    /// <param name="source">需要采用的国家编制。</param>
    public void CopyOrganizationFrom(KingdomMilitaryPolicy source)
    {
        SquadName = source.SquadName; BattalionName = source.BattalionName; ArmyName = source.ArmyName;
        SquadLeaderTitle = source.SquadLeaderTitle; BattalionLeaderTitle = source.BattalionLeaderTitle;
        ArmyLeaderTitle = source.ArmyLeaderTitle; SquadSize = source.SquadSize;
        SquadsPerBattalion = source.SquadsPerBattalion; BattalionsPerArmy = source.BattalionsPerArmy;
        FormationColumns = source.FormationColumns;
    }

    /// <summary>只有人数或列数变化才需要等待安全时机重编。</summary>
    /// <param name="other">待应用的国家设置。</param>
    internal bool HasSameStructure(KingdomMilitaryPolicy other) => SquadSize == other.SquadSize &&
        SquadsPerBattalion == other.SquadsPerBattalion && BattalionsPerArmy == other.BattalionsPerArmy &&
        FormationColumns == other.FormationColumns;

    /// <summary>仅在用户提交和存档读入边界检查配置。</summary>
    public virtual bool IsValid()
    {
        if (SquadSize < 1 || SquadsPerBattalion < 1 || BattalionsPerArmy < 1 ||
            FormationColumns < 0 || FormationColumns > SquadSize) return false;
        // 原版人数使用整数；先限制前两项乘积，避免第三项乘入时溢出。
        long lower = (long)SquadSize * SquadsPerBattalion;
        return lower <= int.MaxValue && lower * BattalionsPerArmy <= int.MaxValue &&
               ValidName(SquadName) && ValidName(BattalionName) && ValidName(ArmyName) &&
               ValidName(SquadLeaderTitle) && ValidName(BattalionLeaderTitle) && ValidName(ArmyLeaderTitle);
    }

    /// <summary>空名称使用本地化默认名，其余名称采用单行短文本。</summary>
    /// <param name="value">待保存的名称。</param>
    private static bool ValidName(string value) => value != null && value.Length <= 40 &&
        !value.Contains("\n") && !value.Contains("\r");
}
