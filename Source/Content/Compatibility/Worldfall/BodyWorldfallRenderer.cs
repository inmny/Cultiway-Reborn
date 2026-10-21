using System.Collections;
using System.Reflection;
using Cultiway.Content.Skills;
using Cultiway.Core.Skills;
using HarmonyLib;

namespace Cultiway.Content.Compatibility.Worldfall;

/// <summary>共同身体动作接管三维旁观人物的相应肢体及持物，避免同一把剑出现两份。</summary>
internal static class BodyWorldfallRenderer
{
    /// <summary>外部本帧已经建立的人物显示，不复制或修改人物持有关系。</summary>
    private static FieldInfo figures;
    /// <summary>外部人物显示中的主体、骨架及静态器物。</summary>
    private static FieldInfo bodyMesh, bodyRig, heldWeapon;
    /// <summary>本帧隐藏位，外部穿着处理在下一帧重新建立。</summary>
    private static FieldInfo hidden;
    /// <summary>外部骨架明确命名的两侧肢段。</summary>
    private static FieldInfo upperRight, lowerRight, upperLeft, lowerLeft;

    /// <summary>在已核对版本的旁观人物绘制完成后接管实际使用的部位。</summary>
    /// <param name="assembly">已加载的可选模组。</param>
    internal static void Install(Assembly assembly)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        System.Type people = assembly.GetType("FirstPerson.People", true);
        System.Type model = people.GetNestedType("Model", BindingFlags.NonPublic);
        figures = AccessTools.Field(people, "Figures");
        bodyMesh = AccessTools.Field(model, "Mesh"); bodyRig = AccessTools.Field(model, "Rig");
        heldWeapon = AccessTools.Field(model, "Weapon");
        hidden = AccessTools.Field(assembly.GetType("FirstPerson.Core.MeshInstance", true), "Hidden");
        System.Type rig = assembly.GetType("FirstPerson.Core.PersonRig", true);
        upperRight = AccessTools.Field(rig, "UpperArmR"); lowerRight = AccessTools.Field(rig, "LowerArmR");
        upperLeft = AccessTools.Field(rig, "UpperArmL"); lowerLeft = AccessTools.Field(rig, "LowerArmL");
        new Harmony("cultiway.body.worldfall").Patch(people.GetMethod("AddMesh", flags),
            postfix: new HarmonyMethod(typeof(BodyWorldfallRenderer), nameof(AfterPeople)));
    }

    /// <summary>外部身体仍负责穿着、躯干及其他部位；接管部分只显示共同姿态。</summary>
    /// <param name="__0">本次三维模型集合。</param>
    /// <param name="__2">本次实际人物。</param>
    private static void AfterPeople(IList __0, Actor __2)
    {
        if (!ConstructedAbilityService.TryGetBodyPose(__2.id, out _, out BodyOccupancy occupancy)) return;
        object model = ((IDictionary)figures.GetValue(null))[__2];
        if (model == null) return;
        object mesh = bodyMesh.GetValue(model), rig = bodyRig.GetValue(model);
        if (mesh == null || rig == null) return;
        ulong mask = (ulong)hidden.GetValue(mesh);
        if ((occupancy & BodyOccupancy.RightArm) != 0)
        { Add(upperRight); Add(lowerRight); }
        if ((occupancy & BodyOccupancy.LeftArm) != 0)
        { Add(upperLeft); Add(lowerLeft); }
        hidden.SetValue(mesh, mask);
        if (ConstructedAbilityService.IsWeaponBorrowed(__2)) __0.Remove(heldWeapon.GetValue(model));
        void Add(FieldInfo part)
        {
            int index = (int)part.GetValue(rig);
            if (index >= 0 && index < 64) mask |= 1UL << index;
        }
    }
}
