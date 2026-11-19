using System;
using System.Linq;

namespace Cultiway.Core.Skills;

public static partial class BodyConstructionEdits
{
    /// <summary>同一份候选内改名，保留所有已有接触和收势引用。删除时由共同编译器拒绝悬空引用。</summary>
    /// <param name="body">本次独占候选中的身体。</param><param name="index">原姿态位置。</param><param name="name">新的标记名，空值移除标记。</param>
    internal static void RenamePose(BodyActionDefinition body, int index, string name)
    {
        BodyMotionKey key = body.Keys[index];
        string old = key.Id;
        key.Id = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        if (old == null || key.Id == null) return;
        foreach (BodyRelationDefinition relation in body.Construction?.Relations ?? Array.Empty<BodyRelationDefinition>())
        {
            if (relation.FromMarker == old) relation.FromMarker = key.Id;
            if (relation.UntilMarker == old) relation.UntilMarker = key.Id;
        }
        foreach (BodyResultResponse response in body.Responses ?? Array.Empty<BodyResultResponse>())
            if (response.RecoverFrom == old) response.RecoverFrom = key.Id;
    }

    /// <summary>给一条控制关系独立改目标表面，其他关系仍可继续引用原目标。</summary>
    /// <param name="construction">当前候选的部位和关系。</param><param name="relation">明确改动的控制关系。</param><param name="part">新的实际目标部位。</param>
    internal static void SetConnectionTarget(BodyConstructionDefinition construction, BodyRelationDefinition relation, string part)
    {
        BodyPartInput target = construction.Parts.Single(p => p.Id == relation.To);
        if (target.Part == part) return;
        if (!construction.Relations.Any(r => r != relation && (r.From == target.Id || r.To == target.Id)))
        {
            target.Part = part;
            return;
        }
        string id = relation.Id + ".target";
        for (int i = 1; construction.Parts.Any(p => p.Id == id); i++) id = relation.Id + ".target" + i;
        construction.Parts = construction.Parts.Append(new BodyPartInput { Id = id, Subject = target.Subject, Part = part }).ToArray();
        relation.To = id;
    }
}
