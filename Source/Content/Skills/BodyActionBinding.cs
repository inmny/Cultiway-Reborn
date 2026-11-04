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

    /// <summary>真实借用的器物，结束只解除显示控制。</summary>
    internal readonly Item Weapon;

    /// <summary>真实借用物品的稳定编号，回收后重用同一对象也不能沿用旧动作。</summary>
    private readonly long weaponId;

    /// <summary>开始时的装备资源，同一物品更换资源后解除旧绑定。</summary>
    private readonly EquipmentAsset weaponAsset;

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

    /// <summary>被其他动作查询时复用实际肢段、手掌和器物网格，不每步读取Unity资源。</summary>
    private BodySurfacePart[] actionSurfaceParts;

    /// <summary>临时收集同一姿态的形体，数值表面和最终绘制走相同的生成入口。</summary>
    private readonly List<FormVisualInstance> surfaceForms = new();

    /// <summary>出手时锁定的目标身体高度，目标跳开后不在空中追着改变动作。</summary>
    private readonly float aimHeight;

    /// <summary>受理时锁定的实际目标位置，随本步来源脚下换算距离。</summary>
    private readonly Vector2 aimPoint;

    /// <summary>发布施放前完成的真实接触路径适配，执行后不追踪移动目标。</summary>
    private Vector3 aimOffset;

    /// <summary>核对本轮已有的实际身体与器物适配。</summary>
    /// <param name="actor">实际身体。</param>
    /// <param name="action">当前编译动作。</param>
    internal static bool CanBind(ActorExtend actor, CompiledBodyAction action) => CanUse(actor, action, null, null);

    /// <summary>新施放读取当前资料，已开始的动作保持自己实际绑定的握持版本。</summary>
    /// <param name="actor">实际身体。</param><param name="action">本次动作。</param>
    /// <param name="boundGrip">旧施放已经取得的握持；新施放为空。</param>
    /// <param name="boundForm">旧施放已经取得的资源；新施放为空。</param>
    private static bool CanUse(ActorExtend actor, CompiledBodyAction action, FormResource boundForm, FormGrip boundGrip)
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
        FormResource form = boundForm ?? FormResources.Load(action.WeaponForm);
        return action.Effector == "hand.right" && body.hasWeapon() &&
               form.Description.Held.Groups.Contains(body.getWeaponAsset().group_id) &&
               EquippedWeaponVisualService.IsCurrent(body, body.getWeapon()) &&
               (boundGrip ?? form.Grip(action.Grip)).Fits(action.WeaponSize,
                   .035f * HandScale, .08f * HandScale);
    }

    /// <summary>提前解析实际需要的形体和握点，缺少资源时拒绝登记。</summary>
    /// <param name="action">需要核对的动作。</param>
    internal static void ValidateResources(CompiledBodyAction action)
    {
        foreach (var relation in action.Construction.Relations)
        {
            if (relation.Kind is BodyRelationKind.Touch or BodyRelationKind.Brace or BodyRelationKind.Grab &&
                (relation.From.Subject == BodyPartSubject.Source && relation.From.Part != action.Effector ||
                 relation.From.Subject == BodyPartSubject.Held && relation.From.Part != "item" ||
                 relation.From.Subject == BodyPartSubject.Target || relation.To.Subject != BodyPartSubject.Target ||
                 (relation.Kind == BodyRelationKind.Touch
                     ? relation.To.Part != "surface"
                     : !BodySurfaceBinding.SupportsPart(relation.To.Part)) || relation.ToAnchor != null))
                throw new InvalidDataException("Body/Construction/Relations/" + relation.Id + "：尚无该实际部位的接触适配");
        }

        ConstructionReferences.Resolve("Body/Effector/hand", () => FormResources.Load("hand"));
        ConstructionReferences.Resolve("Body/Effector/limb", () => FormResources.Load("limb"));
        var contactResource = FormResources.Load(action.WeaponForm ?? "hand");
        foreach (var relation in action.Construction.Relations.Where(r =>
                     r.Kind is BodyRelationKind.Touch or BodyRelationKind.Brace or BodyRelationKind.Grab))
        {
            if ((relation.From.Subject == BodyPartSubject.Held) != (action.WeaponForm != null) ||
                relation.FromAnchor != null && (action.Part.Contact != BodyContactPart.Hand
                    ? relation.FromAnchor != action.Part.Shape
                    : !contactResource.Description.Contacts.Any(c =>
                        CompiledBodyConstruction.MatchesContact(relation.FromAnchor, c.Id))))
                throw new InvalidDataException("Body/Construction/Relations/" + relation.Id + "：实际作用资源缺少所选接触形状");
        }

        if (action.WeaponForm == null) return;
        FormResource form =
            ConstructionReferences.Resolve("Body/WeaponForm", () => FormResources.Load(action.WeaponForm));
        ConstructionReferences.Resolve("Body/Grip", () => form.Grip(action.Grip));
        if (form.Description.Held.Groups.Length == 0)
            throw new InvalidDataException("Body/WeaponForm：资源未声明可对应的实际装备类型：" + action.WeaponForm);
    }

    /// <summary>确认当前实际部位或持物形状属于所选关系，不借用同次动作的其他接触面。</summary>
    /// <param name="relation">当前关系。</param><param name="shape">实际资源形状编号。</param>
    internal bool UsesRelationShape(in CompiledBodyRelation relation, int shape) =>
        CompiledBodyConstruction.MatchesContact(relation.FromAnchor, action.Part.Contact == BodyContactPart.Hand
            ? (weapon ?? hand).Description.Contacts[shape].Id
            : action.Part.Shape);

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
        weaponId = Weapon.data.id;
        weaponAsset = Weapon.getAsset();
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
        if (action.HasSupportMotion && actor.position_height != 0f &&
            !ConstructedAbilityService.OnSurface(actor)) return null;
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

    /// <summary>发布施放前，以明确所选的真实目标部位完成瞄准；工作线程仍只取得冻结后的身体输入。</summary>
    /// <param name="surface">同一时刻的实际目标表面。</param>
    /// <param name="reference">控制关系中段的实际支撑和身体。</param>
    /// <param name="relation">最早开始的控制关系与真实作用形状。</param>
    internal void AimAt(BodySurfacePose surface, in BodyFrame reference, in CompiledBodyRelation relation)
    {
        if (!action.MatchDistance || action.Part.Contact is not (BodyContactPart.Hand or BodyContactPart.Foot))
            return;
        float at = (relation.FromWorldTime + relation.UntilWorldTime) * .5f;
        var shapes = new List<AbilityCapsule>();
        Vector3 point = default;
        // 按真实掌面、足面或持物的中心调整整段路径，再由同一骨长求解核对剩余误差。
        for (int pass = 0; pass < 2; pass++)
        {
            BodyFrame frame = Frame(reference.Foot, reference.Height, reference.Support);
            BodyPose pose = action.Sample(at, frame);
            shapes.Clear();
            AppendContacts(pose, shapes);
            Vector3 center = Vector3.zero;
            int count = 0;
            foreach (var shape in shapes)
                if (UsesRelationShape(relation, shape.Shape))
                {
                    center += (shape.A + shape.B) * .5f;
                    count++;
                }

            center /= count;
            if (pass == 0) point = surface.Nearest(relation.To.Part, center);
            aimOffset += point - center;
        }
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
            Organ?.Frame(Direction), Vector2.Distance(aimPoint, foot), support, aimOffset: aimOffset);
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
                                          CanUse(actor.GetExtend(), action, weapon, grip) &&
                                          (action.WeaponForm == null ||
                                           Weapon.data?.id == weaponId && Weapon.getAsset() == weaponAsset &&
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
        pose.Rotation, Vector3.one), pose.Height * action.WeaponSize);

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

    /// <summary>动作的肢段、掌部、持物与独立器官使用实际显示网格和相同变换。</summary>
    /// <param name="pose">本次共同姿态。</param>
    internal BodySurfacePose ActionSurface(in BodyPose pose)
    {
        surfaceForms.Clear();
        AppendForms(0, in pose, surfaceForms, null, Color.white);
        if (actionSurfaceParts == null)
        {
            var parts = new List<BodySurfacePart>();
            foreach (FormVisualInstance form in surfaceForms)
                for (int i = 0; i < form.Resource.Meshes.Length; i++)
                {
                    string role = form.BodySegment switch
                    {
                        BodyVisualSegment.UpperRightArm => "arm.right.upper",
                        BodyVisualSegment.LowerRightArm => "arm.right.lower",
                        BodyVisualSegment.UpperLeftArm => "arm.left.upper",
                        BodyVisualSegment.LowerLeftArm => "arm.left.lower",
                        BodyVisualSegment.RightHand => "hand.right", BodyVisualSegment.LeftHand => "hand.left",
                        _ => Organ != null
                            ? "organ:" + action.Part.OrganId
                            : "part:" + form.Resource.Description.Parts[i].Id
                    };
                    string group = role == "hand.right" ? "arm.right.lower" :
                        role == "hand.left" ? "arm.left.lower" : null;
                    parts.Add(new BodySurfacePart(role, form.Resource.Meshes[i].vertices,
                        form.Resource.Meshes[i].triangles,
                        form.BodySegment == BodyVisualSegment.None && weapon != null ? "item" : null, group: group));
                }

            actionSurfaceParts = parts.ToArray();
        }

        var matrices = new Matrix4x4[actionSurfaceParts.Length];
        int at = 0;
        foreach (FormVisualInstance form in surfaceForms)
            for (int i = 0; i < form.Resource.Meshes.Length; i++)
                matrices[at++] = form.PartTransform(i);
        return new BodySurfacePose(actionSurfaceParts, matrices, pose.Foot, pose.Height,
            Mathf.Atan2(pose.Forward.y, pose.Forward.x));
    }

    /// <summary>绘制连接的肢段、掌部及实际借用的器物，不附带烟雾或拖尾。</summary>
    /// <param name="actor">真实来源。</param>
    /// <param name="pose">同一模拟姿态。</param>
    /// <param name="output">共同显示快照。</param>
    /// <param name="terrain">共同绝对高度地表；空值保留原版平地。</param>
    internal void AppendVisuals(Actor actor, in BodyPose pose, List<FormVisualInstance> output,
        AbilityHeightField terrain = null)
    {
        Color sleeve = Color.Lerp(actor.kingdom.getColor().getColorMain(), Color.white, .2f);
        AppendForms(actor.id, in pose, output, terrain, sleeve);
    }

    /// <summary>生成一份共同肢体姿态，不从绘制场景回读肩点，也不另求关节。</summary>
    /// <param name="actor">实际人物编号；纯数值查询不需要显示归属。</param><param name="pose">共同姿态。</param>
    /// <param name="output">实际形体。</param><param name="terrain">共同地表。</param><param name="sleeve">衣色。</param>
    private void AppendForms(long actor, in BodyPose pose, List<FormVisualInstance> output,
        AbilityHeightField terrain, Color sleeve)
    {
        if (Organ?.Model != null)
        {
            output.Add(new FormVisualInstance(Organ.Model.Resource, pose.Organ.Value.Model.Value,
                groundAnchor: (Vector2)pose.Foot, groundActorId: actor, terrain: terrain));
            return;
        }

        if (!action.Part.IsArm) return;
        Vector2 ground = pose.Foot;
        bool left = action.Part.Left;
        // 两种显示和接触都使用适配后的实际臂链，不只共用腕点。
        Vector3 shoulder = pose.OtherArm?.Shoulder ?? pose.Shoulder;
        Vector3 elbow = pose.OtherArm?.Elbow ?? pose.Elbow;
        AddSegment(shoulder, elbow, pose.Height * CompiledBodyAction.UpperWidth, sleeve,
            left ? BodyVisualSegment.UpperLeftArm : BodyVisualSegment.UpperRightArm);
        AddSegment(elbow, pose.Hand, pose.Height * CompiledBodyAction.LowerWidth, null,
            left ? BodyVisualSegment.LowerLeftArm : BodyVisualSegment.LowerRightArm);
        output.Add(new FormVisualInstance(hand, HandMatrix(in pose), fingers, groundAnchor: ground,
            groundActorId: actor, terrain: terrain,
            bodySegment: left ? BodyVisualSegment.LeftHand : BodyVisualSegment.RightHand));
        if (weapon != null)
            output.Add(new FormVisualInstance(weapon, WeaponMatrix(in pose), groundAnchor: ground,
                groundActorId: actor, terrain: terrain));

        void AddSegment(Vector3 start, Vector3 end, float width, Color? color, BodyVisualSegment segment)
        {
            Vector3 delta = end - start;
            output.Add(new FormVisualInstance(limb, Matrix4x4.TRS(start,
                    Quaternion.FromToRotation(Vector3.forward, delta),
                    new Vector3(width, width, delta.magnitude)), tint: color, groundAnchor: ground,
                groundActorId: actor, terrain: terrain,
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