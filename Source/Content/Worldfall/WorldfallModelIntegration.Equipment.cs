#if S3COND2_WORLDFALL
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Cultiway.Patch;
using FirstPerson.Core;
using UnityEngine;

namespace Cultiway.Content.Worldfall;

/// <summary>让鱼人等直立生物显示实际装备，并跟随原生手臂动作。</summary>
public sealed partial class WorldfallModelIntegration
{
    /// <summary>只保存还在场景中的单位装备显示，避免每帧重新生成。</summary>
    private static readonly ConditionalWeakTable<Actor, CreatureHeldItem> CreatureItems = new();

    /// <summary>一个单位当前的手握位置、装备和显示实例。</summary>
    private sealed class CreatureHeldItem
    {
        /// <summary>供单位弱引用缓存创建显示记录。</summary>
        public CreatureHeldItem() { }
        /// <summary>装备实际编号；游戏复用物体时也据此重新取外观。</summary>
        public long ItemId;
        /// <summary>装备类型，防止复用装备后沿用旧武器。</summary>
        public string WeaponId;
        /// <summary>当前装备的原图，材质改变时刷新取色。</summary>
        public Sprite Icon;
        /// <summary>原生武器网格的显示实例。</summary>
        public readonly MeshInstance Mesh = new();
        /// <summary>当前距离档的身体网格。</summary>
        public MeshModel Body;
        /// <summary>身体原坐标中的手握点。</summary>
        public Vec3 Palm;
        /// <summary>武器自身坐标中的握柄位置。</summary>
        public Vec3 WeaponGrip;
        /// <summary>决定枪口、斧刃或长柄的朝前方向。</summary>
        public WeaponKind Kind;
        /// <summary>直立生物骨架中代表右臂的部件。</summary>
        public int Arm;
    }

    /// <summary>在身体姿态完成后读取实际装备，再将原生武器网格接到手握点。</summary>
    /// <param name="meshes">当前帧的网格实例。</param>
    /// <param name="a">当前原生单位。</param>
    /// <param name="tag">沿用身体的选择标记。</param>
    /// <param name="__state">本次身体绘制的开始位置。</param>
    /// <param name="__result">原生渲染器是否已经画出身体。</param>
    private static void DrawCreatureHeldItem(List<MeshInstance> meshes, Actor a, int tag, int __state, bool __result)
    {
        if (!__result || a.asset.is_humanoid || !ActorFamilies.ContainsKey(a.asset.id)) return;
        Item item = a.getWeapon();
        if (item == null) return;
        MeshInstance body = null;
        for (int i = __state; i < meshes.Count; i++)
        {
            MeshInstance candidate = meshes[i];
            if (candidate.Tag != tag || candidate.ShadowOnly ||
                !candidate.Model.Name.StartsWith("creature_cultiway_", StringComparison.Ordinal) ||
                !HasCreatureItemHand(candidate.Model)) continue;
            body = candidate;
            break;
        }
        if (body == null || body.Hidden == ulong.MaxValue) return;
        if (body.Model.Info("cultiway_camouflage_state") == "disguised") return;
        if (body.Model.Info("cultiway_fixed_hand_items") == "1") return;

        if (body.Model.Info("cultiway_native_unit") == "1")
        {
            // 原贴图把武器画在身体里；自建三维身体改用实际装备，仍遵守浸水和临时隐藏规则。
            bool submerged = a._is_in_liquid &&
                !(creatureMovements.TryGetValue(a, out CreatureMovement movement) && movement.flying);
            if (!a.canUseItems() || submerged || a.isEgg() || PatchActor.ShouldHideHandItem(a, false)) return;
        }
        else if (!a.checkHasRenderedItem()) return;

        CreatureHeldItem held = CreatureItems.GetOrCreateValue(a);
        Sprite icon = item.getSprite();
        string weaponId = item.getAsset().id;
        if (held.ItemId != item.data.id || held.WeaponId != weaponId || held.Icon != icon)
        {
            SpriteImage image = (SpriteImage)ReadOriginalPersonSprite.Invoke(
                OriginalPersonSprites.GetValue(FirstPerson.WorldBoxMod.Instance), [icon]);
            WeaponLook look = ViewMeshes.LookOf(weaponId, image);
            if (look.Kind == WeaponKind.None) return;
            held.Mesh.Model = ViewMeshes.Item(look);
            held.ItemId = item.data.id;
            held.WeaponId = weaponId;
            held.Icon = icon;
            held.Kind = look.Kind;
            // 长柄靠近下半段握持，原生武器的末端不会穿进地面。
            held.WeaponGrip = new Vec3(0, 0,
                look.Kind is WeaponKind.Staff or WeaponKind.Spear ? held.Mesh.Model.Min.Z * 0.5f : 0);
        }

        if (held.Body != body.Model)
        {
            // 通用生物不经过人物骨架的横向镜像，面向-Y时负X才是实际右侧。
            int arm = body.Model.PartIndex("wing_l");
            if (arm < 0 || body.Model.Parts[arm].VertexCount == 0) return;
            held.Body = body.Model;
            held.Arm = arm;
            held.Palm = FindCreaturePalm(body.Model.Parts[arm]);
        }
        // 手臂略向前持握；原生姿态每次收集前重置，此处不会逐帧累积转角。
        body.PartRotation[held.Arm] = Mat3.RotX(-0.4f) * body.PartRotation[held.Arm];
        Vec3 palm = PersonRig.Transform(body, held.Arm, held.Palm);
        float bodyScale = (float)Math.Sqrt(Vec3.Dot(body.Basis.C2, body.Basis.C2));
        float scale = body.Model.InfoFloat("height", body.Model.Max.Z - body.Model.Min.Z) * bodyScale;
        Mat3 orientation = ForwardHeldItemBasis(body, held.Kind, 1f);
        MeshModel weapon = held.Mesh.Model;
        Vec3 bottom = new(
            orientation.C0.Z < 0 ? weapon.Max.X : weapon.Min.X,
            orientation.C1.Z < 0 ? weapon.Max.Y : weapon.Min.Y,
            orientation.C2.Z < 0 ? weapon.Max.Z : weapon.Min.Z);
        float lowest = orientation.Mul(bottom - held.WeaponGrip).Z;
        if (lowest < 0)
            scale = Math.Min(scale, (palm.Z - body.Position.Z) * 0.95f / -lowest);
        MeshInstance mesh = held.Mesh;
        mesh.Basis = ForwardHeldItemBasis(body, held.Kind, scale);
        mesh.Position = palm - mesh.Basis.Mul(held.WeaponGrip);
        mesh.Tag = body.Tag;
        mesh.Flash = body.Flash;
        mesh.Multiply = body.Multiply;
        mesh.NoShadow = body.NoShadow;
        mesh.SortDepth = body.SortDepth;
        mesh.Fade = body.Fade;
        mesh.KeepSmall = body.KeepSmall;
        meshes.Add(mesh);
    }

    /// <summary>有真实掌臂的鹿人、人马和人鱼也能持物，不能只按双足类别筛选。</summary>
    /// <param name="model">当前实际身体。</param>
    /// <returns>是否具有可用于持物的右手。</returns>
    private static bool HasCreatureItemHand(MeshModel model) => model.Info("kind") == "biped" ||
        model.Info("cultiway_body") is "Centaur" or "Mermaid";

    /// <summary>固定手持物随真实手臂向前持握，不再另画随机装备。</summary>
    /// <param name="mesh">已摆好姿态的身体。</param>
    private static void PoseFixedCreatureItems(MeshInstance mesh)
    {
        if (mesh.Model.Info("cultiway_fixed_hand_items") != "1") return;
        for (int side = 0; side < 2; side++)
        {
            int item = mesh.Model.PartIndex(side == 0 ? "held_item_r" : "held_item_l");
            if (item < 0) continue;
            int arm = mesh.Model.Parts[item].Parent;
            mesh.PartRotation[arm] = Mat3.RotX(-0.4f) * mesh.PartRotation[arm];
        }
    }

    /// <summary>读入时把三类恶魔的腿适当拉长，同时抬高上身和握物轴，脚底保持落地。</summary>
    /// <param name="model">刚从文件读入、尚未交给骨架和绘制器的独立模型。</param>
    internal static void PrepareDaemonProportions(MeshModel model)
    {
        if (model.Info("cultiway_asset") is not ("Cultiway.KeeperSecrets" or "Cultiway.KeeperSecretsNakari" or
            "Cultiway.ExaltedKeeperSecrets" or "Cultiway.LordChange" or "Cultiway.KairosFateweaver" or
            "Cultiway.ExaltedLordChange" or "Cultiway.Bloodthirster" or "Cultiway.AnggrathUnbound" or
            "Cultiway.ExaltedBloodthirster")) return;
        const float stretch = 1.3f;
        float rise = model.Parts[model.PartIndex("leg_l")].Pivot.Z * (stretch - 1f);
        // 先改真实顶点和法线，再同步所有关节；只在读入时执行一次，不增加逐帧扫描。
        foreach (MeshPart part in model.Parts)
        {
            bool leg = part.Name is "leg_l" or "leg_r";
            for (int i = 0; i < part.Pos.Length; i += 3)
            {
                part.Pos[i + 2] = leg ? part.Pos[i + 2] * stretch : part.Pos[i + 2] + rise;
                if (!leg) continue;
                Vec3 normal = new Vec3(part.Nrm[i], part.Nrm[i + 1], part.Nrm[i + 2] / stretch).Normalized();
                part.Nrm[i] = normal.X;
                part.Nrm[i + 1] = normal.Y;
                part.Nrm[i + 2] = normal.Z;
            }
            part.Pivot += new Vec3(0, 0, rise);
        }
        model.Max += new Vec3(0, 0, rise);
        // height 沿用原贴图的显示比例基准，避免拉长腿后反向缩小整个角色。
        WorldfallMeshes.SetInfo(model, "cultiway_leg_stretch=1.3");
    }

    /// <summary>按真实躯干宽度把恶魔双腿移回髋部下方，不再沿用翼类种子的后置腿位。</summary>
    /// <param name="mesh">已完成原生动作的实际身体。</param>
    private static void AlignDaemonLegs(MeshInstance mesh)
    {
        float halfWidth = mesh.Model.Info("cultiway_asset") switch
        {
            "Cultiway.GreatUncleanOneButcher" or "Cultiway.GreatUncleanOneBellRinger" or
                "Cultiway.GreatUncleanOneRainFather" => 0.1f,
            "Cultiway.KeeperSecrets" or "Cultiway.KeeperSecretsNakari" or
                "Cultiway.ExaltedKeeperSecrets" => 0.09f,
            "Cultiway.LordChange" or "Cultiway.KairosFateweaver" or
                "Cultiway.ExaltedLordChange" => 0.075f,
            "Cultiway.Bloodthirster" or "Cultiway.ExaltedBloodthirster" => 0.045f,
            "Cultiway.AnggrathUnbound" => 0.06f,
            _ => 0f
        };
        if (halfWidth == 0f) return;
        for (int side = 0; side < 2; side++)
        {
            int leg = mesh.Model.PartIndex(side == 0 ? "leg_l" : "leg_r");
            if (leg < 0) continue;
            Vec3 pivot = mesh.Model.Parts[leg].Pivot;
            // 只移左右和前后，保持脚底高度；偏移在原髋轴转动之后应用，迈步仍围绕新髋位。
            mesh.PartOffset[leg] += new Vec3((side == 0 ? -halfWidth : halfWidth) - pivot.X, -pivot.Y, 0);
        }
    }

    /// <summary>让自建人物待机持物朝前，攻击时仍采用原生的挥动姿态。</summary>
    /// <param name="__instance">已经摆好左右手臂的人物骨架。</param>
    /// <param name="body">实际人物身体。</param>
    /// <param name="kind">当前手持物类型。</param>
    /// <param name="attack">原生攻击或工作动作进度。</param>
    /// <param name="height">原生装备显示比例。</param>
    /// <param name="position">实际右侧掌心。</param>
    /// <param name="basis">朝前的装备方向。</param>
    /// <param name="__result">原生入口是否找到手握点。</param>
    private static void FacePersonHeldItemForward(PersonRig __instance, MeshInstance body,
        WeaponKind kind, float attack, float height, ref Vec3 position, ref Mat3 basis, bool __result)
    {
        if (!__result || attack > 0f ||
            !body.Model.Name.StartsWith("person_cultiway_", StringComparison.Ordinal)) return;
        position = __instance.HandWorld(body);
        basis = ForwardHeldItemBasis(body, kind, Math.Max(0.3f, Math.Min(3f, height)));
    }

    /// <summary>以身体前方建立握物方向，枪口朝前，长矛前伸，其余长柄略向前上方。</summary>
    /// <param name="body">用于确定实际面向的身体。</param>
    /// <param name="kind">装备形状。</param>
    /// <param name="scale">保持原有装备大小。</param>
    /// <returns>正交且不镜像的装备坐标。</returns>
    private static Mat3 ForwardHeldItemBasis(MeshInstance body, WeaponKind kind, float scale)
    {
        Vec3 forward = body.Basis.Mul(new Vec3(0, -1, 0)).Normalized();
        Vec3 up = body.Basis.C2.Normalized();
        Vec3 right = Vec3.Cross(forward, up).Normalized();
        Vec3 blade = kind switch
        {
            WeaponKind.Spear => (forward + up * 0.28f).Normalized(),
            WeaponKind.Staff => (up + forward * 0.25f).Normalized(),
            WeaponKind.Gun or WeaponKind.Shotgun or WeaponKind.Bow => up,
            _ => (up + forward * 0.55f).Normalized()
        };
        return new Mat3(right * scale, Vec3.Cross(blade, right) * scale, blade * scale);
    }

    /// <summary>从真实手臂末端取握点；每次切换距离档才计算一次。</summary>
    /// <param name="part">直立生物实际右侧的垂臂网格。</param>
    /// <returns>末端掌部内的握点。</returns>
    private static Vec3 FindCreaturePalm(MeshPart part)
    {
        float low = float.MaxValue, high = float.MinValue;
        for (int i = 0; i < part.VertexCount; i++)
        {
            low = Math.Min(low, part.Pos[i * 3 + 2]);
            high = Math.Max(high, part.Pos[i * 3 + 2]);
        }
        float cut = low + (high - low) * 0.12f, x = 0, y = 0;
        int count = 0;
        for (int i = 0; i < part.VertexCount; i++)
            if (part.Pos[i * 3 + 2] <= cut)
            {
                x += part.Pos[i * 3];
                y += part.Pos[i * 3 + 1];
                count++;
            }
        return new Vec3(x / count, y / count, cut);
    }
}
#endif
