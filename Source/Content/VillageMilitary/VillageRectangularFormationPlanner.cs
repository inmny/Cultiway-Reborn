using System;
using System.Linq;
using Cultiway.Content.VillageDuties;
using UnityEngine;

namespace Cultiway.Content.VillageMilitary;

/// <summary>指挥者在前，成员按稳定槽位组成矩形；上层按下级完整矩形留出间隔。</summary>
public static class VillageRectangularFormationPlanner
{
    /// <summary>横纵格点间隔均为两个地图地块。</summary>
    public const int Spacing = 2;

    /// <summary>编制中的普通成员矩形列数，不随日常伤亡变化。</summary>
    /// <param name="policy">当前小队编制。</param>
    public static int Columns(VillageDutyPolicy policy) => policy.FormationColumns > 0
        ? policy.FormationColumns : Math.Max(1, (int)Math.Ceiling(Math.Sqrt(Math.Max(1, policy.SquadSize - 1))));

    /// <summary>普通成员点位，纵向负值表示指挥者后方。</summary>
    /// <param name="policy">小队人数和列数。</param>
    /// <param name="slot">人物原有的稳定槽位。</param>
    /// <param name="leaderSlot">当前领队所占槽位。</param>
    public static Vector2Int MemberOffset(VillageDutyPolicy policy, int slot, int leaderSlot)
    {
        int index = slot < leaderSlot ? slot : slot - 1;
        int columns = Columns(policy);
        return new Vector2Int((2 * (index % columns) - columns + 1), -Spacing * (index / columns + 1));
    }

    /// <summary>返回一支小队完整的矩形占位尺寸，包含指挥点和边缘间隔。</summary>
    /// <param name="policy">用于计算完整占位的编制。</param>
    public static Vector2Int SquadSize(VillageDutyPolicy policy)
    {
        int columns = Columns(policy);
        int rows = (int)Math.Ceiling(Math.Max(0, policy.SquadSize - 1) / (double)columns);
        return new Vector2Int(columns * Spacing + Spacing, (rows + 1) * Spacing + Spacing);
    }

    /// <summary>计算一个下级矩形在上级指挥点后方的位置。</summary>
    /// <param name="index">下级在本层的顺序。</param>
    /// <param name="capacity">本层实际矩形数。</param>
    /// <param name="child">单个下级的完整宽深。</param>
    private static Vector2Int BlockOffset(int index, int capacity, Vector2Int child)
    {
        int columns = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(capacity)));
        return new Vector2Int((2 * (index % columns) - columns + 1) * (child.x / 2 + Spacing),
            -(index / columns + 1) * (child.y + Spacing));
    }

    /// <summary>整军行动时投影到最高实际指挥者，兼任者只获得一个最高层指挥点。</summary>
    /// <param name="data">全军编制和点位缓存。</param>
    /// <param name="squad">人物所属小队。</param>
    /// <param name="actor">需要计算站位的人物。</param>
    /// <param name="armyLeader">本次整军实际统领编号。</param>
    public static Vector2Int ArmyOffset(VillageMilitaryData data, VillageSquad squad, Actor actor, long armyLeader)
    {
        if (data.FormationDirty || data.FormationLeader != armyLeader) RebuildArmyOffsets(data, armyLeader);
        return data.FormationOffsets.TryGetValue(actor.id, out var offset) ? offset : Vector2Int.zero;
    }

    /// <summary>按实际存在的营队计算完整矩形，兼任指挥者的位置只由最高职位决定。</summary>
    /// <param name="data">需要重新计算点位的编制。</param>
    /// <param name="armyLeader">本次使用的统领编号。</param>
    private static void RebuildArmyOffsets(VillageMilitaryData data, long armyLeader)
    {
        data.FormationOffsets.Clear(); data.FormationLeader = armyLeader; data.FormationDirty = false;
        if (data.Squads.Count == 0) return;
        var policy = data.Policy;
        var squadSize = SquadSize(policy);
        var groups = data.Squads.GroupBy(s => s.BattalionId).ToDictionary(g => g.Key, g => g.ToList());
        int largestBattalion = groups.Values.Max(s => s.Count);
        int battalionColumns = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(largestBattalion)));
        int battalionRows = (int)Math.Ceiling(largestBattalion / (double)battalionColumns);
        var battalionSize = new Vector2Int(battalionColumns * (squadSize.x + Spacing),
            battalionRows * (squadSize.y + Spacing) + squadSize.y + Spacing);
        for (int b = 0; b < data.Battalions.Count; b++)
        {
            var battalion = data.Battalions[b];
            if (!groups.TryGetValue(battalion.Id, out var siblings)) continue;
            var battalionOffset = battalion.Commander == armyLeader ? Vector2Int.zero : BlockOffset(b, data.Battalions.Count, battalionSize);
            for (int s = 0; s < siblings.Count; s++)
            {
                var squad = siblings[s];
                var squadOffset = battalionOffset + (squad.Commander == battalion.Commander ? Vector2Int.zero : BlockOffset(s, siblings.Count, squadSize));
                int leaderSlot = squad.Members.TryGetValue(squad.Commander, out int value) ? value : 0;
                foreach (var member in squad.Members)
                    data.FormationOffsets[member.Key] = member.Key == armyLeader ? Vector2Int.zero :
                        member.Key == battalion.Commander ? battalionOffset : member.Key == squad.Commander ? squadOffset :
                        squadOffset + MemberOffset(policy, member.Value, leaderSlot);
            }
        }
    }

    /// <summary>把局部方阵旋转到四个正交方向，保持点位为整数格。</summary>
    /// <param name="offset">默认朝向下的相对点位。</param>
    /// <param name="heading">当前行进方向。</param>
    public static Vector2Int Orient(Vector2Int offset, Vector2 heading)
    {
        if (Math.Abs(heading.x) > Math.Abs(heading.y))
            return heading.x >= 0 ? new Vector2Int(-offset.y, offset.x) : new Vector2Int(offset.y, -offset.x);
        return heading.y >= 0 ? offset : -offset;
    }
}
