using System;
using Cultiway.Core.Skills;
using Cultiway.UI.Prefab;

namespace Cultiway.UI;

public sealed partial class WindowWanfaConstructionEditor
{
    private bool editLeftFoot = true;

    /// <summary>普通双足取自当前动作的真实站姿，已有踢击足的路径保持原样。</summary>
    private void NativeFootRows(BodyActionDefinition body, CompiledBodyAction compiled)
    {
        if (compiled == null) return;
        Row("NativeFeet", T("NativeFeetHint"));
        BodyMotionToggle("NativeFeetEnabled", compiled.HasSupportMotion, enabled => EditBodyMotion(next =>
        {
            var left = compiled.Construction.LeftFoot;
            var right = compiled.Construction.RightFoot;
            foreach (BodyMotionKey key in next.Keys)
            {
                key.Support = enabled ? new BodySupportMotion
                {
                    LeftFoot = compiled.Part.IsLeg && compiled.Part.Left ? (float[])key.Foot.Clone() : new[] { left.x, left.y, left.z },
                    RightFoot = compiled.Part.IsLeg && !compiled.Part.Left ? (float[])key.Foot.Clone() : new[] { right.x, right.y, right.z }
                } : null;
            }
        }));
        if (!compiled.HasSupportMotion) return;

        bool leftSide = editLeftFoot;
        Choice("NativeFoot", T(leftSide ? "LeftFoot" : "RightFoot"), () => menu.Show(T("NativeFoot"), new[]
        {
            new UiOptionMenuOption { Label = T("LeftFoot"), Selected = leftSide, Select = () => { editLeftFoot = true; Refresh(); } },
            new UiOptionMenuOption { Label = T("RightFoot"), Selected = !leftSide, Select = () => { editLeftFoot = false; Refresh(); } }
        }));
        string[] axes = { "FootForward", "FootSide", "FootHeight" };
        for (int i = 0; i < body.Keys.Length; i++)
        {
            int index = i;
            BodyMotionKey key = body.Keys[i];
            bool middle = i > 0 && i < body.Keys.Length - 1;
            var row = rows.GetNext();
            row.Setup(string.Format(T("BodyKey"), i + 1), key.At.ToString("0.###"), "", true, null);
            var controls = row.UseInlineControls(260f);
            float[] point = leftSide ? key.Support.LeftFoot : key.Support.RightFoot;
            bool strikingFoot = compiled.Part.IsLeg && compiled.Part.Left == leftSide;
            for (int axis = 0; axis < axes.Length; axis++)
            {
                int coordinate = axis;
                FloatInput(controls, axes[axis], point[axis], value => EditBodyMotion(next =>
                    SetNativeFoot(next.Keys[index], leftSide, coordinate, value, strikingFoot)), middle, 48f, draftChange: false);
            }
            FloatInput(controls, "BodyRise", key.Support.Rise, value => EditBodyMotion(next => next.Keys[index].Support.Rise = value),
                middle, 48f, draftChange: false);
            FloatInput(controls, "BodyHeading", key.Heading, value => EditBodyMotion(next => next.Keys[index].Heading = value),
                i > 0, 48f, draftChange: false);
        }
    }

    /// <summary>发力足和同刻落脚是同一个位置，两处编辑入口共用此规则。</summary>
    private static void SetNativeFoot(BodyMotionKey key, bool left, int coordinate, float value, bool strikingFoot)
    {
        (left ? key.Support.LeftFoot : key.Support.RightFoot)[coordinate] = value;
        if (strikingFoot) key.Foot[coordinate] = value;
    }

    /// <summary>只为已有目标瞄准和身体回弹的部位提供对应控制。</summary>
    private void BodyExertionRows(CompiledBodyAction compiled)
    {
        if (compiled == null) return;
        var body = session.Draft.Body;
        if (compiled.Part.Contact is BodyContactPart.Hand or BodyContactPart.Foot)
            BodyMotionToggle("BodyMatchDistance", body.MatchDistance, value => EditBodyMotion(next => next.MatchDistance = value));
        if (!compiled.Part.IsArm) return;
        Row("BodyImpactRecoil", T("BodyImpactRecoilHint"));
        BodyMotionNumber("BodyRecoilDistance", body.ImpactRecoil, (next, value) => next.ImpactRecoil = value);
        if (body.ImpactRecoil > 0f)
            BodyMotionNumber("BodyRecoilSeconds", body.RecoilSeconds, (next, value) => next.RecoilSeconds = value);
    }

    /// <summary>验证完整动作，失败时不把中间状态写进草稿。</summary>
    private void EditBodyMotion(Action<BodyActionDefinition> edit) => ApplyConstructionEdit(() =>
    {
        var candidate = ConstructionEditing.Copy(session.Draft);
        edit(candidate.Body);
        access.Validate(candidate);
        return candidate;
    });

    private void BodyMotionToggle(string key, bool value, Action<bool> set)
    {
        var toggle = UiElements.CreateIconToggle(Row(key).UseInlineControls(32f), key, UiIcons.Options, value, 28f, 22f);
        toggle.onValueChanged.AddListener(next => set(next));
    }

    private void BodyMotionNumber(string key, float value, Action<BodyActionDefinition, float> set) =>
        FloatInput(Row(key).UseInlineControls(104f), key, value, next => EditBodyMotion(candidate => set(candidate, next)),
            true, 100f, draftChange: false);
}
