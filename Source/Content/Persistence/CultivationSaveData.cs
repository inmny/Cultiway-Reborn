using Cultiway.Content.Components;

namespace Cultiway.Content.Persistence;

/// <summary>人物修炼的永久进度；活动节点、背包和技能执行不混入这份记录。</summary>
public sealed class CultivationSaveData
{
    /// <summary>修仙境界与灵气。</summary>
    public Xian? Xian;
    /// <summary>骑士等级与斗气。</summary>
    public Knight? Knight;
    /// <summary>魔法等级与精神力。</summary>
    public Magic? Magic;
    /// <summary>真气成果及凝练积累。</summary>
    public QiRefinementState? Qi;
    /// <summary>筑基成果与五行精气神。</summary>
    public XianBase? Foundation;
    /// <summary>金丹成果。</summary>
    public Jindan? Core;
    /// <summary>元婴成果。</summary>
    public Yuanying? NascentSoul;
    /// <summary>元神成果和原始身体印记，不代表已经恢复元神活动。</summary>
    public Yuanshen? PrimordialSoul;
    /// <summary>修炼方式与元素暴露的累计实践。</summary>
    public CultivationPracticeState? Practice;
    /// <summary>个人暂存的修炼资源。</summary>
    public CultivationResourceState? Resources;
    /// <summary>已经掌握的骑士风格编号。</summary>
    public KnightStyleMastery? KnightStyles;
}
