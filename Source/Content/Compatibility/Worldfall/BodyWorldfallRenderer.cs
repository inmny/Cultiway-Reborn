using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using Cultiway.Content.Combat;
using Cultiway.Content.Skills;
using Cultiway.Core.Skills;
using Cultiway.Core.Visuals.Forms;
using HarmonyLib;
using UnityEngine;

namespace Cultiway.Content.Compatibility.Worldfall;

/// <summary>共同身体动作接管三维旁观人物的相应肢体及持物，避免同一把剑出现两份。</summary>
internal static class BodyWorldfallRenderer
{
    /// <summary>外部本帧已经建立的人物显示，不复制或修改人物持有关系。</summary>
    private static FieldInfo figures;

    /// <summary>外部人物显示中的主体、骨架及静态器物。</summary>
    private static FieldInfo bodyMesh, bodyRig, heldWeapon;

    /// <summary>三维人物的转向缓动，身体动作期间使用共同锁定朝向。</summary>
    private static FieldInfo face, faceTurn, stance, figureStage, childBody;

    /// <summary>独立人物卡片不属于世界身体，不接管其手臂或覆盖世界肩点。</summary>
    private static FieldInfo asFigure;

    /// <summary>正式身体动作接管时解除该人物的闲坐，不清空其他人物的姿态。</summary>
    private static System.Action<Actor> forgetRest;

    /// <summary>本帧隐藏位，外部穿着处理在下一帧重新建立。</summary>
    private static FieldInfo hidden;

    /// <summary>外部骨架明确命名的两侧肢段。</summary>
    private static FieldInfo upperRight, lowerRight, upperLeft, lowerLeft;

    /// <summary>站立支撑使用外部真实骨盆、腿段和足底点。</summary>
    private static FieldInfo headPart,
        hips,
        torsoPart,
        upperLegRight,
        lowerLegRight,
        upperLegLeft,
        lowerLegLeft,
        soleRight,
        soleLeft;

    /// <summary>真实骨架的部件、枢轴与地图坐标。</summary>
    private static FieldInfo meshModel, modelParts, partPivot, partPositions, vectorX, vectorY, vectorZ;

    /// <summary>身体自己的肤色和衣料色，保留实际人物配色。</summary>
    private static FieldInfo palette, skinSlot, shirtSlot;

    /// <summary>衣甲共用的肢体姿态数组和当前实际甲片数量。</summary>
    private static FieldInfo rotations, offsets, scales, armourShown, parentPart;

    /// <summary>在接入时绑定外部调用，逐帧取点不再建立反射参数数组。</summary>
    private static System.Func<object, int, Vector3, Vector3> worldPoint;

    /// <summary>将局部姿态写回外部骨架需要的值类型，不复制甲片几何。</summary>
    private static System.Func<Vector3, object> vectorValue;

    /// <summary>保留肢体横向尺寸，只沿骨长调整的外部矩阵。</summary>
    private static System.Func<Matrix4x4, object> matrixValue;

    /// <summary>外部自身的关节变换，包含父级姿态和身体朝向。</summary>
    private static MethodInfo transformPoint;

    /// <summary>当前场景实际收集到的两侧肩点，不保存旧帧身体引用。</summary>
    private static readonly Dictionary<(long Actor, bool Left), Vector3> shoulders = new();

    /// <summary>同一场景内复用已取得的身体配色。</summary>
    private static readonly Dictionary<long, (Color Skin, Color Shirt)> colours = new();

    /// <summary>肩点只对收集它们的这一帧有效。</summary>
    private static int shoulderFrame = -1;

    /// <summary>实际人物外观的资料入口，只在创建身体绑定时使用。</summary>
    private static MethodInfo figureOf,
        familyOf,
        lookFamily,
        detailedBody,
        rigOf,
        dressBody,
        dressLook,
        lookBody,
        bodyTall,
        spriteImage;

    /// <summary>外部实例及其原有图片缓存，不要求先打开三维镜头。</summary>
    private static PropertyInfo externalInstance, rigHeight;

    /// <summary>外部图片缓存与人物外观字段。</summary>
    private static FieldInfo externalSprites, figurePalette, female;

    /// <summary>临时身体只用于初始化原有外观资料，不加入任何场景或移动人物。</summary>
    private static System.Type meshType;

    /// <summary>每种原模型的固定骨长和安装点共用一份；实例比例另行换算。</summary>
    private static readonly Dictionary<(object Model, bool Left), BodyFit> fits = new();

    /// <summary>让器官接合复用已绑定的完整外部关节变换，不另建逐帧反射调用。</summary>
    /// <param name="mesh">外部实际身体。</param>
    /// <param name="part">身体部件。</param>
    /// <param name="point">模型空间位置。</param>
    internal static Vector3 TransformPoint(object mesh, int part, Vector3 point) => worldPoint(mesh, part, point);

    /// <summary>在已核对版本的旁观人物绘制完成后接管实际使用的部位。</summary>
    /// <param name="assembly">已加载的可选模组。</param>
    internal static void Install(Assembly assembly)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static |
                                   BindingFlags.Instance;
        System.Type people = assembly.GetType("FirstPerson.People", true);
        System.Type model = people.GetNestedType("Model", BindingFlags.NonPublic);
        figures = AccessTools.Field(people, "Figures");
        asFigure = AccessTools.Field(people, "_asFigure");
        forgetRest = (System.Action<Actor>)System.Delegate.CreateDelegate(typeof(System.Action<Actor>),
            AccessTools.Method(assembly.GetType("FirstPerson.Lounging", true), "Forget"));
        bodyMesh = AccessTools.Field(model, "Mesh");
        bodyRig = AccessTools.Field(model, "Rig");
        heldWeapon = AccessTools.Field(model, "Weapon");
        face = AccessTools.Field(model, "Face");
        faceTurn = AccessTools.Field(model, "FaceTurn");
        stance = AccessTools.Field(model, "Stance");
        figureStage = AccessTools.Field(model, "Stage");
        System.Type mesh = assembly.GetType("FirstPerson.Core.MeshInstance", true);
        hidden = AccessTools.Field(mesh, "Hidden");
        meshModel = AccessTools.Field(mesh, "Model");
        rotations = AccessTools.Field(mesh, "PartRotation");
        offsets = AccessTools.Field(mesh, "PartOffset");
        scales = AccessTools.Field(mesh, "PartScale");
        armourShown = AccessTools.Field(model, "ArmorShown");
        palette = AccessTools.Field(mesh, "Palette");
        modelParts = AccessTools.Field(assembly.GetType("FirstPerson.Core.MeshModel", true), "Parts");
        partPivot = AccessTools.Field(assembly.GetType("FirstPerson.Core.MeshPart", true), "Pivot");
        parentPart = AccessTools.Field(assembly.GetType("FirstPerson.Core.MeshPart", true), "Parent");
        partPositions = AccessTools.Field(assembly.GetType("FirstPerson.Core.MeshPart", true), "Pos");
        System.Type vector = assembly.GetType("FirstPerson.Core.Vec3", true);
        vectorX = AccessTools.Field(vector, "X");
        vectorY = AccessTools.Field(vector, "Y");
        vectorZ = AccessTools.Field(vector, "Z");
        System.Type rig = assembly.GetType("FirstPerson.Core.PersonRig", true);
        skinSlot = AccessTools.Field(rig, "SlotSkin");
        shirtSlot = AccessTools.Field(rig, "SlotShirt");
        transformPoint = AccessTools.Method(rig, "Transform");
        BindPoseValues(mesh, vector, assembly.GetType("FirstPerson.Core.Mat3", true));
        upperRight = AccessTools.Field(rig, "UpperArmR");
        lowerRight = AccessTools.Field(rig, "LowerArmR");
        upperLeft = AccessTools.Field(rig, "UpperArmL");
        lowerLeft = AccessTools.Field(rig, "LowerArmL");
        headPart = AccessTools.Field(rig, "Head");
        childBody = AccessTools.Field(rig, "ChildBody");
        hips = AccessTools.Field(rig, "Hips");
        torsoPart = AccessTools.Field(rig, "Torso");
        upperLegRight = AccessTools.Field(rig, "UpperLegR");
        lowerLegRight = AccessTools.Field(rig, "LowerLegR");
        upperLegLeft = AccessTools.Field(rig, "UpperLegL");
        lowerLegLeft = AccessTools.Field(rig, "LowerLegL");
        soleRight = AccessTools.Field(rig, "_soleR");
        soleLeft = AccessTools.Field(rig, "_soleL");
        System.Type models = assembly.GetType("FirstPerson.Models", true),
            external = assembly.GetType("FirstPerson.WorldBoxMod", true);
        figureOf = AccessTools.Method(people, "Figure");
        familyOf = AccessTools.Method(people, "FamilyOf");
        lookFamily = AccessTools.Method(people, "LookFamily");
        detailedBody = AccessTools.Method(models, "DetailedCreature");
        rigOf = AccessTools.Method(models, "Rig");
        dressBody = AccessTools.Method(rig, "Dress");
        dressLook = AccessTools.Method(people, "LookDress");
        lookBody = AccessTools.Method(people, "LookModel");
        bodyTall = AccessTools.Method(people, "LookTall");
        spriteImage = AccessTools.Method(assembly.GetType("FirstPerson.SpriteCache", true), "Get",
            new[] { typeof(Sprite) });
        externalInstance = AccessTools.Property(external, "Instance");
        externalSprites = AccessTools.Field(external, "_sprites");
        rigHeight = AccessTools.Property(rig, "Height");
        figurePalette = AccessTools.Field(model, "Palette");
        female = AccessTools.Field(model, "Female");
        meshType = mesh;
        BodyActionBinding.FitProvider = CaptureFit;
        new Harmony("cultiway.body.worldfall").Patch(people.GetMethod("AddMesh", flags),
            prefix: new HarmonyMethod(typeof(BodyWorldfallRenderer), nameof(BeforePeople)),
            postfix: new HarmonyMethod(typeof(BodyWorldfallRenderer), nameof(AfterPeople)));
    }

    /// <summary>从正式外观解析安装点及固定骨长，返回纯数值；原版视角也走同一入口。</summary>
    /// <param name="actor">当前实际身体。</param><param name="left">当前控制的手臂。</param>
    private static BodyFit CaptureFit(Actor actor, bool left)
    {
        object external = externalInstance.GetValue(null);
        if (external == null) return null;
        object sprites = externalSprites.GetValue(external);
        Sprite picture = actor.getSpriteToRender();
        WorldfallVisualBridge.PrepareSprites(sprites);
        object image = spriteImage.Invoke(sprites, new object[] { picture });
        if (image == null || !WorldfallVisualBridge.TryBodyHeight(actor, 1f, sprites, out float height)) return null;
        object figure = figureOf.Invoke(null, new[] { actor, image });
        object family = lookFamily.Invoke(null, new[] { figure, familyOf.Invoke(null, new[] { actor, figure }) });
        object model = detailedBody.Invoke(null, new[] { family, 0f, null });
        if (model == null) return null;
        object rig = rigOf.Invoke(null, new[] { model });
        int upper = (int)(left ? upperLeft : upperRight).GetValue(rig),
            lower = (int)(left ? lowerLeft : lowerRight).GetValue(rig),
            lu = (int)upperLegLeft.GetValue(rig),
            ll = (int)lowerLegLeft.GetValue(rig),
            ru = (int)upperLegRight.GetValue(rig),
            rl = (int)lowerLegRight.GetValue(rig);
        if (upper < 0 || lower < 0 || lu < 0 || ll < 0 || ru < 0 || rl < 0) return null;
        // 外部的衣着选择同时确定个体高度。初始化原有资料，不运行行走、动画或场景收集。
        object temporary = System.Activator.CreateInstance(meshType);
        meshModel.SetValue(temporary, model);
        dressBody.Invoke(rig, new[] { temporary, figurePalette.GetValue(figure), true, female.GetValue(figure) });
        dressLook.Invoke(null, new[] { actor, figure, family, rig, temporary });
        model = lookBody.Invoke(null, new[] { figure, model });
        rig = rigOf.Invoke(null, new[] { model });
        if (!fits.TryGetValue((model, left), out BodyFit raw))
        {
            var parts = (System.Array)modelParts.GetValue(model);
            Vector3 Point(int index) => ReadVector(partPivot.GetValue(parts.GetValue(index)));
            Vector3 Map(Vector3 point) => new(-point.y, point.x, point.z);
            Vector3 shoulder = Point(upper),
                elbow = Point(lower),
                leftHip = Point(lu),
                leftKnee = Point(ll),
                rightHip = Point(ru),
                rightKnee = Point(rl);
            float leftThigh = Vector3.Distance(leftHip, leftKnee),
                leftShin = Vector3.Distance(leftKnee, ReadVector(soleLeft.GetValue(rig))),
                rightThigh = Vector3.Distance(rightHip, rightKnee),
                rightShin = Vector3.Distance(rightKnee, ReadVector(soleRight.GetValue(rig)));
            raw = new BodyFit(Map(shoulder), Vector3.Distance(shoulder, elbow),
                new Vector3(left ? -.004f : .004f, -.012f, -.3f).magnitude,
                Map(leftHip), Map(rightHip),
                leftThigh + leftShin, rightThigh + rightShin,
                BodyJointSolver.MinimumReach(leftThigh, leftShin), BodyJointSolver.MinimumReach(rightThigh, rightShin),
                1f, CaptureLeg(parts, lu, ll, ReadVector(soleLeft.GetValue(rig))),
                CaptureLeg(parts, ru, rl, ReadVector(soleRight.GetValue(rig))),
                CaptureHead(parts, (int)headPart.GetValue(rig)));
            fits.Add((model, left), raw);
        }

        float scale = height *
            (float)bodyTall.Invoke(null, new[] { figure }) / (float)rigHeight.GetValue(rig);
        return new BodyFit(raw.Shoulder * scale, raw.Upper * scale, raw.Lower * scale,
            raw.LeftHip * scale, raw.RightHip * scale, raw.LeftLeg * scale, raw.RightLeg * scale,
            raw.LeftMinimum * scale, raw.RightMinimum * scale, height,
            raw.LeftLegData?.Scaled(scale), raw.RightLegData?.Scaled(scale),
            raw.HeadData?.Sized(figureStage.GetValue(figure).ToString() == "Child" && !(bool)childBody.GetValue(rig)
                ? 1.28f
                : 1f).Scaled(scale));
    }

    /// <summary>在实际模型的头部网格上取得关节及接触包络，不读取绘制时钟。</summary>
    /// <param name="parts">模型部件。</param><param name="head">原骨架声明的头部。</param>
    private static BodyHeadFit CaptureHead(System.Array parts, int head)
    {
        if (head < 0) return null;
        object part = parts.GetValue(head);
        var vertices = (float[])partPositions.GetValue(part);
        if (vertices == null || vertices.Length < 3) return null;
        Vector3 Read(int index) => new(vertices[index], vertices[index + 1], vertices[index + 2]);
        Bounds bounds = new(Read(0), Vector3.zero);
        for (int i = 3; i < vertices.Length; i += 3) bounds.Encapsulate(Read(i));
        Vector3 a = bounds.center - Vector3.forward * bounds.extents.z * .5f,
            b = bounds.center + Vector3.forward * bounds.extents.z * .5f;
        float radius = 0;
        for (int i = 0; i < vertices.Length; i += 3)
        {
            Vector3 point = Read(i), nearest = point;
            nearest.x = a.x;
            nearest.y = a.y;
            nearest.z = Mathf.Clamp(point.z, a.z, b.z);
            radius = Mathf.Max(radius, Vector3.Distance(point, nearest));
        }

        Vector3 Map(Vector3 value) => new(-value.y, value.x, value.z);
        return new BodyHeadFit(Map(ReadVector(partPivot.GetValue(part))), Map(a), Map(b), radius);
    }

    /// <summary>只在模型绑定时读取实际腿骨与足部网格，工作线程只取得数值。</summary>
    /// <param name="parts">原模型部件。</param><param name="upper">大腿部件。</param>
    /// <param name="lower">小腿部件。</param><param name="sole">实际足底点。</param>
    private static BodyLegFit CaptureLeg(System.Array parts, int upper, int lower, Vector3 sole)
    {
        object part = parts.GetValue(lower);
        var vertices = (float[])partPositions.GetValue(part);
        if (vertices == null || vertices.Length < 3) return null;
        Vector3 hip = ReadVector(partPivot.GetValue(parts.GetValue(upper))),
            knee = ReadVector(partPivot.GetValue(part));
        float length = Vector3.Distance(hip, knee) + Vector3.Distance(knee, sole);
        Vector3 first = new(vertices[0], vertices[1], vertices[2]);
        float top = first.z;
        for (int i = 3; i < vertices.Length; i += 3) top = Mathf.Max(top, vertices[i + 2]);
        Bounds foot = new(sole, Vector3.zero);
        float kneeRadius = 0;
        for (int i = 0; i < vertices.Length; i += 3)
        {
            Vector3 point = new(vertices[i], vertices[i + 1], vertices[i + 2]);
            if (point.z <= sole.z + length * .12f) foot.Encapsulate(point);
            if (point.z >= top - length * .04f) kneeRadius = Mathf.Max(kneeRadius, Vector3.Distance(point, knee));
        }

        Vector3 extents = foot.extents, axis;
        float radius, half;
        if (extents.x > extents.y)
        {
            axis = Vector3.right;
            radius = new Vector2(extents.y, extents.z).magnitude;
            half = extents.x;
        }
        else
        {
            axis = Vector3.up;
            radius = new Vector2(extents.x, extents.z).magnitude;
            half = extents.y;
        }

        Vector3 a = foot.center - axis * half - sole, b = foot.center + axis * half - sole;
        Vector3 Map(Vector3 value) => new(-value.y, value.x, value.z);
        return new BodyLegFit(Map(hip), Map(knee), Map(sole), kneeRadius, Map(a), Map(b), radius);
    }

    /// <summary>切世界不保留外部几何、旧身体安装点或旧配色。</summary>
    internal static void Clear()
    {
        fits.Clear();
        shoulders.Clear();
        colours.Clear();
        shoulderFrame = -1;
    }

    /// <summary>实际身体使用共同脚下和尺寸，全身发力时再接管转向；绘制不另行追赶体型。</summary>
    /// <param name="__2">当前实际人物。</param>
    /// <param name="__4">原身体的实际图片缓存。</param>
    /// <param name="__5">本次横向位置。</param><param name="__6">本次纵向位置。</param>
    /// <param name="__7">本次绝对脚下高度。</param><param name="__8">本次身体高度。</param>
    /// <param name="__9">原身体本次使用的水平朝向。</param>
    private static void BeforePeople(Actor __2, object __4, ref float __5, ref float __6, ref float __7, ref float __8,
        ref float __9)
    {
        if ((bool)asFigure.GetValue(null)) return;
        if (!ConstructedAbilityService.TryGetBodyRoot(__2.id, out BodyPose pose, out _))
        {
            // 闲置和接管时采用相同的站立图片比例，避免切入动作时从另一套尺寸缓动跳过来。
            if (__2.asset.is_humanoid && WorldfallVisualBridge.TryBodyHeight(__2,
                    SpatialActorPose.Height(__2), __4, out float height)) __8 = height;
            return;
        }

        Vector3 root = pose.Foot + WorldfallVisualBridge.BodyShift(__2);
        __5 = root.x;
        __6 = root.y;
        __7 = root.z;
        __8 = pose.BodyHeight;
        bool full = ConstructedAbilityService.TryGetBodyPose(__2.id, BodyOccupancy.Stance, out _);
        if (full) forgetRest(__2);
        __9 = Mathf.Atan2(pose.Forward.y, pose.Forward.x) + pose.Turn * Mathf.Deg2Rad;
        object model = ((IDictionary)figures.GetValue(null))[__2];
        if (model == null) return;
        face.SetValue(model, __9);
        faceTurn.SetValue(model, 0f);
        if (full) stance.SetValue(model, 1f);
    }

    /// <summary>外部身体仍负责穿着、躯干及其他部位；接管部分只显示共同姿态。</summary>
    /// <param name="__0">本次三维模型集合。</param>
    /// <param name="__2">本次实际人物。</param>
    private static void AfterPeople(IList __0, Actor __2)
    {
        if ((bool)asFigure.GetValue(null) ||
            !ConstructedAbilityService.TryGetBodyPose(__2.id, out BodyPose completed, out BodyOccupancy occupancy))
            return;
        object model = ((IDictionary)figures.GetValue(null))[__2];
        if (model == null) return;
        object mesh = bodyMesh.GetValue(model), rig = bodyRig.GetValue(model);
        if (mesh == null || rig == null) return;
        ResetTrunk(mesh, rig, completed.Head.HasValue);
        if (ConstructedAbilityService.TryGetBodyPose(__2.id, BodyOccupancy.Stance, out BodyPose support))
            RetargetStance(mesh, rig, in support);
        if (support.Head.HasValue) RetargetHead(mesh, rig, in support);
        if (shoulderFrame != Time.frameCount)
        {
            shoulders.Clear();
            colours.Clear();
            shoulderFrame = Time.frameCount;
        }

        var paint = (uint[])palette.GetValue(mesh);
        int skin = (int)skinSlot.GetValue(rig), shirt = (int)shirtSlot.GetValue(rig);
        // 外部骨架可不声明这两个颜色槽，此时保留来源本身的颜色。
        if (paint != null && skin >= 0 && shirt >= 0)
            colours[__2.id] = (ReadColour(paint[skin]), ReadColour(paint[shirt]));
        ulong mask = (ulong)hidden.GetValue(mesh);
        if ((occupancy & BodyOccupancy.RightArm) != 0)
        {
            Record(upperRight, false);
            Add(upperRight);
            Add(lowerRight);
        }

        if ((occupancy & BodyOccupancy.LeftArm) != 0)
        {
            Record(upperLeft, true);
            Add(upperLeft);
            Add(lowerLeft);
        }

        hidden.SetValue(mesh, mask);
        if (ConstructedAbilityService.IsWeaponBorrowed(__2)) __0.Remove(heldWeapon.GetValue(model));

        void Add(FieldInfo part)
        {
            int index = (int)part.GetValue(rig);
            if (index >= 0 && index < 64) mask |= 1UL << index;
        }

        void Record(FieldInfo part, bool left)
        {
            int index = (int)part.GetValue(rig);
            if (index < 0) return;
            var parts = (System.Array)modelParts.GetValue(meshModel.GetValue(mesh));
            object point = transformPoint.Invoke(null,
                new[] { mesh, (object)index, partPivot.GetValue(parts.GetValue(index)) });
            shoulders[(__2.id, left)] = new Vector3((float)vectorX.GetValue(point), (float)vectorY.GetValue(point),
                (float)vectorZ.GetValue(point));
        }
    }

    /// <summary>身体控制期间躯干只接受共同姿态，不叠加外部闲坐、呼吸或步态的另一套上身偏移。</summary>
    /// <param name="mesh">实际身体。</param><param name="rig">实际骨架。</param>
    /// <param name="head">头部也受共同动作控制。</param>
    private static void ResetTrunk(object mesh, object rig, bool head)
    {
        var localRotations = (System.Array)rotations.GetValue(mesh);
        var localOffsets = (System.Array)offsets.GetValue(mesh);
        var localScales = (float[])scales.GetValue(mesh);
        Reset(hips);
        Reset(torsoPart);
        if (head) Reset(headPart);

        void Reset(FieldInfo field)
        {
            int index = (int)field.GetValue(rig);
            if (index < 0) return;
            localRotations.SetValue(matrixValue(Matrix4x4.identity), index);
            localOffsets.SetValue(vectorValue(Vector3.zero), index);
            localScales[index] = 1f;
        }
    }

    /// <summary>骨盆跟随共同重心，两脚保持共同落点，再连接实际腿段；不会移动真实人物或推进时钟。</summary>
    /// <param name="mesh">当前真实身体。</param><param name="rig">实际关节资料。</param>
    /// <param name="pose">本轮完成的共同支撑。</param>
    private static void RetargetStance(object mesh, object rig, in BodyPose pose)
    {
        int hip = (int)hips.GetValue(rig),
            leftUpper = (int)upperLegLeft.GetValue(rig),
            leftLower = (int)lowerLegLeft.GetValue(rig),
            rightUpper = (int)upperLegRight.GetValue(rig),
            rightLower = (int)lowerLegRight.GetValue(rig);
        if (hip < 0 || leftUpper < 0 || leftLower < 0 || rightUpper < 0 || rightLower < 0) return;
        var parts = (System.Array)modelParts.GetValue(meshModel.GetValue(mesh));
        Matrix4x4 lu = WorldMatrix(mesh, leftUpper),
            ll = WorldMatrix(mesh, leftLower),
            ru = WorldMatrix(mesh, rightUpper),
            rl = WorldMatrix(mesh, rightLower);
        Vector3 shift = (Vector3)WorldMatrix(mesh, -1).GetColumn(3) - pose.Foot, forward = pose.Forward;
        Matrix4x4 parentInverse = WorldMatrix(mesh, (int)parentPart.GetValue(parts.GetValue(hip))).inverse;
        var localOffsets = (System.Array)offsets.GetValue(mesh);
        localOffsets.SetValue(vectorValue(ReadVector(localOffsets.GetValue(hip)) +
                                          parentInverse.MultiplyVector(pose.Torso)), hip);
        Leg(leftUpper, leftLower, ReadVector(soleLeft.GetValue(rig)), lu, ll, pose.LeftFoot + shift, pose.LeftLeg);
        Leg(rightUpper, rightLower, ReadVector(soleRight.GetValue(rig)), ru, rl, pose.RightFoot + shift, pose.RightLeg);

        /// <summary>按原腿段长度求膝点，并将两段实际骨架连到共同足底。</summary>
        /// <param name="upper">上腿部件。</param><param name="lower">下腿部件。</param>
        /// <param name="sole">模型内实际足底点。</param><param name="upperBefore">改骨盆前的上腿变换。</param>
        /// <param name="lowerBefore">改骨盆前的下腿变换。</param><param name="foot">本次真实落脚显示位置。</param>
        /// <param name="declared">已经完成的共同腿姿态。</param>
        void Leg(int upper, int lower, Vector3 sole, Matrix4x4 upperBefore, Matrix4x4 lowerBefore, Vector3 foot,
            BodyLegPose? declared)
        {
            if (declared.HasValue)
            {
                BodyLegPose leg = declared.Value;
                Vector3 lowerPivotPoint = ReadVector(partPivot.GetValue(parts.GetValue(lower)));
                RetargetBone(mesh, parts, upper, upperBefore, lowerPivotPoint, leg.Hip + shift, leg.Knee + shift);
                RetargetBone(mesh, parts, lower, lowerBefore, sole, leg.Knee + shift, leg.Foot + shift);
                return;
            }

            Vector3 upperPivot = ReadVector(partPivot.GetValue(parts.GetValue(upper))),
                lowerPivot = ReadVector(partPivot.GetValue(parts.GetValue(lower)));
            Vector3 start = worldPoint(mesh, upper, upperPivot), delta = foot - start;
            float distance = Mathf.Max(.00001f, delta.magnitude),
                thigh = Vector3.Distance(upperBefore.MultiplyPoint3x4(upperPivot),
                    lowerBefore.MultiplyPoint3x4(lowerPivot)),
                shin = Vector3.Distance(lowerBefore.MultiplyPoint3x4(lowerPivot), lowerBefore.MultiplyPoint3x4(sole));
            Vector3 direction = delta / distance, bend = forward;
            bend -= direction * Vector3.Dot(bend, direction);
            if (bend.sqrMagnitude < 1e-8f) bend = Vector3.Cross(direction, Vector3.right);
            float along = (thigh * thigh - shin * shin + distance * distance) / (2f * distance);
            Vector3 knee = start + direction * along +
                           bend.normalized * Mathf.Sqrt(Mathf.Max(0f, thigh * thigh - along * along));
            RetargetBone(mesh, parts, upper, upperBefore, lowerPivot, start, knee);
            RetargetBone(mesh, parts, lower, lowerBefore, sole, knee, foot);
        }
    }

    /// <summary>原头骨及其头发、头盔子部件一起跟随共同头姿态。</summary>
    /// <param name="mesh">实际身体。</param><param name="rig">实际骨架。</param><param name="pose">共同头部及躯干姿态。</param>
    private static void RetargetHead(object mesh, object rig, in BodyPose pose)
    {
        int index = (int)headPart.GetValue(rig);
        if (index < 0) return;
        var parts = (System.Array)modelParts.GetValue(meshModel.GetValue(mesh));
        object part = parts.GetValue(index);
        Vector3 pivot = ReadVector(partPivot.GetValue(part));
        Matrix4x4 before = WorldMatrix(mesh, index),
            parentInverse = WorldMatrix(mesh, (int)parentPart.GetValue(part)).inverse;
        BodyHeadPose head = pose.Head.Value;
        Vector3 shift = (Vector3)WorldMatrix(mesh, -1).GetColumn(3) - pose.Foot;
        Matrix4x4 local = parentInverse * Matrix4x4.Rotate(head.Delta) * before;
        ((System.Array)rotations.GetValue(mesh)).SetValue(matrixValue(local), index);
        ((System.Array)offsets.GetValue(mesh)).SetValue(
            vectorValue(parentInverse.MultiplyPoint3x4(head.Pivot + shift) - pivot), index);
        ((float[])scales.GetValue(mesh))[index] = head.ShapeScale;
    }

    /// <summary>使用实际身体的肤色或袖色，不用王国色覆盖三维人物穿着。</summary>
    /// <param name="instance">已经发布的实际身体部位。</param>
    internal static Color? ResolveTint(in FormVisualInstance instance)
    {
        if (instance.BodySegment == BodyVisualSegment.None || shoulderFrame != Time.frameCount ||
            !colours.TryGetValue(instance.GroundActorId, out var paint)) return instance.Tint;
        return instance.BodySegment is BodyVisualSegment.UpperRightArm or BodyVisualSegment.UpperLeftArm
            ? paint.Shirt
            : paint.Skin;
    }

    /// <summary>读取外部身体使用的低位红、绿、蓝颜色。</summary>
    /// <param name="packed">已核对格式的身体颜色。</param>
    private static Color ReadColour(uint packed) => new((packed & 255) / 255f, ((packed >> 8) & 255) / 255f,
        ((packed >> 16) & 255) / 255f, 1f);

    /// <summary>从真实肩点连接到共同腕点，适配不同身体比例；手、器物和接触保持共同状态。</summary>
    /// <param name="instance">有明确身体归属的肢段。</param>
    /// <param name="original">已经加入地表和游泳等显示偏移的变换。</param>
    /// <param name="shift">共同姿态到当前三维显示的平移。</param>
    internal static Matrix4x4 AdaptLimb(in FormVisualInstance instance, Matrix4x4 original, Vector3 shift)
    {
        bool left = instance.BodySegment is BodyVisualSegment.UpperLeftArm or BodyVisualSegment.LowerLeftArm;
        if (shoulderFrame != Time.frameCount ||
            !shoulders.TryGetValue((instance.GroundActorId, left), out Vector3 shoulder) ||
            !ConstructedAbilityService.TryGetBodyPose(instance.GroundActorId,
                left ? BodyOccupancy.LeftArm : BodyOccupancy.RightArm, out BodyPose pose)) return original;
        if (!pose.OtherArm.HasValue) return original;
        BodyArmPose arm = pose.OtherArm.Value;
        Vector3 hand = pose.Hand + shift, direction = (hand - shoulder).normalized;
        Vector3 reference = (pose.Hand - arm.Shoulder).normalized;
        Vector3 bend = Quaternion.FromToRotation(reference, direction) * arm.Bend;
        BodyJointSolver.Solve(shoulder, hand, arm.Upper, arm.Lower, bend, direction, bend, 0f, -1f, -1f,
            out Vector3 elbow, out Vector3 endHand, out _);
        bool first = instance.BodySegment is BodyVisualSegment.UpperRightArm or BodyVisualSegment.UpperLeftArm;
        Vector3 start = first ? shoulder : elbow, end = first ? elbow : endHand;
        if (first) RetargetArmour(instance.GroundActorId, left, shoulder, elbow, endHand);
        Vector3 size = original.lossyScale;
        size.z = Vector3.Distance(start, end);
        return Matrix4x4.TRS(start, Quaternion.FromToRotation(Vector3.forward, end - start), size);
    }

    /// <summary>衣甲继续使用身体原有姿态数组，只让已接管的两段肢体跟随共同动作。</summary>
    /// <param name="actorId">当前真实人物。</param>
    /// <param name="left">是否为左臂。</param>
    /// <param name="shoulder">实际三维肩点。</param>
    /// <param name="elbow">与共同腕点连接后的肘点。</param>
    /// <param name="hand">共同腕点。</param>
    private static void RetargetArmour(long actorId, bool left, Vector3 shoulder, Vector3 elbow, Vector3 hand)
    {
        Actor actor = World.world.units.get(actorId);
        if (actor == null) return;
        object model = ((IDictionary)figures.GetValue(null))[actor];
        if (model == null || (int)armourShown.GetValue(model) == 0) return;
        object mesh = bodyMesh.GetValue(model), rig = bodyRig.GetValue(model);
        int upper = (int)(left ? upperLeft : upperRight).GetValue(rig),
            lower = (int)(left ? lowerLeft : lowerRight).GetValue(rig);
        if (upper < 0 || lower < 0) return;
        var parts = (System.Array)modelParts.GetValue(meshModel.GetValue(mesh));
        Vector3 lowerPivot = ReadVector(partPivot.GetValue(parts.GetValue(lower)));
        // 两段都先保留原姿态，避免上臂伸长再次放大弯曲前臂的横向厚度。
        Matrix4x4 upperBefore = WorldMatrix(mesh, upper), lowerBefore = WorldMatrix(mesh, lower);
        RetargetBone(mesh, parts, upper, upperBefore, lowerPivot, shoulder, elbow);
        RetargetBone(mesh, parts, lower, lowerBefore, lowerPivot + new Vector3(left ? -.004f : .004f, -.012f, -.3f),
            elbow, hand);
    }

    /// <summary>先求当前及父级变换，再写局部旋转、轴向伸缩和枢轴偏移，甲片仍保留原来的宽度。</summary>
    /// <param name="mesh">外部身体，与衣甲共用姿态。</param>
    /// <param name="parts">真实部件。</param>
    /// <param name="index">本段部件。</param>
    /// <param name="before">尚未接管父级时的完整原姿态。</param>
    /// <param name="end">原模型坐标中的肢段末端。</param>
    /// <param name="targetStart">新起点。</param>
    /// <param name="targetEnd">新末端。</param>
    private static void RetargetBone(object mesh, System.Array parts, int index, Matrix4x4 before, Vector3 end,
        Vector3 targetStart, Vector3 targetEnd)
    {
        object part = parts.GetValue(index);
        Vector3 pivot = ReadVector(partPivot.GetValue(part));
        Matrix4x4 parentInverse = WorldMatrix(mesh, (int)parentPart.GetValue(part)).inverse;
        Vector3 delta = before.MultiplyPoint3x4(end) - before.MultiplyPoint3x4(pivot), wanted = targetEnd - targetStart;
        Vector3 axis = delta.normalized;
        Matrix4x4 stretch = Matrix4x4.identity;
        float ratio = wanted.magnitude / delta.magnitude;
        for (int row = 0; row < 3; row++)
        for (int column = 0; column < 3; column++)
            stretch[row, column] += (ratio - 1f) * axis[row] * axis[column];
        Matrix4x4 local = parentInverse * Matrix4x4.Rotate(Quaternion.FromToRotation(delta, wanted)) * stretch * before;
        ((System.Array)rotations.GetValue(mesh)).SetValue(matrixValue(local), index);
        ((System.Array)offsets.GetValue(mesh)).SetValue(
            vectorValue(parentInverse.MultiplyPoint3x4(targetStart) - pivot), index);
        ((float[])scales.GetValue(mesh))[index] = 1f;
    }

    /// <summary>读取包含父级和体型的完整坐标系；较长的参考轴减少地图位置引起的浮点消减。</summary>
    /// <param name="mesh">实际身体。</param>
    /// <param name="part">部件编号，负值表示身体根坐标。</param>
    private static Matrix4x4 WorldMatrix(object mesh, int part)
    {
        Vector3 origin = worldPoint(mesh, part, Vector3.zero);
        Matrix4x4 matrix = Matrix4x4.identity;
        for (int column = 0; column < 3; column++)
        {
            Vector3 unit = column == 0 ? Vector3.right : column == 1 ? Vector3.up : Vector3.forward;
            Vector3 axis = (worldPoint(mesh, part, unit * 8f) - origin) / 8f;
            matrix.SetColumn(column, new Vector4(axis.x, axis.y, axis.z, 0f));
        }

        matrix.SetColumn(3, new Vector4(origin.x, origin.y, origin.z, 1f));
        return matrix;
    }

    /// <summary>只在发现外部版本时连接坐标与矩阵调用，不把其类型引入核心技能资料。</summary>
    /// <param name="mesh">外部实例类型。</param>
    /// <param name="vector">外部三维数值类型。</param>
    /// <param name="matrix">外部矩阵类型。</param>
    private static void BindPoseValues(System.Type mesh, System.Type vector, System.Type matrix)
    {
        var vectorConstructor = vector.GetConstructor(new[] { typeof(float), typeof(float), typeof(float) });
        var point = Expression.Parameter(typeof(Vector3), "point");
        NewExpression NewVector(Expression x, Expression y, Expression z) => Expression.New(vectorConstructor, x, y, z);
        NewExpression nativePoint = NewVector(Expression.Field(point, "x"), Expression.Field(point, "y"),
            Expression.Field(point, "z"));
        vectorValue = Expression
            .Lambda<System.Func<Vector3, object>>(Expression.Convert(nativePoint, typeof(object)), point).Compile();
        var source = Expression.Parameter(typeof(object), "mesh");
        var part = Expression.Parameter(typeof(int), "part");
        var result = Expression.Variable(vector, "result");
        var transform = Expression.Block(new[] { result },
            Expression.Assign(result,
                Expression.Call(transformPoint, Expression.Convert(source, mesh), part, nativePoint)),
            Expression.New(typeof(Vector3).GetConstructor(new[] { typeof(float), typeof(float), typeof(float) }),
                Expression.Field(result, vectorX), Expression.Field(result, vectorY),
                Expression.Field(result, vectorZ)));
        worldPoint = Expression.Lambda<System.Func<object, int, Vector3, Vector3>>(transform, source, part, point)
            .Compile();
        var pose = Expression.Parameter(typeof(Matrix4x4), "pose");

        NewExpression Column(int index) => NewVector(Expression.Field(pose, "m0" + index),
            Expression.Field(pose, "m1" + index), Expression.Field(pose, "m2" + index));

        var value = Expression.New(matrix.GetConstructor(new[] { vector, vector, vector }), Column(0), Column(1),
            Column(2));
        matrixValue = Expression.Lambda<System.Func<Matrix4x4, object>>(Expression.Convert(value, typeof(object)), pose)
            .Compile();
    }

    /// <summary>读取已核对格式的外部枢轴。</summary>
    /// <param name="value">外部三维数值。</param>
    private static Vector3 ReadVector(object value) => new((float)vectorX.GetValue(value),
        (float)vectorY.GetValue(value), (float)vectorZ.GetValue(value));
}