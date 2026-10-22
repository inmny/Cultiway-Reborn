using System;
using System.Linq;
using Cultiway.Core.Skills;
using Cultiway.UI.Prefab;
using UnityEngine;

namespace Cultiway.UI;

/// <summary>实际身体姿态及其显化投送，与其他形体共用草稿、编译、撤销和正式保存。</summary>
public sealed partial class WindowWanfaConstructionEditor
{
    /// <summary>编辑真实已有的身体动作，不凭空补出未适配的身体部位。</summary>
    private void BodyPage()
    {
        BodyActionDefinition body = session.Draft.Body;
        if (body == null) { Row("NoBody", T("NoBodyHint")); return; }
        Row("Effector", T(body.Effector == "hand.right" ? "RightHand" : body.Effector == "hand.left" ? "LeftHand" : "UnsupportedEffector"));
        Row("HeldForm", body.WeaponForm == null ? T("BareHand") : T("Form." + body.WeaponForm) + " / " + body.Grip);
        Number("BodyActiveFrom", body.ActiveFrom, value => body.ActiveFrom = value);
        Number("BodyActiveUntil", body.ActiveUntil, value => body.ActiveUntil = value);
        if (body.Hold != null)
        {
            Number("HoldAt", body.Hold.At, value => body.Hold.At = value);
            Number("HoldSeconds", body.Hold.Seconds, value => body.Hold.Seconds = value);
            Row("EffectiveBodyUntil", (body.ActiveUntil + body.Hold.Seconds).ToString("0.###"));
        }
        if (session.Draft.Root == null)
            ActionRow("ProjectBody", T("ProjectBodyHint"), () => ApplyConstructionEdit(() => BodyConstructionEdits.Project(session.Draft, new BodyProjection())));
        else Row("Manifestation", T("ManifestationHint"));
        Choice("BodyValues", T(new[] { "HandPoint", "TorsoPoint", "HandRotation" }[bodyHandle]), () => menu.Show(T("BodyValues"),
            Enumerable.Range(0, 3).Select(index => new UiOptionMenuOption
            {
                Label = T(new[] { "HandPoint", "TorsoPoint", "HandRotation" }[index]), Selected = bodyHandle == index,
                Select = () => { bodyHandle = index; Refresh(); }
            }).ToArray()));
        Row("BodyCoordinates", T(bodyHandle == 2 ? "RotationHint" : "BodyCoordinatesHint"));
        for (int i = 0; i < body.Keys.Length; i++)
        {
            int index = i; BodyMotionKey key = body.Keys[i];
            WanfaEditorRow row = rows.GetNext();
            row.Setup(string.Format(T("BodyKey"), i + 1), "", T("RemovePose"), true,
                i > 0 && body.Keys.Length > 2 ? () => Change(() => body.Keys = body.Keys.Where((_, at) => at != index).ToArray()) : null, UiIcons.Delete);
            Transform controls = row.UseInlineControls(260f);
            FloatInput(controls, "At", key.At, value => key.At = value, i != 0, 48f);
            float[] vector = bodyHandle == 0 ? key.Hand : bodyHandle == 1 ? key.Torso : key.Rotation;
            for (int axis = 0; axis < 3; axis++)
            {
                int coordinate = axis;
                FloatInput(controls, new[] { "X", "Y", "Z" }[axis], vector[axis], value => vector[coordinate] = value, true, 48f);
            }
            FloatInput(controls, "BodyTurn", key.Turn, value => key.Turn = value, true, 48f);
        }
        if (body.Keys.Length < 128) ActionRow("AddPose", T("AddPoseHint"), () => Change(() =>
        {
            BodyMotionKey last = body.Keys[body.Keys.Length - 1];
            var next = new BodyMotionKey
            {
                At = last.At + .1f, Hand = (float[])last.Hand.Clone(), Torso = (float[])last.Torso.Clone(),
                Rotation = (float[])last.Rotation.Clone(), Turn = last.Turn
            };
            body.Keys = body.Keys.Concat(new[] { next }).ToArray();
            session.Draft.Lifetime = Mathf.Max(session.Draft.Lifetime, next.At);
        }));
    }
}
