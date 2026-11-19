using System;
using System.Linq;
using Cultiway.Core.Skills;
using Cultiway.Core.Visuals.Forms;
using Cultiway.Utils.Extension;
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
        string[] handles = rotating ? new[] { "PartRotation", "NamedFeet", "BodyFootwork" } :
            head ? new[] { "HeadRotation", "TorsoPoint" } :
            leg ? new[] { "FootPoint", "TorsoPoint" } : new[] { "HandPoint", "TorsoPoint", "HandRotation" };
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
        if (rotating && handles[bodyHandle] == "BodyFootwork")
        {
            BodyFootworkRows(body);
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
                    ? () => Change(() => body.Keys = body.Keys.Where((_, at) => at != index).ToArray())
                    : null, UiIcons.Delete);
            Transform controls = row.UseInlineControls(260f);
            FloatInput(controls, "At", key.At, value => key.At = value, i != 0, 48f);
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
                FloatInput(controls, new[] { "X", "Y", "Z" }[axis], vector[axis], value =>
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

    /// <summary>手部沿用掌形显化，其他实际部位由玩家明确选择已有形体。</summary>
    private void ProjectBody()
        => TransformBody(false);

    /// <summary>延伸仍依附真实末端及身体作用，不自动复制双方控制关系。</summary>
    private void ExtendBody()
        => TransformBody(true);

    /// <summary>由已装备的真实法宝承载作用，施放时再取得对应原物件。</summary>
    private void CarryBody()
        => TransformBody(true, true);

    /// <summary>沿共用资料命令改造，非掌部必须先明确选择已有形体。</summary>
    /// <param name="extend">是否保留身体连接而不离手投送。</param>
    /// <param name="carrier">是否改为外部真实载体的连接。</param>
    private void TransformBody(bool extend, bool carrier = false)
    {
        /// <summary>同一入口建立可编辑资料，不把本次实体编号写入知识。</summary>
        /// <param name="form">玩家明确选择的形体，掌部可缺省。</param>
        AbilityDefinition Transform(string form = null) => carrier
            ? BodyConstructionEdits.Carry(session.Draft, new BodyProjection { Form = form })
            : extend ? BodyConstructionEdits.Extend(session.Draft, new BodyExtension { Form = form }) :
            BodyConstructionEdits.Project(session.Draft, new BodyProjection { Form = form });
        if (previewPlan?.Body?.Part.Contact == BodyContactPart.Hand)
        {
            ApplyConstructionEdit(() => Transform());
            return;
        }

        menu.Show(T("Form"), FormResources.Ids.Where(id => FormResources.Load(id).Meshes.Length > 0)
            .Select(id => new UiOptionMenuOption
            {
                Label = LMTools.GetOrFallback("Cultiway.Construction.Form." + id, id),
                Select = () => ApplyConstructionEdit(() => Transform(id))
            }).ToArray(), true);
    }
}
