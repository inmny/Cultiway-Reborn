#if S3COND2_WORLDFALL
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using FirstPerson.Core;
using UnityEngine;

namespace Cultiway.Content.Worldfall;

/// <summary>按实际移动切换兽形，并让有翼单位离地飞行。</summary>
public sealed partial class WorldfallModelIntegration
{
    /// <summary>移动时使用另一套身体的单位。</summary>
    private static readonly HashSet<string> movingForms = new(StringComparer.Ordinal)
    {
        "Cultiway.Werewolf", "Cultiway.Bloodsucker", "Cultiway.Bloodthirsty",
        "Cultiway.BloodBeast", "Cultiway.JinWu"
    };

    /// <summary>只跟随仍然存在的单位保存飞行显示状态。</summary>
    private static readonly ConditionalWeakTable<Actor, CreatureMovement> creatureMovements = new();
    /// <summary>把实际绘制实例与该单位的起落状态相连。</summary>
    private static readonly ConditionalWeakTable<MeshInstance, CreatureMovement> movementMeshes = new();
    /// <summary>沿用渲染器计算原贴图有效宽度的入口。</summary>
    private static Func<SpriteImage, float> opaqueCreatureWidth;
    /// <summary>人物面板的临时预览不改变世界里的起落记录。</summary>
    private static bool drawingCreatureFigure;
    /// <summary>各种身体实际可能使用的脚或爪。</summary>
    private static readonly string[] flightLegs =
    [
        "leg_l", "leg_r", "leg_fl", "leg_fr", "leg_bl", "leg_br",
        "leg_1", "leg_2", "leg_3", "leg_4", "leg_5", "leg_6", "leg_7", "leg_8"
    ];

    /// <summary>一个单位本次移动、起落和实际模型的显示记录。</summary>
    private sealed class CreatureMovement
    {
        /// <summary>供弱引用表建立显示记录。</summary>
        public CreatureMovement() { }
        /// <summary>是否正在移动。</summary>
        public bool moving;
        /// <summary>是否已经离地。</summary>
        public bool flying;
        /// <summary>平滑起落的高度。</summary>
        public float lift;
        /// <summary>含轻微悬浮起伏的实际绘制高度。</summary>
        public float drawnLift;
        /// <summary>连续推进的悬浮周期，停下时放慢，不因启停突然跳动。</summary>
        public float hoverPhase;
        /// <summary>当前离水飞行是否由背包推动。</summary>
        public bool jetpack;
        /// <summary>仅在水面喷气时绘制的双喷口尾焰。</summary>
        public readonly MeshInstance exhaust = new();
        /// <summary>本次实际身体高度，供水面附身视角使用。</summary>
        public float tall;
        /// <summary>当前模型实例，切换身体时重新关联。</summary>
        public MeshInstance mesh;
    }

    /// <summary>只在移动时选用已发布的狼形或蝙蝠形。</summary>
    /// <param name="actor">实际单位。</param>
    /// <param name="family">原站立身体。</param>
    /// <returns>本次应画出的身体族。</returns>
    private static string MovingCreatureFamily(Actor actor, string family)
    {
        if (!movingForms.Contains(actor.asset.id)) return family;
        bool moving = actor.is_moving ||
            (creatureMovements.TryGetValue(actor, out CreatureMovement state) && state.moving);
        string form = family + "_moving";
        return moving && CustomModels.ContainsKey(form) ? form : family;
    }

    /// <summary>识别真正带翅膀的身体，蝙蝠群由各只蝙蝠分别振翅。</summary>
    /// <param name="model">当前身体。</param>
    /// <returns>是否采用飞行移动。</returns>
    private static bool HasFlightMovement(MeshModel model)
    {
        // 变身单位的站立原形不在空中迈步，飞行由移动身体承担。
        if (movingForms.Contains(model.Info("cultiway_asset")) && model.Info("cultiway_move_form") == null) return false;
        return model.Info("cultiway_move_form") == "bat_swarm" || model.Info("flap") == "1" ||
            (model.Info("kind") == "insect" && model.PartIndex("wing_l") >= 0);
    }

    /// <summary>按身体与翼展留出空间，避免下拍翅膀扫进地面。</summary>
    /// <param name="model">实际身体。</param>
    /// <param name="scale">本次显示比例。</param>
    /// <returns>身体脚底相对地面的飞行高度。</returns>
    private static float CreatureFlightHeight(MeshModel model, float scale)
    {
        float tall = model.InfoFloat("height", model.Max.Z - model.Min.Z);
        float span = Math.Max(model.Max.X - model.Min.X, model.Max.Y - model.Min.Y);
        return Math.Max(0.35f, Math.Max(tall * 0.65f, span * 0.4f) * scale + 0.2f);
    }

    /// <summary>先记住本次网格起点，再确定移动形态并平滑起落。</summary>
    /// <param name="meshes">本帧网格。</param>
    /// <param name="a">实际单位。</param>
    /// <param name="img">本次原图。</param>
    /// <param name="spriteW">原图显示宽度。</param>
    /// <param name="spriteH">原图显示高度。</param>
    /// <param name="speed">本次实际移动速度，包含附身移动。</param>
    /// <param name="dt">本帧时间。</param>
    /// <param name="z">本次地面高度，起飞后加上离地距离。</param>
    /// <param name="swimming">飞行中不叠加沉水姿态。</param>
    /// <param name="__state">本次加入身体之前的网格数量。</param>
    private static void PrepareCreatureFrame(List<MeshInstance> meshes, Actor a, SpriteImage img,
        float spriteW, float spriteH, float speed, float dt, ref float z, ref bool swimming, out int __state)
    {
        __state = meshes.Count;
        if (drawingCreatureFigure || a.asset.is_humanoid || !ActorFamilies.TryGetValue(a.asset.id, out string family)) return;
        MeshModel standing = CustomModels[family];
        bool floating = UsesFloatingMovement(a.asset.id);
        bool jetpack = UsesWaterJetpack(a.asset.id);
        if (!movingForms.Contains(a.asset.id) && !HasFlightMovement(standing) && !floating && !jetpack) return;
        CreatureMovement state = creatureMovements.GetOrCreateValue(a);
        state.moving = a.is_moving || speed > 0.01f;
        MeshModel model = CustomModels[MovingCreatureFamily(a, family)];
        float height = model.InfoFloat("height", model.Max.Z - model.Min.Z);
        float length = model.InfoFloat("length", model.Max.Y - model.Min.Y);
        float scale = Math.Max(spriteH * img.VisibleFraction, spriteW * opaqueCreatureWidth(img) * 0.9f) /
            Math.Max(0.05f, Math.Max(height, length));
        bool flying = ((HasFlightMovement(model) || floating) && (state.moving || a.isFlying()) ||
            jetpack && (swimming || a._is_in_liquid)) &&
            !a.is_in_magnet && a.isAlive();
        float target = flying ? (floating || jetpack ? Math.Max(0.25f, height * scale * 0.3f + 0.15f) :
            CreatureFlightHeight(model, scale)) : 0f;
        state.lift = Mathf.MoveTowards(state.lift, target, Math.Max(0.6f, height * scale * 3f) * dt);
        state.flying = state.lift > 0.01f;
        state.jetpack = jetpack && state.flying;
        state.hoverPhase = Mathf.Repeat(state.hoverPhase + dt * (state.moving && !floating ? 2.4f : 0.8f), Mathf.PI * 2f);
        state.drawnLift = state.lift + (flying ? 0.025f * scale * Math.Min(1f, state.lift / target) *
            Mathf.Sin(state.hoverPhase + (a.GetHashCode() & 255) * 0.03f) : 0f);
        state.tall = height * scale;
        // 收集器已把水中贴图下沉四成；起飞时先恢复到水面，再加入飞行高度。
        if (state.flying && swimming) z += spriteH * img.VisibleFraction * 0.4f;
        z += state.drawnLift;
        if (state.flying) swimming = false;
    }

    /// <summary>关联身体与起落记录，再在可见掌心加入实际装备。</summary>
    /// <param name="meshes">本帧网格。</param>
    /// <param name="a">实际单位。</param>
    /// <param name="tag">选择标记。</param>
    /// <param name="__state">本次网格的开始位置。</param>
    /// <param name="__result">是否成功画出身体。</param>
    private static void FinishCreatureFrame(List<MeshInstance> meshes, Actor a, int tag, int __state, bool __result)
    {
        if (__result && !drawingCreatureFigure && creatureMovements.TryGetValue(a, out CreatureMovement state))
            for (int i = __state; i < meshes.Count; i++)
            {
                MeshInstance mesh = meshes[i];
                if (mesh.ShadowOnly || !mesh.Model.Name.StartsWith("creature_cultiway_", StringComparison.Ordinal)) continue;
                if (!ReferenceEquals(state.mesh, mesh))
                {
                    state.mesh = mesh;
                    movementMeshes.Add(mesh, state);
                }
                if (state.jetpack) DrawCreatureJetpack(meshes, a.asset.id, mesh, state.exhaust, state.hoverPhase);
                break;
            }
        DrawCreatureHeldItem(meshes, a, tag, __state, __result);
    }

    /// <summary>标记原生面板开始临时绘制，不能用与附身角色共用的选择标记判断。</summary>
    /// <param name="__state">进入前是否已经在画预览。</param>
    private static void EnterCreatureFigure(out bool __state)
    {
        __state = drawingCreatureFigure;
        drawingCreatureFigure = true;
    }

    /// <summary>面板绘制结束后恢复现场，原生异常仍按原样传播。</summary>
    /// <param name="__state">进入前的预览状态。</param>
    private static void LeaveCreatureFigure(bool __state) => drawingCreatureFigure = __state;

    /// <summary>把附加的离地高度一并交给原生附身相机。</summary>
    /// <param name="a">实际单位。</param>
    /// <param name="lift">原生记录的悬浮高度。</param>
    /// <param name="__result">是否已有实际绘制记录。</param>
    private static void ReportCreatureLift(Actor a, ref float lift, bool __result)
    {
        if (__result && creatureMovements.TryGetValue(a, out CreatureMovement state)) lift += state.drawnLift;
    }

    /// <summary>飞行身体不再使用贴地脚步与地面姿态。</summary>
    /// <param name="a">实际单位。</param>
    /// <param name="__result">原生判断结果。</param>
    private static void ReportCreatureAirborne(Actor a, ref bool __result)
    {
        if (a != null && creatureMovements.TryGetValue(a, out CreatureMovement state) && state.flying) __result = true;
    }

    /// <summary>狼形采用前后成组的奔跑步态，不沿用直立摆臂。</summary>
    /// <param name="mesh">实际狼形。</param>
    /// <param name="phase">由实际移动距离推进的步态周期。</param>
    private static void PoseRunningWolf(MeshInstance mesh, float phase)
    {
        SetCreatureRotation(mesh, "leg_fl", Mat3.RotX(0.7f * Mathf.Sin(phase)));
        SetCreatureRotation(mesh, "leg_fr", Mat3.RotX(0.7f * Mathf.Sin(phase + 0.25f)));
        SetCreatureRotation(mesh, "leg_bl", Mat3.RotX(0.75f * Mathf.Sin(phase + 2.7f)));
        SetCreatureRotation(mesh, "leg_br", Mat3.RotX(0.75f * Mathf.Sin(phase + 2.95f)));
        int body = mesh.Model.PartIndex("body");
        mesh.PartOffset[body] = new Vec3(0, 0, 0.025f * (1f + Mathf.Sin(phase * 2f)));
    }

    /// <summary>按鸟、龙、蝙蝠、昆虫与飞鲨分别振翅，离地后收腿。</summary>
    /// <param name="mesh">实际身体。</param>
    /// <param name="stride">当前移动幅度。</param>
    /// <param name="time">动作时间。</param>
    /// <param name="nativeFlying">原生飞行标记。</param>
    /// <param name="seed">个体相位。</param>
    private static void PoseFlyingCreature(MeshInstance mesh, float stride, float time, bool nativeFlying, float seed)
    {
        MeshModel model = mesh.Model;
        string asset = model.Info("cultiway_asset");
        bool floating = UsesFloatingMovement(asset);
        bool jetpack = UsesWaterJetpack(asset);
        bool winged = HasFlightMovement(model);
        if (!winged && !floating && !jetpack) return;
        bool flying = (winged || floating) && (nativeFlying || stride > 0.01f) ||
            (movementMeshes.TryGetValue(mesh, out CreatureMovement state) && state.flying);
        if (floating || jetpack)
        {
            if (flying)
            {
                int root = model.PartIndex("body");
                if (root >= 0) mesh.PartOffset[root] = new Vec3(0, 0, 0);
                foreach (string leg in flightLegs)
                    SetCreatureRotation(mesh, leg, floating ? Mat3.Identity : Mat3.RotX(0.3f));
                if (jetpack)
                {
                    SetCreatureRotation(mesh, "wing_l", Mat3.RotX(-0.18f));
                    SetCreatureRotation(mesh, "wing_r", Mat3.RotX(-0.18f));
                }
            }
            return;
        }
        string kind = model.Info("kind");
        bool backWings = model.Info("cultiway_back_wings") == "1";
        float frequency = kind is "bat" or "swarm" ? 9f : kind == "insect" ? 28f :
            kind == "dragon" ? 3.5f : kind == "fish" ? 3f : backWings ? 7f : 5.5f;
        if (model.Info("cultiway_asset") == "Cultiway.BloodBeast") frequency = 5f;
        float amplitude = kind is "bat" or "swarm" ? 0.85f : kind == "fish" ? 0.3f : kind == "insect" ? 0.5f : 0.7f;
        float angle = flying ? 0.08f + amplitude * Mathf.Sin(time * frequency + seed) :
            (kind == "insect" ? 0.2f : 0.05f * Mathf.Sin(time * 1.3f + seed));
        string left = backWings ? "wing_l2" : "wing_l";
        string right = backWings ? "wing_r2" : "wing_r";
        if (model.Info("cultiway_folded_wings") == "1")
        {
            SetCreatureRotation(mesh, left, flying ? Mat3.RotY(angle) * Mat3.RotZ(Mathf.PI * 0.5f) : Mat3.Identity);
            SetCreatureRotation(mesh, right, flying ? Mat3.RotY(-angle) * Mat3.RotZ(-Mathf.PI * 0.5f) : Mat3.Identity);
        }
        else
        {
            SetCreatureRotation(mesh, left, Mat3.RotY(angle));
            SetCreatureRotation(mesh, right, Mat3.RotY(-angle));
        }
        if (!flying) return;
        int sun = model.PartIndex("sun_ring");
        if (sun >= 0)
        {
            mesh.PartRotation[sun] = Mat3.RotY(time * 0.8f + seed);
            mesh.PartScale[sun] = 1f + 0.025f * Mathf.Sin(time * 7f + seed);
        }
        int body = model.PartIndex("body");
        if (body >= 0) mesh.PartOffset[body] = new Vec3(0, 0, 0);
        SetCreatureRotation(mesh, "head", Mat3.Identity);
        foreach (string leg in flightLegs)
            SetCreatureRotation(mesh, leg, Mat3.RotX(0.8f));
        if (backWings)
        {
            SetCreatureRotation(mesh, "wing_l", Mat3.RotX(-0.18f));
            SetCreatureRotation(mesh, "wing_r", Mat3.RotX(-0.18f));
        }
        if (model.Info("cultiway_move_form") == "bat_swarm")
            for (int i = 0; i < model.Parts.Length; i++)
            {
                string name = model.Parts[i].Name;
                if (!name.StartsWith("bat_", StringComparison.Ordinal)) continue;
                float phase = time * frequency + seed + (name[4] - '0') * 0.85f;
                if (name.EndsWith("_wing_l", StringComparison.Ordinal)) mesh.PartRotation[i] = Mat3.RotY(0.85f * Mathf.Sin(phase));
                else if (name.EndsWith("_wing_r", StringComparison.Ordinal)) mesh.PartRotation[i] = Mat3.RotY(-0.85f * Mathf.Sin(phase));
                else if (name.EndsWith("_body", StringComparison.Ordinal)) mesh.PartOffset[i] = new Vec3(0, 0, 0.025f * Mathf.Sin(phase * 0.4f));
            }
    }

    /// <summary>只调整本身体真实存在的部件。</summary>
    /// <param name="mesh">当前身体。</param>
    /// <param name="part">部件名称。</param>
    /// <param name="rotation">以原转轴为中心的动作。</param>
    private static void SetCreatureRotation(MeshInstance mesh, string part, Mat3 rotation)
    {
        int index = mesh.Model.PartIndex(part);
        if (index >= 0) mesh.PartRotation[index] = rotation;
    }
}
#endif
