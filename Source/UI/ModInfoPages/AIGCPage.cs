using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI.ModInfoPages;

public sealed class AIGCPage : ModInfoPage
{
    public override string Id => "AIGC";
    public override string TitleKey => "Cultiway.UI.WindowModInfo.Tab.AIGC";
    public override string DescriptionKey => "Cultiway.UI.WindowModInfo.Tab.AIGC Description";
    public override string IconPath => "cultiway/icons/iconTab";

    protected override void BuildContent(Transform root, float width)
    {
        Transform hero = CreateCard(root, "AIGC Hero", width, 7, 7, 6, 6, 3f);
        AddText(hero, "Title", "AI 演化", 9, FontStyle.Bold, TextAnchor.MiddleLeft, AccentTextColor);
        AddText(hero, "Body",
            "这是本模组最独特的地方。接入一个大语言模型（默认 DeepSeek），整个修真世界的功法、丹药、法术、材料、灵植、人名地名，都将由 AI 实时生成——每一次修炼、每一炉丹药，都是世上独一份。",
            6, FontStyle.Normal, TextAnchor.UpperLeft, PrimaryTextColor);
        Transform heroBadge = CreatePlainGroup(hero, "Badges", width - 14f, true, 3f, TextAnchor.MiddleLeft);
        AddBadge(heroBadge, "独家", 38f, AccentTextColor);
        AddBadge(heroBadge, "可关闭", 46f, CoolColor);

        Transform gen = CreateCard(root, "AI Generate", width, 6, 6, 5, 5, 4f);
        AddText(gen, "Title", "AI 能生成什么", 8, FontStyle.Bold, TextAnchor.MiddleLeft, AccentTextColor);
        Transform g1 = AddTwoColumnRow(gen, "Gen Row 1", width - 12f);
        AddMiniCard(g1, "Cultibook", "cultiway/icons/iconWriting", "功法",
            "AI 据修士灵根境界，起名、写简介、编排技能", 99f);
        AddMiniCard(g1, "Improve", "cultiway/icons/iconWriting", "改进功法",
            "把已有功法升级成更强版本", 99f);
        Transform g2 = AddTwoColumnRow(gen, "Gen Row 2", width - 12f);
        AddMiniCard(g2, "Elixir", "cultiway/icons/iconWriting", "丹药药效",
            "同样材料，每次炼出不同效果与名字", 99f);
        AddMiniCard(g2, "SkillName", "cultiway/icons/iconWriting", "法术命名",
            "火球 → 霜爆雷、焚天裂", 99f);
        Transform g3 = AddTwoColumnRow(gen, "Gen Row 3", width - 12f);
        AddMiniCard(g3, "Ingredient", "cultiway/icons/iconWriting", "灵植材料",
            "龙鳞 → 赤金龙鳞", 99f);
        AddMiniCard(g3, "Names", "cultiway/icons/iconWriting", "人名地名",
            "修士与世界区域的命名", 99f);

        Transform enable = CreateCard(root, "How To Enable", width, 6, 6, 5, 5, 2f);
        AddText(enable, "Title", "如何开启", 8, FontStyle.Bold, TextAnchor.MiddleLeft, AccentTextColor);
        AddBullet(enable, "在模组设置里找到「AI 生成内容相关设置」。");
        AddBullet(enable, "填入 BASE_URL（如 https://api.deepseek.com）、MODEL（如 deepseek-chat）、你的 API_KEY。");
        AddBullet(enable, "不填 KEY 也能玩——模组自带一批作者预生成的结果兜底，只是少了那份每次都不同的灵气。");
        AddBullet(enable, "生成结果会本地缓存，越玩越丰富。");

        Transform tip = CreateCard(root, "Tip", width, 6, 6, 5, 5, 3f);
        AddText(tip, "Title", "建议", 8, FontStyle.Bold, TextAnchor.MiddleLeft, AccentTextColor);
        AddText(tip, "Body",
            "DeepSeek 的 API 很便宜，几块钱能玩很久。配好之后，你会发现这个修真世界真的活了——每个 NPC 都有 AI 替他写的、独一无二的修行故事。",
            6, FontStyle.Normal, TextAnchor.UpperLeft, PrimaryTextColor);
    }
}
