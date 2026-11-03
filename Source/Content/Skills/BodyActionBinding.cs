using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cultiway.Content.Combat;
using Cultiway.Core;
using Cultiway.Core.Skills;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.Visuals.Forms;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>将已编译动作绑定真实肢体及当前器物，显示和接触共用这些绑定。</summary>
internal sealed class BodyActionBinding
{
    /// <summary>可选身体接入在主线程采集固定比例，不依赖当前是否打开其镜头。</summary>
    internal static Func<Actor, bool, BodyFit> FitProvider;

    /// <summary>可选身体显示接回普通移动时，继承实际终点与整身朝向；只在主线程调用。</summary>
    internal static Action<Actor, Vector3> FacingReleased;

    /// <summary>本次实际身体的固定比例，只向工作线程提供数字。</summary>
    private readonly BodyFit fit;

    /// <summary>原版实际头图的关节与包络，创建时读取。</summary>
    private readonly BodyHeadFit nativeHead;

    /// <summary>已有实际器官的独立绑定，其他动作为空。</summary>
    internal BodyOrganBinding Organ { get; private set; }

    /// <summary>手掌使用实际体高的比例，掌长及可闭合柄粗一起缩放。</summary>
    private const float HandScale = CompiledBodyAction.HandScale;

    /// <summary>已制作持剑的模型尺寸与体高之比。</summary>
    private const float WeaponScale = CompiledBodyAction.WeaponScale;

    /// <summary>真实借用的器物，结束只解除显示控制。</summary>
    internal readonly Item Weapon;

    /// <summary>开始时锁定的水平朝向，不在出手后追着目标转身。</summary>
    internal readonly Vector3 Direction;

    /// <summary>本组并行身体动作共用的实际身体朝向。</summary>
    internal readonly Vector3 Facing;

    /// <summary>用于连续姿态的实际末端。</summary>
    private readonly CompiledBodyAction action;

    /// <summary>带有指节及掌面作用区的共用手形。</summary>
    private readonly FormResource hand;

    /// <summary>固定肢段资源。</summary>
    private readonly FormResource limb;

    /// <summary>实际借用器物的显示及作用资源。</summary>
    private readonly FormResource weapon;

    /// <summary>已解析的握点，不在运行中查找名称。</summary>
    private readonly FormGrip grip;

    /// <summary>手指闭合后器物穿过的掌内握点。</summary>
    private readonly Vector3 handGrip;

    /// <summary>握持时逐节屈指，空手时不覆盖原始伸掌。</summary>
    private readonly Matrix4x4[] fingers;

    /// <summary>出手时锁定的目标身体高度，目标跳开后不在空中追着改变动作。</summary>
    private readonly float aimHeight;

    /// <summary>受理时锁定的实际目标位置，随本步来源脚下换算距离。</summary>
    private readonly Vector2 aimPoint;

    /// <summary>核对本轮已有的实际身体与器物适配。</summary>
    /// <param name="actor">实际身体。</param>
    /// <param name="action">当前编译动作。</param>
    internal static bool CanBind(ActorExtend actor, CompiledBodyAction action) => CanUse(actor, action, null);

    /// <summary>新施放读取当前资料，已开始的动作保持自己实际绑定的握持版本。</summary>
    /// <param name="actor">实际身体。</param><param name="action">本次动作。</param>
    /// <param name="boundGrip">旧施放已经取得的握持；新施放为空。</param>
    private static bool CanUse(ActorExtend actor, CompiledBodyAction action, FormGrip boundGrip)
    {
        Actor body = actor.Base;
        if (action.HasSupportMotion && body.under_forces) return false;
        if (action.Part.IsOrgan)
            return (FitProvider == null || BodyOrganBinding.ModelProvider != null) && !body.isFlying() &&
                   SkillCasterContextService.Resolve(actor).Kind == SkillCarrierKind.Physical &&
                   BodyOrganBinding.CanBind(body, action.Part.OrganId);
        if (!body.asset.is_humanoid || body.isFlying() ||
            SkillCasterContextService.Resolve(actor).Kind != SkillCarrierKind.Physical) return false;
        if (action.WeaponForm == null) return !action.Part.IsArm || action.Part.Left || !body.hasWeapon();
        // 原版只有一个实际武器槽，当前对应右手；左手不能再次借用同一物件。
        return action.Effector == "hand.right" && action.WeaponForm == "sword" && body.hasWeapon() &&
               body.getWeaponAsset().group_id == "sword" &&
               EquippedWeaponVisualService.IsCurrent(body, body.getWeapon()) &&
               (boundGrip ?? FormResources.Load(action.WeaponForm).Grip(action.Grip)).Fits(WeaponScale,
                   .035f * HandScale, .08f * HandScale);
    }

    /// <summary>提前解析实际需要的形体和握点，缺少资源时拒绝登记。</summary>
    /// <param name="action">需要核对的动作。</param>
    internal static void ValidateResources(CompiledBodyAction action)
    {
        foreach (var relation in action.Construction.Relations)
        {
            if (relation.Kind is BodyRelationKind.Brace or BodyRelationKind.Grab)
                throw new InvalidDataException("Body/Construction/Relations/" + relation.Id + "：双方控制执行尚未接入");
            if (relation.Kind == BodyRelationKind.Touch &&
                (relation.From.Subject == BodyPartSubject.Source && relation.From.Part != action.Effector ||
                 relation.From.Subject == BodyPartSubject.Held && relation.From.Part != "item" ||
                 relation.From.Subject == BodyPartSubject.Target || relation.To.Subject != BodyPartSubject.Target ||
                 relation.To.Part != "surface" || relation.ToAnchor != null))
                throw new InvalidDataException("Body/Construction/Relations/" + relation.Id + "：尚无该实际部位的接触适配");
        }

        ConstructionReferences.Resolve("Body/Effector/hand", () => FormResources.Load("hand"));
        ConstructionReferences.Resolve("Body/Effector/limb", () => FormResources.Load("limb"));
        var contactResource = FormResources.Load(action.WeaponForm ?? "hand");
        foreach (var relation in action.Construction.Relations.Where(r => r.Kind == BodyRelationKind.Touch))
        {
            if ((relation.From.Subject == BodyPartSubject.Held) != (action.WeaponForm != null) ||
                relation.FromAnchor != null && (action.Part.Contact != BodyContactPart.Hand
                    ? relation.FromAnchor != action.Part.Shape
                    : !contactResource.Description.Contacts.Any(c => c.Id == relation.FromAnchor)))
                throw new InvalidDataException("Body/Construction/Relations/" + relation.Id + "：实际作用资源缺少所选接触形状");
        }

        if (action.WeaponForm == null) return;
        FormResource form =
            ConstructionReferences.Resolve("Body/WeaponForm", () => FormResources.Load(action.WeaponForm));
        ConstructionReferences.Resolve("Body/Grip", () => form.Grip(action.Grip));
        if (action.WeaponForm != "sword")
            throw new InvalidDataException("Body/WeaponForm：当前没有借用该器物的实际握持适配：" + action.WeaponForm +
                                           "；已支持sword，可作为普通生成主体使用其他形体");
    }

    /// <summary>一次施放只绑定一次真实物件及手部关节。</summary>
    /// <param name="actor">已核对的实际身体。</param>
    /// <param name="action">已编译动作。</param>
    /// <param name="direction">锁定的出手朝向。</param>
    /// <param name="targetFoot">共同时间的实际目标脚下。</param>
    /// <param name="targetHeight">共同时间的实际目标高度。</param>
    /// <param name="fit">已在主线程采集的实际身体比例。</param>
    private BodyActionBinding(Actor actor, CompiledBodyAction action, Vector3 direction, Vector3 targetFoot,
        float targetHeight, BodyFit fit)
    {
        this.action = action;
        this.fit = fit;
        nativeHead = action.Part.IsHead ? CaptureNativeHead(actor) : null;
        Direction = direction;
        Facing = BodyControlState.Facing(actor.id, direction);
        aimHeight = targetFoot.z + targetHeight * .55f;
        aimPoint = targetFoot;
        if (action.Part.IsOrgan) return;
        hand = FormResources.Load("hand");
        limb = FormResources.Load("limb");
        if (action.WeaponForm == null) return;
        Weapon = actor.getWeapon();
        weapon = FormResources.Load(action.WeaponForm);
        handGrip = Point(hand.Description.Anchors.Single(anchor => anchor.Id == action.Construction.HandGrip).Position);
        grip = weapon.Grip(action.Grip);
        fingers = new Matrix4x4[hand.Meshes.Length];
        fingers[0] = Matrix4x4.identity;
        for (int finger = 0; finger < 5; finger++)
        {
            int first = 1 + finger * 2, second = first + 1;
            Vector3 pivot = Point(hand.Description.Parts[first].Pivot),
                tip = Point(hand.Description.Parts[second].Pivot);
            fingers[first] = Around(pivot, Quaternion.Euler(-65f, 0f, finger == 4 ? -35f : 0f));
            fingers[second] = fingers[first] * Around(tip, Quaternion.Euler(-65f, 0f, 0f));
        }
    }

    /// <summary>可选身体确实支持实际肢段时才绑定；失败发生在取得控制和支付之前。</summary>
    /// <param name="actor">实际身体。</param><param name="action">本次动作。</param>
    /// <param name="direction">本手瞄准方向。</param><param name="targetFoot">目标实际脚下。</param>
    /// <param name="targetHeight">目标实际体高。</param>
    internal static BodyActionBinding TryCreate(Actor actor, CompiledBodyAction action, Vector3 direction,
        Vector3 targetFoot, float targetHeight)
    {
        if (action.HasSupportMotion && actor.position_height != 0f) return null;
        if (action.Part.IsOrgan)
        {
            if (FitProvider != null && BodyOrganBinding.ModelProvider == null) return null;
            BodyOrganBinding organ = BodyOrganBinding.TryCreate(actor, action.Part.OrganId);
            if (organ == null) return null;
            return new BodyActionBinding(actor, action, direction, targetFoot, targetHeight, null) { Organ = organ };
        }

        BodyFit fit = FitProvider?.Invoke(actor, action.Part.Left);
        if ((action.Part.IsLeg || action.HasSupportMotion) && fit != null &&
            (fit.LeftLegData == null || fit.RightLegData == null)) return null;
        if (FitProvider != null && fit == null || action.Part.IsHead && fit != null && fit.HeadData == null)
            return null;
        var binding = new BodyActionBinding(actor, action, direction, targetFoot, targetHeight, fit);
        return action.Part.IsHead && binding.nativeHead == null ? null : binding;
    }

    /// <summary>从真实体高及当前脚下采集本步身体，左右肢体分别提供肩点和肘部方向。</summary>
    /// <param name="actor">来源人物。</param>
    internal BodyFrame Frame(Actor actor)
    {
        return Frame(SpatialActorPose.Foot(actor), SpatialActorPose.Height(actor));
    }

    /// <summary>用共同时间步采集的身体位置推进，避免一帧内把整段位移提前套给动作。</summary>
    /// <param name="foot">本步实际脚下位置。</param>
    /// <param name="height">本步实际体高。</param>
    /// <param name="support">按实际地表及落脚换算的支撑。</param>
    internal BodyFrame Frame(Vector3 foot, float height, BodySupportPose? support = null)
    {
        float side = action.Part.Left ? -1f : 1f;
        return new BodyFrame(foot, Facing, height, new Vector3(0, side * CompiledBodyAction.ShoulderSide, .62f),
            height * CompiledBodyAction.UpperArm, height * CompiledBodyAction.LowerArm, side,
            Mathf.Clamp(aimHeight - foot.z - height * .62f, -height * .3f, height * .3f), fit, Direction, nativeHead,
            Organ?.Frame(Direction), Vector2.Distance(aimPoint, foot), support);
    }

    /// <summary>读取正式头图及其在身体帧中的实际位置，头部未提供时不造一个替代球体。</summary>
    /// <param name="actor">当前真实人物。</param>
    private static BodyHeadFit CaptureNativeHead(Actor actor)
    {
        Sprite body = actor.calculateMainSprite();
        if (body == null) return null;
        actor.calculateColoredSprite(body);
        AnimationFrameData frame = actor.getAnimationFrameData();
        Sprite head = actor.cached_sprite_head;
        if (frame == null || !frame.show_head || head == null) return null;
        float scale = Mathf.Max(.03f, actor.actor_scale) / (body.pixelsPerUnit * SpatialActorPose.Height(actor));
        Vector3 center = new(frame.pos_head_new.x - body.pivot.x + head.bounds.center.x * head.pixelsPerUnit, 0,
            frame.pos_head_new.y - body.pivot.y + head.bounds.center.y * head.pixelsPerUnit);
        center *= scale;
        if (actor.flip) center.x = -center.x;
        float radius = head.bounds.extents.x * head.pixelsPerUnit * scale;
        float half = head.bounds.extents.y * head.pixelsPerUnit * scale;
        return radius <= 0
            ? null
            : new BodyHeadFit(new Vector3(center.x, 0, center.z - half),
                center - Vector3.forward * Mathf.Max(0, half - radius),
                center + Vector3.forward * Mathf.Max(0, half - radius), radius, true);
    }

    /// <summary>借用器物被换走、损坏或失去时立即失效，不能用旧外观继续结算。</summary>
    /// <param name="actor">来源身体。</param>
    internal bool IsValid(Actor actor) => (Organ == null || Organ.Refresh()) &&
                                          CanUse(actor.GetExtend(), action, grip) &&
                                          (action.WeaponForm == null ||
                                           EquippedWeaponVisualService.IsCurrent(actor, Weapon));

    /// <summary>让手部模型腕点准确连接到实际前臂末端。</summary>
    /// <param name="pose">已确定身体姿态。</param>
    private Matrix4x4 HandMatrix(in BodyPose pose) => Matrix4x4.TRS(pose.Hand,
                                                          pose.Rotation * (weapon == null
                                                              ? Quaternion.identity
                                                              : Quaternion.Euler(0f, -90f, 0f)),
                                                          Vector3.one * pose.Height * HandScale) *
                                                      Matrix4x4.Translate(new Vector3(0, .04f, 0));

    /// <summary>器物握点对准闭合手指内的握点，腕点只用于接合前臂。</summary>
    /// <param name="pose">已确定身体姿态。</param>
    private Matrix4x4 WeaponMatrix(in BodyPose pose) => grip.Align(Matrix4x4.TRS(
        HandMatrix(in pose).MultiplyPoint3x4(handGrip),
        pose.Rotation, Vector3.one), pose.Height * WeaponScale);

    /// <summary>同一姿态的掌面或器物作用形状，不叠加单独的近战圆形范围。</summary>
    /// <param name="pose">实际身体姿态。</param>
    /// <param name="output">本步形状。</param>
    internal void AppendContacts(in BodyPose pose, List<AbilityCapsule> output) =>
        AppendContactShapes(in pose, output, false);

    /// <summary>关系解除时保留最后一段扫掠，正常起点只读取仍有效的关系。</summary>
    /// <param name="pose">同刻身体姿态。</param><param name="output">共同作用形状。</param>
    /// <param name="includeEnd">是否取解除时点的末态。</param>
    internal void AppendContactShapes(in BodyPose pose, List<AbilityCapsule> output, bool includeEnd)
    {
        if (action.Part.IsOrgan)
        {
            if (action.Construction.UsesContact(false, "organ", pose.WorldAge, includeEnd))
            {
                BodyOrganPose part = pose.Organ.Value;
                if (Organ.Model == null) Organ.Shape.Append(part, output);
                else
                {
                    Matrix4x4 matrix = part.Model.Value;
                    for (int i = 0; i < Organ.Model.Resource.Contacts.Length; i++)
                    {
                        var shape = Organ.Model.Resource.Contacts[i];
                        output.Add(new AbilityCapsule(-1, matrix.MultiplyPoint3x4(shape.A),
                            matrix.MultiplyPoint3x4(shape.B), shape.Radius * Organ.Model.Scale, "physical", i));
                    }
                }
            }

            return;
        }

        if (action.Part.IsHead)
        {
            if (action.Construction.UsesContact(false, "head", pose.WorldAge, includeEnd))
            {
                BodyHeadPose head = pose.Head.Value;
                output.Add(new AbilityCapsule(-1, head.A, head.B, head.Radius, "physical"));
            }

            return;
        }

        if (action.Part.IsLeg)
        {
            if (action.Construction.UsesContact(false, action.Part.Shape, pose.WorldAge, includeEnd))
            {
                BodyLegPose leg = (action.Part.Left ? pose.LeftLeg : pose.RightLeg).Value;
                bool knee = action.Part.Contact == BodyContactPart.Knee;
                output.Add(new AbilityCapsule(-1, knee ? leg.Knee : leg.FootA, knee ? leg.Knee : leg.FootB,
                    knee ? leg.KneeRadius : leg.FootRadius, "physical"));
            }

            return;
        }

        if (action.Part.Contact != BodyContactPart.Hand)
        {
            if (action.Construction.UsesContact(false, action.Part.Shape, pose.WorldAge, includeEnd))
                output.Add(new AbilityCapsule(-1, pose.EffectorPoint, pose.EffectorPoint,
                    action.JointRadius * pose.Height, "physical"));
            return;
        }

        FormResource resource = weapon ?? hand;
        Matrix4x4 transform = weapon == null ? HandMatrix(in pose) : WeaponMatrix(in pose);
        for (int shape = 0; shape < resource.Description.Contacts.Length; shape++)
        {
            FormContactDescription contact = resource.Description.Contacts[shape];
            if (!action.Construction.UsesContact(weapon != null, contact.Id, pose.WorldAge, includeEnd)) continue;
            output.Add(new AbilityCapsule(-1, transform.MultiplyPoint3x4(Point(contact.A)),
                transform.MultiplyPoint3x4(Point(contact.B)), contact.Radius * transform.lossyScale.x,
                weapon == null ? "physical" : "iron", shape));
        }
    }

    /// <summary>绘制连接的肢段、掌部及实际借用的器物，不附带烟雾或拖尾。</summary>
    /// <param name="actor">真实来源。</param>
    /// <param name="pose">同一模拟姿态。</param>
    /// <param name="output">共同显示快照。</param>
    /// <param name="terrain">共同绝对高度地表；空值保留原版平地。</param>
    internal void AppendVisuals(Actor actor, in BodyPose pose, List<FormVisualInstance> output,
        AbilityHeightField terrain = null)
    {
        if (Organ?.Model != null)
        {
            output.Add(new FormVisualInstance(Organ.Model.Resource, pose.Organ.Value.Model.Value,
                groundAnchor: (Vector2)pose.Foot, groundActorId: actor.id, terrain: terrain));
            return;
        }

        if (!action.Part.IsArm) return;
        Vector2 ground = pose.Foot;
        Color sleeve = Color.Lerp(actor.kingdom.getColor().getColorMain(), Color.white, .2f);
        bool left = action.Part.Left;
        // 肘肩接触采用实际适配臂链；两种显示都绘制这条臂链，不能只让手腕相同。
        bool fittedJoint = action.Part.Contact != BodyContactPart.Hand && pose.OtherArm.HasValue;
        Vector3 shoulder = fittedJoint ? pose.OtherArm.Value.Shoulder : pose.Shoulder;
        Vector3 elbow = fittedJoint ? pose.OtherArm.Value.Elbow : pose.Elbow;
        AddSegment(shoulder, elbow, pose.Height * CompiledBodyAction.UpperWidth, sleeve,
            left ? BodyVisualSegment.UpperLeftArm : BodyVisualSegment.UpperRightArm);
        AddSegment(elbow, pose.Hand, pose.Height * CompiledBodyAction.LowerWidth, null,
            left ? BodyVisualSegment.LowerLeftArm : BodyVisualSegment.LowerRightArm);
        output.Add(new FormVisualInstance(hand, HandMatrix(in pose), fingers, groundAnchor: ground,
            groundActorId: actor.id, terrain: terrain,
            bodySegment: left ? BodyVisualSegment.LeftHand : BodyVisualSegment.RightHand));
        if (weapon != null)
            output.Add(new FormVisualInstance(weapon, WeaponMatrix(in pose), groundAnchor: ground,
                groundActorId: actor.id, terrain: terrain));

        void AddSegment(Vector3 start, Vector3 end, float width, Color? color, BodyVisualSegment segment)
        {
            Vector3 delta = end - start;
            output.Add(new FormVisualInstance(limb, Matrix4x4.TRS(start,
                    Quaternion.FromToRotation(Vector3.forward, delta),
                    new Vector3(width, width, delta.magnitude)), tint: color, groundAnchor: ground,
                groundActorId: actor.id, terrain: terrain,
                bodySegment: segment));
        }
    }

    /// <summary>以一个已解析的关节中心施加局部旋转。</summary>
    /// <param name="pivot">关节中心。</param>
    /// <param name="rotation">局部旋转。</param>
    private static Matrix4x4 Around(Vector3 pivot, Quaternion rotation) =>
        Matrix4x4.Translate(pivot) * Matrix4x4.Rotate(rotation) * Matrix4x4.Translate(-pivot);

    /// <summary>从已检查资源读取三维坐标。</summary>
    /// <param name="value">资源坐标。</param>
    private static Vector3 Point(float[] value) => new(value[0], value[1], value[2]);
}