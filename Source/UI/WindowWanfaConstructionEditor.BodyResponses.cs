using System;
using System.Linq;
using Cultiway.Core.Skills;
using Cultiway.UI.Prefab;
using UnityEngine;

namespace Cultiway.UI;

/// <summary>一招内的真实结果各自选择收势姿态，复用现有草稿、菜单和撤销。</summary>
public sealed partial class WindowWanfaConstructionEditor
{
    /// <summary>每种结果只占一行，未设规则时允许保留原制作过程。</summary>
    private void BodyResponses()
    {
        BodyActionDefinition body = session.Draft.Body;
        int[] points = BodyConstructionEdits.RecoveryPoints(session.Draft);
        foreach (BodyActionResult result in Enum.GetValues(typeof(BodyActionResult)))
        {
            BodyResultResponse response = body.Responses?.FirstOrDefault(rule => rule.Result == result);
            WanfaEditorRow row = rows.GetNext();
            row.Setup(T("BodyResult." + result), response != null ? string.Empty : T(points.Length == 0 ? "NoRecoveryPose" : "BodyResponseHint"),
                T(response == null ? "AddBodyResponse" : "RemoveBodyResponse"), true,
                response != null ? () => ApplyConstructionEdit(() => BodyConstructionEdits.RemoveResponse(session.Draft, result)) :
                points.Length == 0 ? null : () => ApplyConstructionEdit(() =>
                {
                    int pose = points[points.Length - 1];
                    return BodyConstructionEdits.Response(session.Draft, result, pose,
                        Mathf.Min(.12f, (body.Keys[pose].At - body.ActiveUntil) * .5f));
                }), response == null ? UiIcons.Add : UiIcons.Delete);
            if (response == null) continue;
            Transform controls = row.UseInlineControls(260f);
            int current = Array.FindIndex(body.Keys, key => key.Id == response.RecoverFrom);
            var choose = UiElements.CreateButton(controls, "ResponsePose." + result,
                current < 0 ? T("MissingRecoveryPose") : RecoveryPoseName(current, body.Keys[current].At), 168f, 22f,
                () => menu.Show(T("RecoveryPose"), points.Select(index => new UiOptionMenuOption
                {
                    Label = RecoveryPoseName(index, body.Keys[index].At), Selected = current == index,
                    Select = () => ApplyConstructionEdit(() =>
                        BodyConstructionEdits.Response(session.Draft, result, index, response.Seconds))
                }).ToArray(), true));
            UiTooltip.Set(choose.gameObject, "Cultiway.Construction.RecoveryPose", "Cultiway.Construction.BodyResponseHint");
            FloatInput(controls, "ResponseSeconds", response.Seconds, value => response.Seconds = value, true, 84f);
        }
    }

    /// <summary>显示原动作中的姿态序号和时点，不要求玩家认识内部标记编号。</summary>
    /// <param name="index">原动作中的姿态序号。</param><param name="at">制作时点。</param>
    private static string RecoveryPoseName(int index, float at) => string.Format(T("RecoveryPoseAt"), index + 1, at.ToString("0.###"));
}
