using System.Collections.Generic;
using Cultiway.Content.Combat;
using Cultiway.Core.SkillLibV3.Effects;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;

namespace Cultiway.Content.Skills;

public sealed partial class ConstructedAbilityService
{
    /// <summary>同一批真实变化留下的短暂画面，不参与接触或存档。</summary>
    private static readonly List<TimeWear> timeWear = new(32);

    /// <summary>实际时序痕迹对应的人物、护甲、植被或石材。</summary>
    private enum WearKind { Age, Armor, Plant, Stone, Sand }

    /// <summary>一次已结算变化的身份、原位置、强度和时间。</summary>
    private readonly struct TimeWear
    {
        /// <summary>需随身绘制的原人物及事件时的身份。</summary>
        internal readonly Actor Actor;
        internal readonly long Id;
        /// <summary>地物实际发生变化的位置和类别。</summary>
        internal readonly Vector3 Ground;
        internal readonly WearKind Kind;
        /// <summary>实际损耗决定的画面强度及确认时点。</summary>
        internal readonly float Strength;
        internal readonly double At;

        /// <summary>只记录已发生的变化。</summary>
        /// <param name="actor">原人物，地物为空。</param><param name="id">原人物编号。</param>
        /// <param name="ground">地物实际位置。</param><param name="kind">实际变化类别。</param>
        /// <param name="strength">实际损耗决定的强度。</param><param name="at">确认时点。</param>
        internal TimeWear(Actor actor, long id, Vector3 ground, WearKind kind, float strength, double at)
        {
            Actor = actor; Id = id; Ground = ground; Kind = kind; Strength = strength; At = at;
        }
    }

    /// <summary>仅在实际变化后保留一条可绘制资料，限制密集接触产生的积累。</summary>
    /// <param name="mark">当前实际变化。</param>
    private static void AddTimeWear(in TimeWear mark)
    {
        if (timeWear.Count == 32) timeWear.RemoveAt(0);
        timeWear.Add(mark);
    }

    /// <summary>地物只在原生命、植被或原地块真实变化后给出相应反馈。</summary>
    /// <param name="tile">已经结算的实际地块。</param>
    /// <param name="result">已确认结果。</param>
    /// <param name="now">实际接触时点。</param>
    private static void ShowTimeWear(WorldTile tile, in SkillEffectResult result, double now)
    {
        if ((result.Flags & SkillEffectOutcomeFlags.FloraCreated) != 0 && tile.building is Building grown)
            AddPlantRepair(grown, grown.getMaxHealth(), now);
        if ((result.Flags & SkillEffectOutcomeFlags.FloraWithered) != 0)
            AddTimeWear(new TimeWear(null, 0, tile.posV3, WearKind.Plant, .85f, now));
        if ((result.Flags & SkillEffectOutcomeFlags.StoneWeathered) != 0)
        {
            WearKind kind = (result.Flags & SkillEffectOutcomeFlags.WeatheredSand) != 0 ? WearKind.Sand : WearKind.Stone;
            AddTimeWear(new TimeWear(null, 0, tile.posV3, kind, Mathf.Clamp(result.Magnitude / 90f, .46f, 1.4f), now));
        }
    }

    /// <summary>绘制附身的旧痕和落向原地面的木叶、铁屑及砂砾。</summary>
    /// <param name="now">已经确认的共同世界时间。</param>
    private void AppendTimeWear(double now)
    {
        for (int i = timeWear.Count - 1; i >= 0; i--)
        {
            TimeWear mark = timeWear[i];
            float phase = (float)((now - mark.At) / (mark.Kind == WearKind.Age ? 1.18f : .81f));
            Actor actor = mark.Actor;
            if (phase >= 1f || actor != null && (actor.data?.id != mark.Id || !actor.isAlive()))
            {
                timeWear.RemoveAt(i);
                continue;
            }
            if (phase < 0f) continue;

            Vector3 origin = actor != null ? SpatialActorPose.Foot(actor) : mark.Ground;
            origin.z += terrain?.Sample(origin.x, origin.y) ?? 0f;
            float height = actor != null ? SpatialActorPose.Height(actor) * .69f : 1.2f;
            float opacity = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.46f, 1f, phase));
            if (mark.Kind == WearKind.Plant)
            {
                Vector3 scale = new(1.88f, 1.88f, Mathf.Lerp(1.55f, .30f, phase));
                var pose = Matrix4x4.TRS(origin, Quaternion.Euler(phase * 16f, phase * 36f, 9f), scale);
                visuals.Add(new FormVisualInstance(FormResources.Load("life_wood_graft"), pose,
                    dye: new Color(.91f, .50f, .19f, opacity), terrain: terrain));
            }
            else
                AppendTimeFragments(mark, origin, height, phase, opacity);
        }
    }

    /// <summary>用有厚度的旧印、铁屑与残石表现已失去的部分。</summary>
    /// <param name="mark">本次真实变化。</param>
    /// <param name="origin">当前脚下或原地块。</param>
    /// <param name="height">在实际对象上的起点高度。</param>
    /// <param name="phase">该短暂画面进度。</param>
    /// <param name="opacity">画面离开时的可见比例。</param>
    private void AppendTimeFragments(in TimeWear mark, Vector3 origin, float height, float phase, float opacity)
    {
        bool age = mark.Kind == WearKind.Age;
        FormResource model = FormResources.Load(age ? "time_fold_imprint" : "rock_projectile");
        Color dye = mark.Kind switch
        {
            WearKind.Age => new Color(.78f, .61f, .31f, opacity),
            WearKind.Armor => new Color(.86f, .41f, .17f, opacity),
            WearKind.Sand => new Color(1f, .80f, .48f, opacity),
            _ => new Color(.86f, .77f, .64f, opacity)
        };
        string surface = mark.Kind == WearKind.Armor ? "iron_crust" : null;
        int count = age ? 2 : 4;
        for (int i = 0; i < count; i++)
        {
            float angle = i * 2.399963f + .7f;
            Vector3 side = new(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            float spread = age ? .44f + .39f * phase : .26f + phase * (1.05f + .11f * i);
            Vector3 point = origin + side * (spread * mark.Strength);
            point.z += age ? height + .43f * i + .61f * phase :
                Mathf.Lerp(height + .22f * i, .05f, phase * phase);
            float size = (age ? .44f : .38f - .044f * i) * mark.Strength * Mathf.Lerp(1f, .66f, phase);
            var pose = Matrix4x4.TRS(point, Quaternion.Euler(28f + phase * 124f, angle * 64f, i * 53f + phase * 121f),
                new Vector3(size, size * 1.18f, size * (mark.Kind == WearKind.Armor ? .43f : .88f)));
            visuals.Add(new FormVisualInstance(model, pose, surface: surface, dye: dye, terrain: terrain));
        }
    }
}
