using System;
using System.Collections.Generic;

namespace Cultiway.Content.Const
{
    /// <summary>
    /// 东方人族文化服饰风格定义与切换。
    /// 风格 key 对应 skin 文件夹后缀（male_tang → "tang"，male_1 → "1"）。
    /// 通过神力点击文化的单位/城市，循环切换该文化的服饰风格。
    /// </summary>
    public static class EasternHumanSkinStyles
    {
        // 展示/循环顺序（按历史脉络）
        private static readonly string[] _order =
        {
            "1",        // 通用
            "chunqiu",  // 春秋
            "tang",     // 唐
            "song",     // 宋
            "yuan",     // 元
            "ming",     // 明
            "qing",     // 清
            "fusang",   // 扶桑
            "chaoxian"  // 朝鲜
        };

        // 数组顺序由 Directory.GetDirectories 决定（不稳定），故运行时动态建立 index↔key 双向映射
        private static readonly Dictionary<int, string> _indexToKey = new();
        private static readonly Dictionary<string, int> _keyToIndex = new();

        /// <summary>
        /// 扫描 skin_citizen_male 数组，建立 index↔key 双向映射。应在 Actors.EasternHuman 初始化后调用。
        /// </summary>
        public static void BuildIndex(string[] skin_citizen_male)
        {
            _indexToKey.Clear();
            _keyToIndex.Clear();
            const string prefix = "male_";
            for (int i = 0; i < skin_citizen_male.Length; i++)
            {
                string folder = skin_citizen_male[i];
                string key = folder.StartsWith(prefix) ? folder.Substring(prefix.Length) : folder;
                _indexToKey[i] = key;
                _keyToIndex[key] = i;
            }
        }

        /// <summary>
        /// 将文化的服饰风格循环切换到下一个，并刷新所有成员的纹理。
        /// 返回新风格的 key（用于本地化反馈）。
        /// </summary>
        public static string CycleNext(Culture culture)
        {
            culture.data.get(ContentCultureDataKeys.SkinID_int, out int cur, -1);
            if (cur < 0 || !_indexToKey.TryGetValue(cur, out string cur_key))
            {
                cur_key = _order[0];
            }

            int pos = Array.IndexOf(_order, cur_key);
            string next_key = _order[(pos + 1) % _order.Length];

            if (!_keyToIndex.TryGetValue(next_key, out int next_index))
            {
                ModClass.LogInfo($"[EasternHumanSkin] style '{next_key}' not found in skin folders, skip.");
                return next_key;
            }

            culture.data.set(ContentCultureDataKeys.SkinID_int, next_index);
            foreach (Actor actor in culture.units)
            {
                actor.clearSprites();
            }
            return next_key;
        }

        public static string StyleNameLocaleKey(string key) => "eastern_human_skin_" + key;
    }
}
