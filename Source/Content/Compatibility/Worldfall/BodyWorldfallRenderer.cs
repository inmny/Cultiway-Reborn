using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
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
        rotations = AccessTools.Field(mesh, "PartRotation"); offsets = AccessTools.Field(mesh, "PartOffset");
        scales = AccessTools.Field(mesh, "PartScale"); armourShown = AccessTools.Field(model, "ArmorShown");
        palette = AccessTools.Field(mesh, "Palette");
        modelParts = AccessTools.Field(assembly.GetType("FirstPerson.Core.MeshModel", true), "Parts");
        partPivot = AccessTools.Field(assembly.GetType("FirstPerson.Core.MeshPart", true), "Pivot");
        parentPart = AccessTools.Field(assembly.GetType("FirstPerson.Core.MeshPart", true), "Parent");
        System.Type vector = assembly.GetType("FirstPerson.Core.Vec3", true);
        vectorX = AccessTools.Field(vector, "X"); vectorY = AccessTools.Field(vector, "Y"); vectorZ = AccessTools.Field(vector, "Z");
        System.Type rig = assembly.GetType("FirstPerson.Core.PersonRig", true);
        skinSlot = AccessTools.Field(rig, "SlotSkin"); shirtSlot = AccessTools.Field(rig, "SlotShirt");
        transformPoint = AccessTools.Method(rig, "Transform");
        BindPoseValues(mesh, vector, assembly.GetType("FirstPerson.Core.Mat3", true));
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
        if (first) RetargetArmour(instance.GroundActorId, left, shoulder, elbow, hand);
        Vector3 size = original.lossyScale; size.z = Vector3.Distance(start, end);
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
        int upper = (int)(left ? upperLeft : upperRight).GetValue(rig), lower = (int)(left ? lowerLeft : lowerRight).GetValue(rig);
        if (upper < 0 || lower < 0) return;
        var parts = (System.Array)modelParts.GetValue(meshModel.GetValue(mesh));
        Vector3 lowerPivot = ReadVector(partPivot.GetValue(parts.GetValue(lower)));
        // 两段都先保留原姿态，避免上臂伸长再次放大弯曲前臂的横向厚度。
        Matrix4x4 upperBefore = WorldMatrix(mesh, upper), lowerBefore = WorldMatrix(mesh, lower);
        RetargetBone(mesh, parts, upper, upperBefore, lowerPivot, shoulder, elbow);
        RetargetBone(mesh, parts, lower, lowerBefore, lowerPivot + new Vector3(left ? -.004f : .004f, -.012f, -.3f), elbow, hand);
    }

    /// <summary>先求当前及父级变换，再写局部旋转、轴向伸缩和枢轴偏移，甲片仍保留原来的宽度。</summary>
    /// <param name="mesh">外部身体，与衣甲共用姿态。</param>
    /// <param name="parts">真实部件。</param>
    /// <param name="index">本段部件。</param>
    /// <param name="before">尚未接管父级时的完整原姿态。</param>
    /// <param name="end">原模型坐标中的肢段末端。</param>
    /// <param name="targetStart">新起点。</param>
    /// <param name="targetEnd">新末端。</param>
    private static void RetargetBone(object mesh, System.Array parts, int index, Matrix4x4 before, Vector3 end, Vector3 targetStart, Vector3 targetEnd)
    {
        object part = parts.GetValue(index);
        Vector3 pivot = ReadVector(partPivot.GetValue(part));
        Matrix4x4 parentInverse = WorldMatrix(mesh, (int)parentPart.GetValue(part)).inverse;
        Vector3 delta = before.MultiplyPoint3x4(end) - before.MultiplyPoint3x4(pivot), wanted = targetEnd - targetStart;
        Vector3 axis = delta.normalized;
        Matrix4x4 stretch = Matrix4x4.identity;
        float ratio = wanted.magnitude / delta.magnitude;
        for (int row = 0; row < 3; row++)
        for (int column = 0; column < 3; column++) stretch[row, column] += (ratio - 1f) * axis[row] * axis[column];
        Matrix4x4 local = parentInverse * Matrix4x4.Rotate(Quaternion.FromToRotation(delta, wanted)) * stretch * before;
        ((System.Array)rotations.GetValue(mesh)).SetValue(matrixValue(local), index);
        ((System.Array)offsets.GetValue(mesh)).SetValue(vectorValue(parentInverse.MultiplyPoint3x4(targetStart) - pivot), index);
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
        NewExpression nativePoint = NewVector(Expression.Field(point, "x"), Expression.Field(point, "y"), Expression.Field(point, "z"));
        vectorValue = Expression.Lambda<System.Func<Vector3, object>>(Expression.Convert(nativePoint, typeof(object)), point).Compile();
        var source = Expression.Parameter(typeof(object), "mesh"); var part = Expression.Parameter(typeof(int), "part");
        var result = Expression.Variable(vector, "result");
        var transform = Expression.Block(new[] { result },
            Expression.Assign(result, Expression.Call(transformPoint, Expression.Convert(source, mesh), part, nativePoint)),
            Expression.New(typeof(Vector3).GetConstructor(new[] { typeof(float), typeof(float), typeof(float) }),
                Expression.Field(result, vectorX), Expression.Field(result, vectorY), Expression.Field(result, vectorZ)));
        worldPoint = Expression.Lambda<System.Func<object, int, Vector3, Vector3>>(transform, source, part, point).Compile();
        var pose = Expression.Parameter(typeof(Matrix4x4), "pose");
        NewExpression Column(int index) => NewVector(Expression.Field(pose, "m0" + index), Expression.Field(pose, "m1" + index), Expression.Field(pose, "m2" + index));
        var value = Expression.New(matrix.GetConstructor(new[] { vector, vector, vector }), Column(0), Column(1), Column(2));
        matrixValue = Expression.Lambda<System.Func<Matrix4x4, object>>(Expression.Convert(value, typeof(object)), pose).Compile();
    }

    /// <summary>读取已核对格式的外部枢轴。</summary>
    /// <param name="value">外部三维数值。</param>
    private static Vector3 ReadVector(object value) => new((float)vectorX.GetValue(value), (float)vectorY.GetValue(value), (float)vectorZ.GetValue(value));
}
