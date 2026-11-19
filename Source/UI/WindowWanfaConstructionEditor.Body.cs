using System;
using System.Linq;
using Cultiway.Core.Skills;
using Cultiway.Core.Visuals.Forms;
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
        if (body == null)
        {
            Row("NoBody", T("NoBodyHint"));
            return;
        }

        string effector = previewPlan?.Body?.Effector ?? body.Construction?.Parts.FirstOrDefault(part => part.Id == body.Effector)?.Part ?? body.Effector;
        bool rotating = previewPlan?.Body?.Part.HasPartRotation == true || body.Keys.Any(key => key.PartRotation != null);
        Row("Effector",
            rotating
                ? ActualPartName(effector)
                : T(previewPlan?.Body?.Part.Label ?? (effector == "hand.right" ? "RightHand" :
                    effector == "hand.left" ? "LeftHand" : "UnsupportedEffector")));
        Row("HeldForm",
            body.WeaponForm == null
                ? T(rotating ? "NaturalPart" : "BareHand")
                : T("Form." + body.WeaponForm) + " / " + (previewPlan?.Body?.Grip ?? body.Grip));
        BodyOccupancyRows();
        if (rotating) ActualPartRows(effector);
        MartialBindingRows(previewPlan?.Body);
        Number("BodyActiveFrom", body.ActiveFrom, value => body.ActiveFrom = value);
        Number("BodyActiveUntil", body.ActiveUntil, value => body.ActiveUntil = value);
        if (body.Hold != null)
        {
            Number("HoldAt", body.Hold.At, value => body.Hold.At = value);
            Number("HoldSeconds", body.Hold.Seconds, value => body.Hold.Seconds = value);
            Row("EffectiveBodyUntil", (body.ActiveUntil + body.Hold.Seconds).ToString("0.###"));
        }

        BodyExertionRows(previewPlan?.Body);
        BodyStages();
        BodyResponses();
        BodyRelations();

        if (session.Draft.Root == null)
        {
            ActionRow("ProjectBody", T("ProjectBodyHint"),
                ProjectBody);
            ActionRow("ExtendBody", T("ExtendBodyHint"), ExtendBody);
            ActionRow("CarryBody", T("CarryBodyHint"), CarryBody);
            ActionRow("PropagateBody", T("PropagationHint"), PropagateBody);
        }
        else Row("Manifestation", T(session.Draft.Binding?.Anchor == AbilityAnchor.Carrier ? "CarrierHint" :
            session.Draft.Root.BodyWindow ? "ExtensionHint" : "ManifestationHint"));
        bool leg = previewPlan?.Body?.Part.IsLeg == true || body.Keys.Any(key => key.Foot != null);
        bool head = previewPlan?.Body?.Part.IsHead == true || body.Keys.Any(key => key.HeadRotation != null);
        string[] handles = rotating ? new[] { "PartRotation", "NamedFeet", "BodyFootwork", "BodyPoseMarkers" } :
            head ? new[] { "HeadRotation", "TorsoPoint", "NativeFeet", "BodyFootwork", "BodyPoseMarkers" } :
            leg ? new[] { "FootPoint", "TorsoPoint", "NativeFeet", "BodyFootwork", "BodyPoseMarkers" } :
                new[] { "HandPoint", "TorsoPoint", "HandRotation", "NativeFeet", "BodyFootwork", "BodyPoseMarkers" };
        bodyHandle = Math.Min(bodyHandle, handles.Length - 1);
        Choice("BodyValues", T(handles[bodyHandle]), () =>
            menu.Show(T("BodyValues"),
                Enumerable.Range(0, handles.Length).Select(index => new UiOptionMenuOption
                {
                    Label = T(handles[index]),
                    Selected = bodyHandle == index,
                    Select = () =>
                    {
                        bodyHandle = index;
                        Refresh();
                    }
                }).ToArray()));
        if (handles[bodyHandle] == "BodyFootwork")
        {
            BodyFootworkRows(body);
            return;
        }
        if (handles[bodyHandle] == "NativeFeet")
        {
            NativeFootRows(body, previewPlan?.Body);
            return;
        }
        if (handles[bodyHandle] == "BodyPoseMarkers")
        {
            PoseMarkerRows(body);
            return;
        }
        bool feet = rotating && handles[bodyHandle] == "NamedFeet";
        if (feet) NamedFootRows(body);
        Row("BodyCoordinates",
            T(feet ? "NamedFeetHint" : rotating ? "PartRotationHint" :
                head && bodyHandle == 0 ? "HeadRotationHint" :
                bodyHandle == 2 ? "RotationHint" : "BodyCoordinatesHint"));
        for (int i = 0; i < body.Keys.Length; i++)
        {
            int index = i;
            BodyMotionKey key = body.Keys[i];
            WanfaEditorRow row = rows.GetNext();
            row.Setup(string.Format(T("BodyKey"), i + 1), "", T("RemovePose"), true,
                i > 0 && body.Keys.Length > 2
                    ? () => EditBodyMotion(next => next.Keys = next.Keys.Where((_, at) => at != index).ToArray())
                    : null, UiIcons.Delete);
            Transform controls = row.UseInlineControls(260f);
            FloatInput(controls, "At", key.At, value => EditBodyMotion(next => next.Keys[index].At = value), i != 0, 48f, draftChange: false);
            if (feet)
            {
                if (bodyFoot != null && key.Support?.Parts?.TryGetValue(bodyFoot, out float[] foot) == true)
                {
                    for (int axis = 0; axis < 3; axis++)
                    {
                        int coordinate = axis;
                        FloatInput(controls, new[] { "FootForward", "FootSide", "FootHeight" }[axis], foot[axis], value => foot[coordinate] = value, true, 48f);
                    }
                    FloatInput(controls, "BodyRise", key.Support.Rise, value => key.Support.Rise = value, true, 48f);
                }
                continue;
            }
            if (rotating)
            {
                for (int axis = 0; axis < 3; axis++)
                {
                    int coordinate = axis;
                    FloatInput(controls, "PartRotation" + new[] { "X", "Y", "Z" }[axis], key.PartRotation[axis],
                        value => key.PartRotation[coordinate] = value, true, 64f);
                }
                continue;
            }

            float[] vector = bodyHandle == 0
                ? head ? key.HeadRotation ?? key.Hand : leg ? key.Foot ?? key.Hand : key.Hand
                : bodyHandle == 1
                    ? key.Torso
                    : key.Rotation;
            for (int axis = 0; axis < 3; axis++)
            {
                int coordinate = axis;
                string field = new[] { "X", "Y", "Z" }[axis];
                if (leg && bodyHandle == 0 && key.Support != null)
                {
                    bool left = effector?.EndsWith(".left", StringComparison.Ordinal) == true;
                    FloatInput(controls, field, vector[axis], value => EditBodyMotion(next =>
                            SetNativeFoot(next.Keys[index], left, coordinate, value, true)), true, 48f, draftChange: false);
                    continue;
                }
                FloatInput(controls, field, vector[axis], value =>
                    {
                        if (head && bodyHandle == 0)
                        {
                            key.HeadRotation ??= (float[])vector.Clone();
                            key.HeadRotation[coordinate] = value;
                        }
                        else if (leg && bodyHandle == 0)
                        {
                            key.Foot ??= (float[])vector.Clone();
                            key.Foot[coordinate] = value;
                        }
                        else vector[coordinate] = value;
                    },
                    true, 48f);
            }

            FloatInput(controls, "BodyTurn", key.Turn, value => key.Turn = value, true, 48f);
        }

        if (body.Keys.Length < 128)
            ActionRow("AddPose", T("AddPoseHint"), () => ApplyConstructionEdit(() => BodyConstructionEdits.AppendPose(session.Draft)));
    }

    /// <summary>保留实际发力和装备，由玩家选择离手的形体。</summary>
    private void ProjectBody()
        => TransformBody(false);

    /// <summary>延伸仍依附真实末端及身体作用，不自动复制双方控制关系。</summary>
    private void ExtendBody()
        => TransformBody(true);

    /// <summary>由已装备的真实法宝承载作用，施放时再取得对应原物件。</summary>
    private void CarryBody()
        => TransformBody(false, true);

    /// <summary>实际发力部位或握持的装备只给出默认建议；新显化体使用自己的接触形状。</summary>
    /// <param name="extend">是否保留身体连接而不离手投送。</param>
    /// <param name="carrier">是否改为外部真实载体的连接。</param>
    private void TransformBody(bool extend, bool carrier = false)
    {
        CompiledBodyAction body = previewPlan?.Body;
        string suggested = body?.Part.Contact == BodyContactPart.Hand ? body.WeaponForm ?? "hand" : null;
        ShowFormChoices("ManifestationForm", suggested, form => ApplyConstructionEdit(() =>
        {
            var resource = FormResources.Load(form);
            if (resource.Contacts.Length == 0)
                throw ConstructionReferences.Error("Body/Manifestation", "形体没有用于命中的接触形状",
                    resourceKind: "FormContact", resourceId: form);
            if (carrier)
            {
                AbilityDefinition carried = BodyConstructionEdits.Carry(session.Draft, new BodyProjection { Form = form });
                AbilityCompiler.Compile(carried, cache: false);
                return carried;
            }
            return extend ? BodyConstructionEdits.Extend(session.Draft, new BodyExtension { Form = form }) :
                BodyConstructionEdits.Project(session.Draft, new BodyProjection { Form = form });
        }));
    }
}
