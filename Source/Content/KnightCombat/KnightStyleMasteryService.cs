using System;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core;

namespace Cultiway.Content.KnightCombat;

/// <summary>统一读写角色已经掌握的骑士流派。</summary>
public static class KnightStyleMasteryService
{
    /// <summary>判断角色是否已经掌握指定流派。</summary>
    public static bool IsMastered(ActorExtend actor, KnightStyleAsset style)
    {
        return actor.TryGetComponent(out KnightStyleMastery mastery) &&
               Array.IndexOf(mastery.style_ids, style.id) >= 0;
    }

    /// <summary>掌握指定流派，并返回本次是否为首次掌握。</summary>
    public static bool Master(ActorExtend actor, KnightStyleAsset style)
    {
        if (!actor.HasComponent<KnightStyleMastery>())
        {
            actor.AddComponent(new KnightStyleMastery
            {
                style_ids = [style.id]
            });
            return true;
        }

        ref KnightStyleMastery mastery = ref actor.GetComponent<KnightStyleMastery>();
        if (Array.IndexOf(mastery.style_ids, style.id) >= 0) return false;

        int length = mastery.style_ids.Length;
        Array.Resize(ref mastery.style_ids, length + 1);
        mastery.style_ids[length] = style.id;
        return true;
    }

    /// <summary>移除指定流派，供管理和测试入口使用。</summary>
    public static void Unmaster(ActorExtend actor, KnightStyleAsset style)
    {
        if (!actor.HasComponent<KnightStyleMastery>()) return;

        ref KnightStyleMastery mastery = ref actor.GetComponent<KnightStyleMastery>();
        int index = Array.IndexOf(mastery.style_ids, style.id);
        if (index < 0) return;
        if (mastery.style_ids.Length == 1)
        {
            actor.E.RemoveComponent<KnightStyleMastery>();
            return;
        }

        Array.Copy(
            mastery.style_ids,
            index + 1,
            mastery.style_ids,
            index,
            mastery.style_ids.Length - index - 1);
        Array.Resize(ref mastery.style_ids, mastery.style_ids.Length - 1);
    }
}
