using System.Collections.Generic;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;

namespace Cultiway.Content.Skills;

public sealed partial class WorldMaterialService
{
    /// <summary>只给正在实际改速的材料扣上印片，附着数量和运动读同一过程进度。</summary>
    private void AppendTimeImprints(List<FormVisualInstance> output, WorldMaterialRecord row)
    {
        if (row.Recovering || row.Material.Volume <= 0) return;
        var process = row.Material.Erosion;
        float rate = process.CurrentRate;
        if (rate == 1f) return;

        var bounds = BoundsOf(row);
        float radius = Mathf.Max(.33f, Mathf.Min(2f, Mathf.Max(bounds.extents.x, bounds.extents.y)));
        float size = Mathf.Clamp(radius * .70f, .42f, 1.14f);
        float progress = (float)process.Progress;
        int count = 1 + Mathf.FloorToInt(progress * 3f);
        float opening = rate == 0f ? 0f : rate < 1f ? .24f : .65f + .35f * Mathf.Clamp01((rate - 1f) / 2f);
        FormResource stamp = FormResources.Load("time_fold_imprint");
        for (int layer = 0; layer < count; layer++)
        {
            float angle = -.75f + progress * 2.1f + layer * 2.0943951f;
            var side = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            // 外沿开合表现速率；实际推进停下时，印片也停在同一位置。
            Vector3 position = bounds.center + side * radius * (.72f + opening * .30f);
            position.z += bounds.extents.z * .64f + size * (.25f + layer * .12f);
            var orientation = Quaternion.AngleAxis(angle * Mathf.Rad2Deg - 90f, Vector3.forward) *
                              Quaternion.AngleAxis(16f + opening * 29f, Vector3.right);
            output.Add(new FormVisualInstance(stamp, Matrix4x4.TRS(position, orientation, Vector3.one * size), terrain: terrain));
        }
    }
}
