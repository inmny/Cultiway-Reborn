using System;
using System.Linq;
using Cultiway.Core.Skills;

namespace Cultiway.UI;

/// <summary>实际控制由动作推导，作者只通过原表单额外预留部位。</summary>
public sealed partial class WindowWanfaConstructionEditor
{
    /// <summary>显示共同编译的真实占用，并使用原开关编辑附加预留。</summary>
    private void BodyOccupancyRows()
    {
        Row("BodyRequiredParts", previewPlan?.Body == null ? T("BodyOccupancyHint") :
            string.Join(" / ", Enum.GetValues(typeof(BodyOccupancy)).Cast<BodyOccupancy>()
                .Where(part => part != BodyOccupancy.None && (previewPlan.Body.Occupancy & part) != 0)
                .Select(part => T("BodyPart." + part))
                .Concat(previewPlan.Body.Part.IsOrgan ? new[] { T("Organ") } : Array.Empty<string>())));
        Row("BodyReservation", T("BodyOccupancyHint"));
        foreach (BodyOccupancy part in Enum.GetValues(typeof(BodyOccupancy)))
        {
            if (part == BodyOccupancy.None) continue;
            Toggle("Reserve." + part, (session.Draft.Body.Occupancy & part) != 0,
                value => session.Draft.Body.Occupancy = value ? session.Draft.Body.Occupancy | part :
                    session.Draft.Body.Occupancy & ~part);
        }
    }
}
