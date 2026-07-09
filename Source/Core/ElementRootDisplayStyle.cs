namespace Cultiway.Core;

/// <summary>
///     ElementRootPage 展示风格：不同修炼体系对"元素亲和/灵根"的称谓与强度等级名。
///     强度等级采用"stage × level"组合式命名（如仙道"天阶一品"、魔法"日环十二"）。
/// </summary>
public class ElementRootDisplayStyle
{
    /// <summary>类别行标签的 locale key（如"灵根类别"/"元素亲和"）</summary>
    public string category_label_key;

    /// <summary>各组分强度行标签的 locale key</summary>
    public string components_label_key;

    /// <summary>综合评价行标签的 locale key</summary>
    public string overall_label_key;

    /// <summary>ElementRootPage 页面标题的 locale key（如"灵根详解"/"元素亲和详解"）。null 时回退 ui.csv 的 ElementRootPage。</summary>
    public string page_title_key;

    /// <summary>强度大档位数（仙道4：黄玄地天，魔法12：十二环）</summary>
    public int stage_count;

    /// <summary>每个大档位下的小档位数（仙道9：九品，魔法3：日月星）</summary>
    public int level_per_stage;

    /// <summary>各 stage 名的 locale key，长度须等于 stage_count</summary>
    public string[] stage_name_keys;

    /// <summary>各 level 名的 locale key，长度须等于 level_per_stage</summary>
    public string[] level_name_keys;

    /// <summary>
    ///     等级名拼接模板，用 {stage} 和 {level} 占位符。
    ///     例：仙道 "{stage}阶{level}"（天阶一品），魔法 "{level}{stage}"（日环十二）。
    /// </summary>
    public string level_format = "{stage}阶{level}";

    /// <summary>元素根名字的 locale key 前缀（实际 key = {prefix}.{er_id}）。null/空时走默认 Cultiway.ER。</summary>
    public string element_root_name_prefix;

    /// <summary>元素根描述的 locale key 前缀（实际 key = {prefix}.{er_id}.Info）。null/空时走默认 Cultiway.ER。</summary>
    public string element_root_desc_prefix;

    /// <summary>总强度档位数 = stage_count × level_per_stage</summary>
    public int TotalLevelCount => stage_count * level_per_stage;
}
