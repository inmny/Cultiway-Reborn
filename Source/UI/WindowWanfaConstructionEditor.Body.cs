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

        string effector = previewPlan?.Body?.Effector ?? body.Effector;
        bool organ = previewPlan?.Body?.Part.IsOrgan == true || body.Keys.Any(key => key.PartRotation != null);
        Row("Effector",
            organ
                ? T("Organ") + " / " + LMTools.GetOrFallback(
                    "Cultiway.CreatureOrgan." + previewPlan?.Body?.Part.OrganId, T("NaturalOrgan"))
                : T(previewPlan?.Body?.Part.Label ?? (effector == "hand.right" ? "RightHand" :
                    effector == "hand.left" ? "LeftHand" : "UnsupportedEffector")));
        Row("HeldForm",
            body.WeaponForm == null
                ? T(organ ? "NaturalOrgan" : "BareHand")
                : T("Form." + body.WeaponForm) + " / " + (previewPlan?.Body?.Grip ?? body.Grip));
        BodyOccupancyRows();
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
            ActionRow("ProjectBody", T("ProjectBodyHint"),
                ProjectBody);
        else Row("Manifestation", T("ManifestationHint"));
        bool leg = previewPlan?.Body?.Part.IsLeg == true || body.Keys.Any(key => key.Foot != null);
        bool head = previewPlan?.Body?.Part.IsHead == true || body.Keys.Any(key => key.HeadRotation != null);
        string[] handles = organ ? new[] { "PartRotation" } :
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
        Row("BodyCoordinates",
            T(organ ? "PartRotationHint" :
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
            if (organ)
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
            ActionRow("AddPose", T("AddPoseHint"), () => Change(() =>
            {
                BodyMotionKey last = body.Keys[body.Keys.Length - 1];
                var next = new BodyMotionKey
                {
                    At = last.At + .1f, Hand = (float[])last.Hand.Clone(), Torso = (float[])last.Torso.Clone(),
                    Rotation = (float[])last.Rotation.Clone(), Turn = last.Turn, Foot = (float[])last.Foot?.Clone(),
                    HeadRotation = (float[])last.HeadRotation?.Clone(), PartRotation = (float[])last.PartRotation?.Clone()
                };
                body.Keys = body.Keys.Concat(new[] { next }).ToArray();
                session.Draft.Lifetime = Mathf.Max(session.Draft.Lifetime, next.At);
            }));
    }

    /// <summary>手部沿用掌形显化，其他实际部位由玩家明确选择已有形体。</summary>
    private void ProjectBody()
    {
        if (previewPlan?.Body?.Part.Contact == BodyContactPart.Hand)
        {
            ApplyConstructionEdit(() => BodyConstructionEdits.Project(session.Draft, new BodyProjection()));
            return;
        }

        menu.Show(T("Form"), FormResources.Ids.Where(id => FormResources.Load(id).Meshes.Length > 0)
            .Select(id => new UiOptionMenuOption
            {
                Label = LMTools.GetOrFallback("Cultiway.Construction.Form." + id, id),
                Select = () => ApplyConstructionEdit(() =>
                    BodyConstructionEdits.Project(session.Draft, new BodyProjection { Form = id }))
            }).ToArray(), true);
    }
}
