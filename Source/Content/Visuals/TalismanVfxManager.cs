using Cultiway.Core.Components;
using Cultiway.Core.Semantics;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Visuals;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Visuals;

public static class TalismanVfxManager
{
    public static void QueueActivation(BaseSimObject caster, Entity talismanEntity, Entity skillContainer,
        Vector3 direction, float powerLevel, float strength)
    {
        if (caster == null || caster.isRekt()) return;
        if (talismanEntity.IsNull || skillContainer.IsNull) return;
        if (!skillContainer.HasComponent<SkillContainer>()) return;

        var icon = GetTalismanIcon(talismanEntity);
        if (icon == null) return;

        var skill = skillContainer.GetComponent<SkillContainer>();
        var skillAsset = skill.Asset;
        if (skillAsset == null) return;

        var element = skill.VfxElement;
        var colorPalette = skill.ColorPalette;
        var color = colorPalette.GetColor(0, SkillVfxColor.GetElementColor(skillAsset.Element));
        color.a = 0.82f;
        var accentColor = colorPalette.Count > 1
            ? SemanticColorResolver.ToVfxColor(colorPalette.Secondary)
            : element.GetAccentColor(color);
        accentColor.a = element.AccentAlpha;
        var frames = TalismanVfxFrameLibrary.GetActivationFrames(icon, color, accentColor);
        if (frames.Length == 0) return;

        var pos = caster.GetSimPos();
        pos.z += 1.0f;

        const float scale = 4.2f;
        const float frameInterval = 0.075f;
        ModClass.I.SkillV3.SpawnAnim(frames, pos, Vector3.right, scale, frameInterval: frameInterval,
            lifeTime: frames.Length * frameInterval, visualRotation: VisualRotation.FixedUpright());
    }

    private static Sprite GetTalismanIcon(Entity talismanEntity)
    {
        if (!talismanEntity.TryGetComponent(out SpecialItem item)) return null;
        if (item.self.IsNull) item.self = talismanEntity;
        return item.GetSprite();
    }
}
