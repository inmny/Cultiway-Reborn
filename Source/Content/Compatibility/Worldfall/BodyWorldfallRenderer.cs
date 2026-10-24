using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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
    private static FieldInfo face, faceTurn, stance;
    /// <summary>独立人物卡片不属于世界身体，不接管其手臂或覆盖世界肩点。</summary>
    private static FieldInfo asFigure;
    /// <summary>正式身体动作接管时解除该人物的闲坐，不清空其他人物的姿态。</summary>
    private static System.Action<Actor> forgetRest;
    /// <summary>本帧隐藏位，外部穿着处理在下一帧重新建立。</summary>
    private static FieldInfo hidden;
    /// <summary>外部骨架明确命名的两侧肢段。</summary>
    private static FieldInfo upperRight, lowerRight, upperLeft, lowerLeft;
    /// <summary>真实骨架的部件、枢轴与地图坐标。</summary>
    private static FieldInfo meshModel, modelParts, partPivot, vectorX, vectorY, vectorZ;
    /// <summary>身体自己的肤色和衣料色，保留实际人物配色。</summary>
    private static FieldInfo palette, skinSlot, shirtSlot;
    /// <summary>外部自身的关节变换，包含父级姿态和身体朝向。</summary>
    private static MethodInfo transformPoint;
    /// <summary>当前场景实际收集到的两侧肩点，不保存旧帧身体引用。</summary>
    private static readonly Dictionary<(long Actor, bool Left), Vector3> shoulders = new();
    /// <summary>同一场景内复用已取得的身体配色。</summary>
    private static readonly Dictionary<long, (Color Skin, Color Shirt)> colours = new();
    /// <summary>肩点只对收集它们的这一帧有效。</summary>
    private static int shoulderFrame = -1;

    /// <summary>在已核对版本的旁观人物绘制完成后接管实际使用的部位。</summary>
    /// <param name="assembly">已加载的可选模组。</param>
    internal static void Install(Assembly assembly)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        System.Type people = assembly.GetType("FirstPerson.People", true);
        System.Type model = people.GetNestedType("Model", BindingFlags.NonPublic);
        figures = AccessTools.Field(people, "Figures");
        asFigure = AccessTools.Field(people, "_asFigure");
        forgetRest = (System.Action<Actor>)System.Delegate.CreateDelegate(typeof(System.Action<Actor>),
            AccessTools.Method(assembly.GetType("FirstPerson.Lounging", true), "Forget"));
        bodyMesh = AccessTools.Field(model, "Mesh"); bodyRig = AccessTools.Field(model, "Rig");
        heldWeapon = AccessTools.Field(model, "Weapon");
        face = AccessTools.Field(model, "Face"); faceTurn = AccessTools.Field(model, "FaceTurn");
        stance = AccessTools.Field(model, "Stance");
        System.Type mesh = assembly.GetType("FirstPerson.Core.MeshInstance", true);
        hidden = AccessTools.Field(mesh, "Hidden"); meshModel = AccessTools.Field(mesh, "Model");
        palette = AccessTools.Field(mesh, "Palette");
        modelParts = AccessTools.Field(assembly.GetType("FirstPerson.Core.MeshModel", true), "Parts");
        partPivot = AccessTools.Field(assembly.GetType("FirstPerson.Core.MeshPart", true), "Pivot");
        System.Type vector = assembly.GetType("FirstPerson.Core.Vec3", true);
        vectorX = AccessTools.Field(vector, "X"); vectorY = AccessTools.Field(vector, "Y"); vectorZ = AccessTools.Field(vector, "Z");
        System.Type rig = assembly.GetType("FirstPerson.Core.PersonRig", true);
        skinSlot = AccessTools.Field(rig, "SlotSkin"); shirtSlot = AccessTools.Field(rig, "SlotShirt");
        transformPoint = AccessTools.Method(rig, "Transform");
        upperRight = AccessTools.Field(rig, "UpperArmR"); lowerRight = AccessTools.Field(rig, "LowerArmR");
        upperLeft = AccessTools.Field(rig, "UpperArmL"); lowerLeft = AccessTools.Field(rig, "LowerArmL");
        new Harmony("cultiway.body.worldfall").Patch(people.GetMethod("AddMesh", flags),
            prefix: new HarmonyMethod(typeof(BodyWorldfallRenderer), nameof(BeforePeople)),
            postfix: new HarmonyMethod(typeof(BodyWorldfallRenderer), nameof(AfterPeople)));
    }

    /// <summary>将共同动作的朝向交给原身体，避免闲置转向缓动让手臂跨过胸前。</summary>
    /// <param name="__2">当前实际人物。</param>
    /// <param name="__9">原身体本次使用的水平朝向。</param>
    private static void BeforePeople(Actor __2, ref float __9)
    {
        if ((bool)asFigure.GetValue(null) || !ConstructedAbilityService.TryGetBodyPose(__2.id, out BodyPose pose)) return;
        forgetRest(__2);
        __9 = Mathf.Atan2(pose.Forward.y, pose.Forward.x) + pose.Turn * Mathf.Deg2Rad;
        object model = ((IDictionary)figures.GetValue(null))[__2];
        if (model == null) return;
        face.SetValue(model, __9); faceTurn.SetValue(model, 0f);
        stance.SetValue(model, 1f);
    }

    /// <summary>外部身体仍负责穿着、躯干及其他部位；接管部分只显示共同姿态。</summary>
    /// <param name="__0">本次三维模型集合。</param>
    /// <param name="__2">本次实际人物。</param>
    private static void AfterPeople(IList __0, Actor __2)
    {
        if ((bool)asFigure.GetValue(null) || !ConstructedAbilityService.TryGetBodyPose(__2.id, out _, out BodyOccupancy occupancy)) return;
        object model = ((IDictionary)figures.GetValue(null))[__2];
        if (model == null) return;
        object mesh = bodyMesh.GetValue(model), rig = bodyRig.GetValue(model);
        if (mesh == null || rig == null) return;
        if (shoulderFrame != Time.frameCount) { shoulders.Clear(); colours.Clear(); shoulderFrame = Time.frameCount; }
        var paint = (uint[])palette.GetValue(mesh);
        int skin = (int)skinSlot.GetValue(rig), shirt = (int)shirtSlot.GetValue(rig);
        // 外部骨架可不声明这两个颜色槽，此时保留来源本身的颜色。
        if (paint != null && skin >= 0 && shirt >= 0)
            colours[__2.id] = (ReadColour(paint[skin]), ReadColour(paint[shirt]));
        ulong mask = (ulong)hidden.GetValue(mesh);
        if ((occupancy & BodyOccupancy.RightArm) != 0)
        { Record(upperRight, false); Add(upperRight); Add(lowerRight); }
        if ((occupancy & BodyOccupancy.LeftArm) != 0)
        { Record(upperLeft, true); Add(upperLeft); Add(lowerLeft); }
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
            object point = transformPoint.Invoke(null, new[] { mesh, (object)index, partPivot.GetValue(parts.GetValue(index)) });
            shoulders[(__2.id, left)] = new Vector3((float)vectorX.GetValue(point), (float)vectorY.GetValue(point), (float)vectorZ.GetValue(point));
        }
    }

    /// <summary>使用实际身体的肤色或袖色，不用王国色覆盖三维人物穿着。</summary>
    /// <param name="instance">已经发布的实际身体部位。</param>
    internal static Color? ResolveTint(in FormVisualInstance instance)
    {
        if (instance.BodySegment == BodyVisualSegment.None || shoulderFrame != Time.frameCount ||
            !colours.TryGetValue(instance.GroundActorId, out var paint)) return instance.Tint;
        return instance.BodySegment is BodyVisualSegment.UpperRightArm or BodyVisualSegment.UpperLeftArm ? paint.Shirt : paint.Skin;
    }

    /// <summary>读取外部身体使用的低位红、绿、蓝颜色。</summary>
    /// <param name="packed">已核对格式的身体颜色。</param>
    private static Color ReadColour(uint packed) => new((packed & 255) / 255f, ((packed >> 8) & 255) / 255f, ((packed >> 16) & 255) / 255f, 1f);

    /// <summary>从真实肩点连接到共同腕点，适配不同身体比例；手、器物和接触保持共同状态。</summary>
    /// <param name="instance">有明确身体归属的肢段。</param>
    /// <param name="original">已经加入地表和游泳等显示偏移的变换。</param>
    /// <param name="shift">共同姿态到当前三维显示的平移。</param>
    internal static Matrix4x4 AdaptLimb(in FormVisualInstance instance, Matrix4x4 original, Vector3 shift)
    {
        bool left = instance.BodySegment is BodyVisualSegment.UpperLeftArm or BodyVisualSegment.LowerLeftArm;
        if (shoulderFrame != Time.frameCount || !shoulders.TryGetValue((instance.GroundActorId, left), out Vector3 shoulder) ||
            !ConstructedAbilityService.TryGetBodyPose(instance.GroundActorId, out BodyPose pose)) return original;
        // 先保留共同腕点，再按原动作的肢长和弯肘方向求连接；只有外部身体比例不同才调整显示肢长。
        Vector3 hand = pose.Hand + shift, desiredElbow = pose.Elbow + shift;
        Vector3 delta = hand - shoulder;
        float distance = Mathf.Max(.00001f, delta.magnitude);
        float upper = Vector3.Distance(pose.Shoulder, pose.Elbow), lower = Vector3.Distance(pose.Elbow, pose.Hand);
        if (distance <= Mathf.Abs(upper - lower)) upper = lower = (upper + lower) * .5f;
        float stretch = Mathf.Max(1f, distance / Mathf.Max(.00001f, upper + lower - .00001f));
        upper *= stretch; lower *= stretch;
        Vector3 direction = delta / distance;
        Vector3 bend = desiredElbow - shoulder;
        bend -= direction * Vector3.Dot(bend, direction);
        if (bend.sqrMagnitude < 1e-10f) bend = Vector3.Cross(direction, Vector3.forward);
        if (bend.sqrMagnitude < 1e-10f) bend = Vector3.right;
        float along = (upper * upper - lower * lower + distance * distance) / (2f * distance);
        Vector3 elbow = shoulder + direction * along + bend.normalized * Mathf.Sqrt(Mathf.Max(0f, upper * upper - along * along));
        bool first = instance.BodySegment is BodyVisualSegment.UpperRightArm or BodyVisualSegment.UpperLeftArm;
        Vector3 start = first ? shoulder : elbow, end = first ? elbow : hand;
        Vector3 size = original.lossyScale; size.z = Vector3.Distance(start, end);
        return Matrix4x4.TRS(start, Quaternion.FromToRotation(Vector3.forward, end - start), size);
    }
}
