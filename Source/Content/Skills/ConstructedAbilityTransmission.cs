using System;
using System.Linq;
using Cultiway.Content.Sects;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.Skills;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Skills;

/// <summary>让确定版本的已学构造进入原有写书、存书与读书流程。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>能力是否确实存在于当前世界。</summary>
    /// <param name="id">稳定能力编号。</param>
    public static bool Contains(string id) => id != null && definitions.ContainsKey(id);

    /// <summary>只读当前掌握，不创建或复制另一份定义。</summary>
    /// <param name="actor">实际生灵。</param>
    /// <param name="id">确定版本编号。</param>
    public static bool Knows(ActorExtend actor, string id) => actor != null &&
                                                              actor.E.TryGetComponent(out ConstructedKnowledge known) &&
                                                              known.Ids.Contains(id);

    /// <summary>阅读前检查实际目录与掌握容量。</summary>
    /// <param name="actor">实际读者。</param>
    /// <param name="id">书中记录的能力。</param>
    public static bool CanLearn(ActorExtend actor, string id) => Contains(id) && actor != null &&
                                                                 (!actor.E.TryGetComponent(
                                                                      out ConstructedKnowledge known) ||
                                                                  known.Ids.Contains(id) || known.Ids.Length < 128);

    /// <summary>直接读取当前正式计划生成说明，切换语言后不沿用旧说明缓存。</summary>
    /// <param name="id">当前世界的确定版本。</param>
    public static string Explain(string id) => ConstructionPresentation.Explain(definitions[id], PlanFor(id));

    /// <summary>写书只记录作者真正掌握的稳定版本；书本不会拥有或销毁世界能力定义。</summary>
    /// <param name="actor">实际执笔者。</param>
    /// <param name="id">已经掌握的能力。</param>
    public static Book WriteBook(Actor actor, string id)
    {
        if (actor == null || actor.isRekt() || !Contains(id) || !Knows(actor.GetExtend(), id)) return null;
        Book book = World.world.books.NewBook(actor, BookTypes.Skillbook);
        if (book == null) return null;
        book.GetExtend().E.AddComponent(new ConstructionBook { AbilityId = id });
        book.data.name = definitions[id].Name;
        return book;
    }

    /// <summary>城市和宗门沿用原有位置与权限规则，仅补充确定版本的去重。</summary>
    /// <param name="city">实际城市。</param>
    /// <param name="id">要收入的能力。</param>
    public static bool CanStore(City city, string id) => city != null && !city.isRekt() && city.hasBookSlots() &&
                                                         Contains(id) && !city.getBooks().Select(bookId =>
                                                                 World.world.books.get(bookId))
                                                             .Where(book => book != null && !book.isRekt()).Any(book =>
                                                                 book.GetExtend().E
                                                                     .TryGetComponent(out ConstructionBook content) &&
                                                                 content.AbilityId == id);

    /// <summary>宗门的版本去重；贡献权限由既有写书流程检查。</summary>
    /// <param name="sect">实际宗门。</param>
    /// <param name="id">要收入的能力。</param>
    public static bool CanStore(Sect sect, string id) => sect != null && !sect.isRekt() && Contains(id) &&
                                                         !sect.Scriptures.ContainsConstruction(id);

    /// <summary>自主写书从真正掌握的构造里选择，优先满足宗门，再考虑城市。</summary>
    /// <param name="actor">正在选择写作内容的人物。</param>
    /// <param name="destination">原有收书入口。</param>
    /// <param name="id">本次固定版本。</param>
    public static bool TryPickBook(Actor actor, out ScriptureBookDestination destination, out string id)
    {
        destination = default;
        id = null;
        if (actor == null || actor.isRekt() || !actor.hasCity() || !actor.hasLanguage() ||
            !actor.GetExtend().E.TryGetComponent(out ConstructedKnowledge known)) return false;
        Sect sect = actor.GetExtend().sect;
        string[] candidates = known.Ids.Where(Contains).ToArray();
        if (SectScripturePolicy.CanContribute(actor, sect))
        {
            string[] missing = candidates.Where(value => CanStore(sect, value)).ToArray();
            if (missing.Length > 0)
            {
                id = missing[Randy.randomInt(0, missing.Length - 1)];
                destination = ScriptureBookDestination.ForSect(sect);
                return true;
            }
        }

        string[] cityMissing = candidates.Where(value => CanStore(actor.getCity(), value)).ToArray();
        if (cityMissing.Length == 0) return false;
        id = cityMissing[Randy.randomInt(0, cityMissing.Length - 1)];
        destination = ScriptureBookDestination.ForCity(actor.getCity());
        return true;
    }
}