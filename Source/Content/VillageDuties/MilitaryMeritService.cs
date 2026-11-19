namespace Cultiway.Content.VillageDuties;

/// <summary>个人军功只在原版确认个人击杀时增加，不随退役或身份变化清零。</summary>
public static class MilitaryMeritService
{
    /// <summary>随原版人物保存的独立军功数值。</summary>
    public const string SaveKey = "cw.village.military_merit";

    /// <summary>首次建立记录时计入原版已有击杀，此后只读取这一份军功。</summary>
    /// <param name="actor">获得军功的人物。</param>
    public static long Get(Actor actor)
    {
        if (actor?.data == null) return 0;
        actor.data.get(SaveKey, out long merit, -1L);
        if (merit >= 0) return merit;
        merit = System.Math.Max(0, actor.data.kills);
        actor.data.set(SaveKey, merit);
        return merit;
    }

    /// <summary>用记账前的值结算本次击杀，避免首次初始化重复计入。</summary>
    /// <param name="actor">原版确认获得击杀的人物。</param>
    /// <param name="previousMerit">此次击杀之前的军功。</param>
    public static void RecordKill(Actor actor, long previousMerit)
    {
        actor.data.set(SaveKey, previousMerit + 1);
    }

    /// <summary>按军功、军事能力、等级、稳定编号比较合格候选者。</summary>
    /// <param name="left">参与任命比较的第一人。</param>
    /// <param name="right">参与任命比较的第二人。</param>
    public static int Compare(Actor left, Actor right)
    {
        int order = Get(right).CompareTo(Get(left));
        if (order == 0) order = right.stats["warfare"].CompareTo(left.stats["warfare"]);
        if (order == 0) order = right.data.level.CompareTo(left.data.level);
        return order == 0 ? left.id.CompareTo(right.id) : order;
    }
}
