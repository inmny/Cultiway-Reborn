using Cultiway.Content.Components;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Persistence;

/// <summary>读档后从元神身份与形成结果重建临时渲染资料。</summary>
internal static class YuanshenSaveVisuals
{
    /// <summary>保留位置、心神和任务，只重建不能写入存档的图片和渲染绑定。</summary>
    /// <param name="node">当前操作使用的资料。</param>
    internal static void RestoreNode(Entity node)
    {
        YuanshenNodeState state = node.GetComponent<YuanshenNodeState>();
        Sprite sprite;
        Color tint;
        if (state.role == YuanshenNodeRole.Thought)
        {
            sprite = SpriteTextureLoader.getSprite("cultiway/special_effects/aura/yuanying_indicator") ??
                     SpriteTextureLoader.getSprite("cultiway/icons/artifact_atoms/spirit_awakening_script");
            tint = new Color(0.62f, 0.9f, 1f, 0.65f);
        }
        else
        {
            Actor owner = World.world.units.get(state.owner_actor_id);
            YuanshenAdvancedNodeService.ResolveAppearance(owner.GetExtend().GetComponent<Yuanshen>(),
                out YuanshenDharmaAppearance appearance, out sprite);
            tint = appearance.element_color;
            tint.a = state.role == YuanshenNodeRole.Avatar ? 0.86f : state.role == YuanshenNodeRole.Manifestation ? 0.52f : 0.72f;
        }
        node.AddComponent(new AnimData { frames = [sprite] });
        node.AddComponent(new AnimBindRenderer());
        node.AddComponent(new AnimTint(tint));
    }
}
