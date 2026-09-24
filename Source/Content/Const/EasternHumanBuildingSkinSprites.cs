using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

namespace Cultiway.Content.Const
{
    /// <summary>
    /// 文化皮肤 → 建筑风格贴图。
    /// 挂接原版 <c>BuildingAsset.get_override_sprites_main</c>（渲染取帧时逐建筑调用）。
    /// 目录级判定：只要风格目录 <c>cultiway/building_skins/&lt;风格key&gt;/&lt;建筑id&gt;/</c>
    /// 下存在该建筑的贴图，就整体只使用风格包贴图（缺失的变体条目用风格包内其他变体顶替，
    /// 绝不与通用贴图混用）；目录下无任何贴图时返回 null，由原版回落通用贴图。
    /// 目录结构与基础目录一致：单帧条目为 <c>main_N.png</c>，多帧动画条目为
    /// <c>main_N_0.png、main_N_1.png...</c>（兼容历史命名 <c>main_N _0.png</c>，多帧优先于单帧）。
    /// </summary>
    public static class EasternHumanBuildingSkinSprites
    {
        private const string RootPath = "cultiway/building_skins/";
        private const string DefaultStyleKey = "1"; // 通用风格即基础贴图，无需覆盖
        private const int MaxEntryProbe = 16;       // 变体条目探测上限

        private static readonly ConcurrentDictionary<(string style, string building_id), Sprite[][]> BuildingEntries = new();

        /// <summary>渲染取帧回调：返回风格包中该建筑的帧序列；风格包未收录该建筑时返回 null。</summary>
        public static Sprite[] OverrideMain(Building building)
        {
            var culture = building.city?.culture;
            if (culture == null) return null;
            culture.data.get(ContentCultureDataKeys.SkinID_int, out int skin_id, -1);
            if (skin_id < 0) return null;
            if (!EasternHumanSkinStyles.TryGetKey(skin_id, out string style)) return null;
            if (style == DefaultStyleKey) return null;

            Sprite[][] entries = GetBuildingEntries(style, building.asset.id);
            if (entries == null) return null; // 风格包未收录该建筑 → 回落通用贴图

            int entry = building.animData_index;
            if (entry < 0 || entry >= entries.Length || entries[entry] == null)
            {
                // 该变体条目在风格包中缺失：用风格包内任一已有变体顶替，保持整体风格统一
                foreach (Sprite[] frames in entries)
                {
                    if (frames != null)
                    {
                        return frames;
                    }
                }
                return null;
            }
            return entries[entry];
        }

        /// <summary>
        /// 探测并缓存风格包中该建筑全部变体条目的帧序列；
        /// 无任何贴图时返回 null（调用方回落通用贴图）。
        /// </summary>
        private static Sprite[][] GetBuildingEntries(string style, string building_id)
        {
            var key = (style, building_id);
            if (BuildingEntries.TryGetValue(key, out var cached))
            {
                return cached.Length > 0 ? cached : null;
            }

            var entries = new Sprite[MaxEntryProbe][];
            bool any = false;
            for (int entry = 0; entry < MaxEntryProbe; entry++)
            {
                entries[entry] = TryGetEntryFrames(style, building_id, entry);
                if (entries[entry] != null)
                {
                    any = true;
                }
            }

            BuildingEntries[key] = entries;
            return any ? entries : null;
        }

        private static Sprite[] TryGetEntryFrames(string style, string building_id, int entry)
        {
            // 多帧动画条目优先（如风车叶片动画）：main_N_0.png、main_N_1.png...
            // 兼容历史文件名怪癖：部分基础素材动画帧为 "main_N _K.png"（下划线前带空格，原版加载器靠 int.Parse 容忍）
            List<Sprite> frames = new();
            for (int f = 0; f < 64; f++)
            {
                Sprite frame = SpriteTextureLoader.getSprite($"{RootPath}{style}/{building_id}/main_{entry}_{f}")
                               ?? SpriteTextureLoader.getSprite($"{RootPath}{style}/{building_id}/main_{entry} _{f}");
                if (frame == null) break;
                frames.Add(frame);
            }
            if (frames.Count > 0)
            {
                return frames.ToArray();
            }

            // 单帧条目：main_N.png
            Sprite single = SpriteTextureLoader.getSprite($"{RootPath}{style}/{building_id}/main_{entry}")
                            ?? SpriteTextureLoader.getSprite($"{RootPath}{style}/{building_id}/main_{entry} ");
            return single != null ? new[] { single } : null;
        }
    }
}
