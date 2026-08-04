using UnityEngine;

namespace Cultiway.Content.Visuals;

internal static class SkavenLeaderBannerVfx
{
    public static void Draw(QuantumSpriteAsset asset)
    {
        if (asset?.group_system == null) return;

        SkavenEvolution.ForEachSkaven(actor =>
        {
            if (actor == null || !actor.isAlive() || !actor.is_visible || !SkavenEvolution.IsGroupLeader(actor))
            {
                return;
            }

            var position = actor.getHeadOffsetPositionForFunRendering();
            var banner = QuantumSpriteLibrary.drawQuantumSprite(
                asset, position, null, null, null, null, 1f, false, actor.current_scale.y);
            var color = GetStableColor(actor.data.id);
            banner.setColor(ref color);
            banner.checkRotation(position, actor, -0.01f);
        });
    }

    private static Color GetStableColor(long actorId)
    {
        var value = unchecked((ulong)actorId + 0x9E3779B97F4A7C15UL);
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        value ^= value >> 31;

        var hue = (value & 0xFFFFUL) / 65535f;
        var saturation = 0.65f + ((value >> 16) & 0xFFUL) / 255f * 0.25f;
        var brightness = 0.8f + ((value >> 24) & 0xFFUL) / 255f * 0.2f;
        return Color.HSVToRGB(hue, saturation, brightness);
    }
}
