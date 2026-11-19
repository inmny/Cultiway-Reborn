using System;
using System.Collections.Generic;
using Cultiway.Content.VillageDuties;
using Newtonsoft.Json;
using UnityEngine;

namespace Cultiway.Content.VillageMilitary;

/// <summary>村庄永久编制记录，顶层成员和统领仍由原版 Army 保存。</summary>
[Serializable]
public sealed class VillageMilitaryData
{
    /// <summary>本村月份和已应用的国家编制快照，保留旧槽位直到安全重编。</summary>
    public VillageDutyPolicy Policy = new();
    /// <summary>交战或运输结束后提交的编制草稿。</summary>
    public VillageDutyPolicy PendingPolicy;
    /// <summary>本村已同步的国家设置来源，换国家或修改设置后才重新排队。</summary>
    [JsonIgnore] internal KingdomMilitaryPolicy PolicySource;
    /// <summary>展示时采用最新国家称呼，人数与实际槽位仍以已应用快照为准。</summary>
    [JsonIgnore] public KingdomMilitaryPolicy OrganizationNames => PendingPolicy ?? Policy;
    /// <summary>稳定的下级编号分配器。</summary>
    public int NextId = 1;
    /// <summary>现有营记录。</summary>
    public List<VillageBattalion> Battalions = new();
    /// <summary>现有小队记录。</summary>
    public List<VillageSquad> Squads = new();
    /// <summary>平时没有军籍的补缺候选人物编号。</summary>
    public HashSet<long> Reserves = new();
    /// <summary>普通工人自动或手动分配的职业归属，休息月保留。</summary>
    public Dictionary<long, string> Occupations = new();
    /// <summary>重编需要在当前交战或运输结束后继续处理。</summary>
    [JsonIgnore] public bool Reorganize;
    /// <summary>真实成员发生变化后补任空缺职位。</summary>
    [JsonIgnore] public bool AppointmentsDirty = true;
    /// <summary>下一轮人员与勤务检查。</summary>
    [JsonIgnore] public double NextUpdate;
    /// <summary>轮流检查补缺候选，避免不符合征兵条件的人堵住名单。</summary>
    [JsonIgnore] public int RecruitCursor;
    /// <summary>本轮允许进入原版征兵入口的人物。</summary>
    [JsonIgnore] public long RecruitCandidate;
    /// <summary>本世界已完成原版岗位计算，避免读档初始零额度导致误退役。</summary>
    [JsonIgnore] public bool ArmyQuotaReady;
    /// <summary>运行期每名成员对应的小队，避免逐队扫描。</summary>
    [JsonIgnore] public readonly Dictionary<long, VillageSquad> Memberships = new();
    /// <summary>成员或任命变化后才重算上级矩形。</summary>
    [JsonIgnore] public bool FormationDirty = true;
    /// <summary>整军方格点的当前统领。</summary>
    [JsonIgnore] public long FormationLeader;
    /// <summary>预先计算的每人整军点位，不在心跳中重复遍历全部营队。</summary>
    [JsonIgnore] public readonly Dictionary<long, Vector2Int> FormationOffsets = new();
    /// <summary>整军到达目标后保留最后朝向，避免原地翻转队形。</summary>
    [JsonIgnore] public Vector2 ArmyHeading = Vector2.up;
    /// <summary>明确军令或原版出征目标发生变化时递增。</summary>
    [JsonIgnore] public long OrderRevision;
    /// <summary>上一轮军令目标，用于识别仍在出征但目标已经改变的情况。</summary>
    [JsonIgnore] public int MilitaryTarget = -1;
}

/// <summary>唯一 Army 下的一层营。</summary>
[Serializable]
public sealed class VillageBattalion
{
    /// <summary>稳定编号。</summary>
    public int Id;
    /// <summary>玩家自定义实例名称，空值使用层级名称和编号。</summary>
    public string Name = "";
    /// <summary>正式营长，选自下辖队长。</summary>
    public long Commander;
}

/// <summary>保存稳定人员槽位的小队，不另造原版 Army。</summary>
[Serializable]
public sealed class VillageSquad
{
    /// <summary>稳定编号。</summary>
    public int Id;
    /// <summary>所属营编号。</summary>
    public int BattalionId;
    /// <summary>玩家自定义实例名称。</summary>
    public string Name = "";
    /// <summary>队长编号。</summary>
    public long Commander;
    /// <summary>人物在队伍中的稳定槽位，死亡后由补员填空。</summary>
    public Dictionary<long, int> Members = new();
    /// <summary>当前协调行动编号，不进入存档。</summary>
    [JsonIgnore] public long ActivityId;
    /// <summary>下次勤务变化时间。</summary>
    [JsonIgnore] public double NextRoutine;
    /// <summary>操练、巡逻、驻守的轮换序号。</summary>
    [JsonIgnore] public int Routine;
    /// <summary>当前任务的实际带队者，可能是日常代理。</summary>
    [JsonIgnore] public long DutyLeader;
    /// <summary>当前行动是否为明确军令。</summary>
    [JsonIgnore] public bool Ordered;
    /// <summary>日常目标格或军事目标格。</summary>
    [JsonIgnore] public int TargetTile = -1;
    /// <summary>小队移动停止后仍保持最后行进方向。</summary>
    [JsonIgnore] public Vector2 Heading = Vector2.up;
    /// <summary>本次行动领取的军令版本。</summary>
    [JsonIgnore] public long OrderRevision;
}
