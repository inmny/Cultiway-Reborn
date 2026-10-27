using System.Collections.Generic;
using Cultiway.Content.Const;
using Cultiway.Content.Names;
using HarmonyLib;

namespace Cultiway.Content.Patch;

/// <summary>
/// 按东人文化的服饰风格重定向名称生成。
/// 原版全部 8 类名称（单位/城市/王国/文化/氏族/家族/语言/宗教）都经过
/// <see cref="NameGenerator.generateName"/> 这一个入口，在此前置拦截：
/// 种族是东人时用当前服饰风格的词库拼名；其余情况交还原版逻辑。
/// 文化未创建时（世界生成建国/建城早于建文化，见 Actor.createDefaultCultureAndLanguageAndClan）
/// 依次回退双亲文化、单位种族判断。
/// 亚种命名走独立的 <see cref="Subspecies.generateName"/>，同样在此拦截；
/// 亚种不隶属文化，按出生地城市文化 → 世界上任意东人文化 → “通用”的顺序取风格。
/// 只影响新创建的实体，已有实体的名字随存档保存、不受影响。
/// </summary>
internal static class PatchNameGenerator
{
    /// <summary>与换装循环一致的默认风格：未选过服饰风格时按“通用”命名。</summary>
    private const string DefaultStyleKey = "1";

    [HarmonyPrefix, HarmonyPatch(typeof(NameGenerator), nameof(NameGenerator.generateName))]
    private static bool generateName_prefix(Actor pActor, MetaType pType, ActorSex pSex, ref string __result)
    {
        if (pActor == null) return true;
        if (!TryResolveStyleKey(pActor, out string style_key)) return true;
        if (!EasternHumanNameLibrary.TryGet(style_key, out NameStyleData data)) return true;

        string name = EasternHumanNameComposer.Compose(data, pType, pSex);
        if (name == null) return true;

        __result = name;
        return false;
    }

    [HarmonyPrefix, HarmonyPatch(typeof(Subspecies), nameof(Subspecies.generateName))]
    private static bool generateName_prefix(Subspecies __instance, ActorAsset pAsset, WorldTile pTile)
    {
        // 亚种按种族资产判断是否东人，风格跟随文化解析。
        if (pAsset?.id != Actors.EasternHuman.id) return true;
        if (!TryResolveSubspeciesStyleKey(pTile, out string style_key)) return true;
        if (!EasternHumanNameLibrary.TryGet(style_key, out NameStyleData data)) return true;

        string name = EasternHumanNameComposer.Compose(data, MetaType.Subspecies, ActorSex.None);
        if (name == null) return true;

        // 与原版一致：重名时追加结尾音节去重后直接 setName（原方法为 void）。
        using StringBuilderPool builder = new StringBuilderPool();
        builder.Append(name);
        for (int i = 0; i < 5; i++)
        {
            if (!__instance.hasNameInWorld(builder)) break;
            builder.Append(SubspeciesManager.NAME_ENDINGS.GetRandom());
        }

        __instance.setName(builder.ToString());
        return false;
    }

    /// <summary>
    /// 亚种的服饰风格跟随文化：出生地所在城市的文化优先，
    /// 其次世界上第一个东人文化；都找不到（如世界初生时）用 <see cref="DefaultStyleKey"/>。
    /// 该文化没选过风格时同样落到默认风格。
    /// </summary>
    private static bool TryResolveSubspeciesStyleKey(WorldTile pTile, out string style_key)
    {
        Culture culture = pTile?.zone_city?.culture;
        if (culture == null && World.world != null)
        {
            foreach (Culture tCulture in World.world.cultures)
            {
                if (tCulture.getActorAsset()?.id == Actors.EasternHuman.id)
                {
                    culture = tCulture;
                    break;
                }
            }
        }

        style_key = DefaultStyleKey;
        if (culture != null)
        {
            culture.data.get(ContentCultureDataKeys.SkinID_int, out int skin_id, -1);
            if (skin_id >= 0 && EasternHumanSkinStyles.TryGetKey(skin_id, out string key))
            {
                style_key = key;
            }
        }

        return true;
    }

    /// <summary>
    /// 解析该名称应使用的服饰风格。按顺序确定种族与文化来源：
    /// 单位自身文化 → 双亲文化（对齐原版 NameGenerator 的无文化回退）→ 单位自身种族；
    /// 种族不是东人返回 false。文化不存在或未选风格时用 <see cref="DefaultStyleKey"/>。
    /// </summary>
    private static bool TryResolveStyleKey(Actor pActor, out string style_key)
    {
        style_key = null;
        Culture culture = pActor.culture;
        if (culture == null)
        {
            foreach (Actor parent in pActor.getParents())
            {
                if (parent != null && parent.hasCulture())
                {
                    culture = parent.culture;
                    break;
                }
            }
        }

        string race_id = culture != null ? culture.getActorAsset()?.id : pActor.asset?.id;
        if (race_id != Actors.EasternHuman.id) return false;

        int skin_id = -1;
        culture?.data.get(ContentCultureDataKeys.SkinID_int, out skin_id, -1);
        style_key = skin_id >= 0 && EasternHumanSkinStyles.TryGetKey(skin_id, out string key)
            ? key
            : DefaultStyleKey;
        return true;
    }
}
