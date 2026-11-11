using System;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Cultiway.Core.Skills;
using HarmonyLib;
using UnityEngine;

namespace Cultiway.Content.Compatibility.Worldfall;

/// <summary>自然动作与共同表面共用原有完整姿态生成，镜头只提交已经采集的结果。</summary>
internal static partial class BodyWorldfallRenderer
{
    /// <summary>原有完整人物姿态及实际绘制时的命中部位登记。</summary>
    private static MethodInfo naturalPeople, recordHitboxes;

    /// <summary>独立采集时不递归套用显示接管，也不登记尚未绘制的命中部位。</summary>
    private static bool samplingPeople;

    /// <summary>人物卡片和第一人称身体有自己的生成入口。</summary>
    private static FieldInfo asHost, peopleFrame, figureDrawn, figureSeen;

    /// <summary>模板的显示字段一次绑定，提交模型时不逐个查找。</summary>
    private static FieldInfo[] naturalDisplayFields;

    /// <summary>归还发生在同一世界时点时，普通绘制不再重复推进刚采集过的动作。</summary>
    private static ConditionalWeakTable<Actor, NaturalStamp> naturalStamps = new();

    /// <summary>仅记录动作时间，不延长人物或世界资源的寿命。</summary>
    private sealed class NaturalStamp
    {
        /// <summary>对象池中的实际身份及本次生成的世界时间。</summary>
        internal long Id;

        /// <summary>已经推进的世界时点。</summary>
        internal double Time;

        /// <summary>原版实际触发受击显示的会话时间，离屏也能采集。</summary>
        internal double HitAt = double.NegativeInfinity;
    }

    /// <summary>原版会复用人物对象；新身份不继承上一个人物的动作或受击时点。</summary>
    /// <param name="actor">当前实际人物。</param>
    private static NaturalStamp NaturalState(Actor actor)
    {
        NaturalStamp stamp = naturalStamps.GetOrCreateValue(actor);
        if (stamp.Id != actor.id)
        {
            stamp.Id = actor.id;
            stamp.Time = stamp.HitAt = double.NegativeInfinity;
        }
        return stamp;
    }

    /// <summary>绑定已经核对过的完整入口。</summary>
    /// <param name="assembly">实际外部模组。</param>
    private static void InstallNaturalPose(Assembly assembly)
    {
        Type people = assembly.GetType("FirstPerson.People", true);
        naturalPeople = AccessTools.Method(people, "AddMesh");
        asHost = AccessTools.Field(people, "_asHost");
        peopleFrame = AccessTools.Field(people, "_frame");
        figureDrawn = AccessTools.Field(bodyMesh.DeclaringType, "Drawn");
        figureSeen = AccessTools.Field(bodyMesh.DeclaringType, "Seen");
        recordHitboxes = AccessTools.Method(assembly.GetType("FirstPerson.Hitboxes", true), "Record");
        naturalDisplayFields = Array.ConvertAll(itemDisplayFields,
            field => AccessTools.Field(naturalPeople.GetParameters()[13].ParameterType, field.Name));
        new Harmony("cultiway.body.natural.worldfall").Patch(recordHitboxes,
            prefix: new HarmonyMethod(typeof(BodyWorldfallRenderer), nameof(BeforeNaturalHitboxes)));
        new Harmony("cultiway.body.natural.worldfall").Patch(AccessTools.Method(typeof(Actor), nameof(Actor.startColorEffect)),
            prefix: new HarmonyMethod(typeof(BodyWorldfallRenderer), nameof(ObserveNaturalHit)));
    }

    /// <summary>原版的受击图片队列只记录可见人物；动作采集从真实触发入口取时点。</summary>
    /// <param name="__instance">实际受击人物。</param><param name="__0">原版请求的显示种类。</param>
    private static void ObserveNaturalHit(Actor __instance, ActorColorEffect __0)
    {
        if (__0 != ActorColorEffect.Red || !__instance.asset.effect_damage || __instance.isUnderDamageCooldown()) return;
        NaturalStamp stamp = NaturalState(__instance);
        stamp.HitAt = World.world.getCurSessionTime();
    }

    /// <summary>只在实际提交画面时登记外部命中部位。</summary>
    private static bool BeforeNaturalHitboxes() => !samplingPeople;

    /// <summary>复用原人物的自然动作状态，独立网格只用于构造动作的数值变换。</summary>
    private sealed partial class SurfaceBinding
    {
        /// <summary>本轮完整自然身体及附属物，列表随该身体绑定释放。</summary>
        private readonly IList naturalMeshes = (IList)Activator.CreateInstance(naturalPeople.GetParameters()[0].ParameterType);

        /// <summary>原生持物图片等非网格显示。</summary>
        private readonly IList naturalVoxels = (IList)Activator.CreateInstance(naturalPeople.GetParameters()[1].ParameterType);

        /// <summary>离屏采集的显示属性，实际绘制时恢复本轮模板。</summary>
        private readonly object naturalTemplate = Activator.CreateInstance(naturalPeople.GetParameters()[13].ParameterType);

        /// <summary>完整自然姿态只推进一次；不调用场景收集，也不推进游戏人物。</summary>
        /// <param name="foot">共同脚下。</param><param name="height">共同体高。</param><param name="time">世界时间。</param>
        private BodySurfacePose SampleNatural(Vector3 foot, float height, double time)
        {
            // 先取得实际移动与受击，再只推进一次原生姿态，最后采集身体和持物的共同矩阵。
            if (IsPinned && To != null) return To;
            float dt = To == null ? 0f : (float)(time - ToTime);
            Vector3 delta = To == null ? Vector3.zero : foot - To.Foot;
            float speed = dt > 0f ? new Vector2(delta.x, delta.y).magnitude / dt : 0f;
            if (speed > .00001f) heading = Mathf.Atan2(delta.y, delta.x);
            object sprites = externalSprites.GetValue(externalInstance.GetValue(null));
            WorldfallVisualBridge.PrepareSprites(sprites);
            object image = spriteImage.Invoke(sprites, new object[] { Actor.getSpriteToRender() });
            Vector3 root = swimming ? WorldfallVisualBridge.SwimmingRoot(Actor, foot) : WorldfallVisualBridge.SurfaceRoot(Actor, foot);
            float hitAge = -1f;
            foreach (ActorDamageEffectData hit in World.world.stack_effects.actor_effect_hit)
            {
                if (hit.actor != Actor) continue;
                float age = World.world.getRealTimeElapsedSince(hit.timestamp);
                if (age >= 0f && age <= .3f && (hitAge < 0f || age < hitAge)) hitAge = age;
            }
            NaturalStamp stamp = NaturalState(Actor);
            float observedAge = World.world.getRealTimeElapsedSince(stamp.HitAt);
            if (observedAge >= 0f && observedAge <= .3f && (hitAge < 0f || observedAge < hitAge)) hitAge = observedAge;

            naturalMeshes.Clear();
            naturalVoxels.Clear();
            // 原缓存可在长时间暂停后清理，绑定期间仍由同一实际模型承接自然动作。
            ((IDictionary)figures.GetValue(null))[Actor] = figure;
            // 原入口的离屏判定基于镜头帧；这里已有持续采集，不能因此反复重置原步态。
            figureDrawn.SetValue(figure, peopleFrame.GetValue(null));
            armourChecked.SetValue(figure, -1000);
            samplingPeople = true;
            try
            {
                naturalPeople.Invoke(null, new object[]
                {
                    naturalMeshes, naturalVoxels, Actor, image, sprites, root.x, root.y, root.z,
                    height * HeightScale, heading, swimming ? .5f : speed, 0f, dt, naturalTemplate,
                    swimming, hitAge, dt, 0f, 0f
                });
            }
            finally { samplingPeople = false; }

            stamp.Time = World.world.getCurWorldTime();
            object actual = bodyMesh.GetValue(figure);
            int count = held.Count == 0 ? nativeParts.Length : held[held.Count - 1].Start + held[held.Count - 1].Parts.Length;
            var matrices = new Matrix4x4[count];
            for (int part = 0; part < nativeParts.Length; part++) matrices[part] = WorldMatrix(actual, part);
            BodySurfacePose result = CaptureHeldSurface(matrices, foot, height, heading, true);
            PlaceSurface(root, height);
            ApplySurfaceMatrices(mesh, result, Vector3.zero);
            return result;
        }

        /// <summary>提交本轮采集结果，原入口不在镜头中再次推进；外部拾取和着色仍用实际模板。</summary>
        /// <param name="meshes">实际网格队列。</param><param name="voxels">实际图片队列。</param><param name="template">本轮显示属性。</param>
        internal void SubmitNatural(IList meshes, IList voxels, object template)
        {
            ((IDictionary)figures.GetValue(null))[Actor] = figure;
            figureSeen.SetValue(figure, peopleFrame.GetValue(null));
            figureDrawn.SetValue(figure, peopleFrame.GetValue(null));
            foreach (object instance in naturalMeshes)
            {
                for (int field = 0; field < itemDisplayFields.Length; field++)
                    itemDisplayFields[field].SetValue(instance, naturalDisplayFields[field].GetValue(template));
                meshes.Add(instance);
            }
            foreach (object instance in naturalVoxels) voxels.Add(instance);
        }
    }
}
