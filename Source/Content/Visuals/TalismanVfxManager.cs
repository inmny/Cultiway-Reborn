using System.Collections.Generic;
using Cultiway.Content.Combat;
using Cultiway.Core.Components;
using Cultiway.Core.Semantics;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Visuals;
using Cultiway.Core.Visuals;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Visuals;

/// <summary>符箓从实际载体处显出纸符、燃解和灵流，法术本身继续使用原有入口。</summary>
public static class TalismanVfxManager
{
    /// <summary>当前世界短暂燃解的纸符。</summary>
    private static readonly List<Activation> activations = new();

    /// <summary>物品正式启用后保留纸符快照，不依赖随后已被消耗的物品实体。</summary>
    /// <param name="caster">真实施放载体。</param><param name="talismanEntity">已受理的符箓。</param>
    /// <param name="skillContainer">原法术容器。</param><param name="direction">真实出手方向。</param>
    /// <param name="powerLevel">原有威力参数，不参与显示计时。</param><param name="strength">原有强度，不新增结算。</param>
    public static void QueueActivation(BaseSimObject caster, Entity talismanEntity, Entity skillContainer,
        Vector3 direction, float powerLevel, float strength)
    {
        if (caster == null || caster.isRekt() || !talismanEntity.TryGetComponent(out SpecialItem item)) return;
        if (item.self.IsNull) item.self = talismanEntity;
        Sprite icon = item.GetSprite();
        if (icon == null) return;
        SkillContainer skill = skillContainer.GetComponent<SkillContainer>();
        Color color = skill.ColorPalette.GetColor(0, SkillVfxColor.GetElementColor(skill.Asset.Element));
        float height = caster.isActor() ? SpatialActorPose.Height(caster.a) : .8f;
        direction = direction.sqrMagnitude > .0001f ? direction.normalized : Vector3.right;
        Vector3 point = caster.GetSimPos() + Vector3.forward * height * .35f + direction * height * .25f;
        activations.Add(new Activation { Model = SpatialSpriteMesh.Get(icon), Position = point,
            Direction = direction, Height = height, Color = color, Started = World.world.getCurWorldTime() });
        for (int i = 0; i < 5; i++)
            SpatialVisualEffects.Particle(point, direction * (.3f + i * .1f) + Vector3.forward * .4f,
                color, .05f, .3f + i * .025f);
    }

    /// <summary>纸符逐渐燃解，短流动与主体共享世界时间。</summary>
    /// <param name="output">本帧快照。</param>
    internal static void Append(List<SpatialVisualMeshInstance> output)
    {
        double now = World.world.getCurWorldTime();
        for (int i = activations.Count - 1; i >= 0; i--)
        {
            Activation item = activations[i];
            float age = (float)(now - item.Started);
            if (age >= .45f) { activations.RemoveAt(i); continue; }
            float scale = item.Height * .38f / Mathf.Max(item.Model.Bounds.size.x, item.Model.Bounds.size.y);
            Color paper = Color.Lerp(Color.white, item.Color, age / .45f); paper.a = 1f - age / .45f;
            SpatialVisualDraw.Mesh(output, item.Model, item.Position + Vector3.forward * age * .2f,
                Quaternion.LookRotation(-item.Direction, Vector3.forward), Vector3.one * scale, paper, false);
        }
    }

    /// <summary>切换世界释放纸符和快照。</summary>
    internal static void ClearWorldState() => activations.Clear();

    /// <summary>已经受理的一次启用快照。</summary>
    private sealed class Activation
    {
        /// <summary>真实纸符轮廓。</summary>
        internal SpatialVisualMesh Model;
        /// <summary>真实作用位置与方向。</summary>
        internal Vector3 Position, Direction;
        /// <summary>身体尺度。</summary>
        internal float Height;
        /// <summary>法术语义色。</summary>
        internal Color Color;
        /// <summary>启用世界时间。</summary>
        internal double Started;
    }
}
