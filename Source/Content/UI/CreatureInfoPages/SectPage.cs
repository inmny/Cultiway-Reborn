using Cultiway.Utils;
using System.Collections.Generic;
using System.Text;
using Cultiway.Content;
using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Libraries;
using Cultiway.UI.Prefab;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.Content.UI.CreatureInfoPages;

public class SectPage : MonoBehaviour
{
    public Text Text { get; private set; }
    public static void Setup(CreatureInfoPage page)
    {
        var this_page = page.gameObject.AddComponent<SectPage>();
        var text = page.gameObject.AddComponent<Text>();

        text.font = UIUtils.GetCurrentFont();
        text.fontSize = 8;

        this_page.Text = text;
    }
    [Hotfixable]
    public static void Show(CreatureInfoPage page, Actor actor)
    {
        ActorExtend ae = actor.GetExtend();
        var sb = new StringBuilder();

        var sect = ae.sect;
        if (sect == null)
        {
            sb.AppendLine("暂无宗门");
            SetText(page, sb);
            return;
        }
        SectPersonnelScore score = sect.GetPersonnelScore(actor);

        sb.AppendLine($"宗门: {sect.name}");
        sb.AppendLine($"称谓: {actor.GetSectRoleSummary()}");
        sb.AppendLine($"门阶: {GetRoleName(actor.GetSectRole(SectRoleSlot.Grade))}  职司: {GetRoleName(actor.GetSectRole(SectRoleSlot.Office))}  头衔: {GetRoleName(actor.GetSectRole(SectRoleSlot.Title))}");
        sb.AppendLine($"人事评分: {score.Total} (境界 {score.Realm} / 资历 {score.Tenure} / 贡献 {score.Contribution})");
        sb.AppendLine($"等级: {sect.data.Level}");
        sb.AppendLine($"声望: {sect.data.Reputation}");
        sb.AppendLine();

        AppendLeaderInfo(sb, sect);
        AppendFounderInfo(sb, sect);
        AppendHomeCityInfo(sb, sect);
        AppendDoctrineInfo(sb, sect);
        AppendMemberInfo(sb, sect);
        AppendArchiveInfo(sb, sect);

        SetText(page, sb);
    }

    private static void AppendLeaderInfo(StringBuilder sb, Sect sect)
    {
        Actor leader = sect.GetLeaderActor();
        if (leader != null)
        {
            sb.AppendLine($"掌门: {leader.getName()}");
            AppendActorCultivation(sb, leader, "\t");
            return;
        }

        sb.AppendLine(string.IsNullOrEmpty(sect.data.LeaderActorName)
            ? "掌门: 暂缺"
            : $"掌门: {sect.data.LeaderActorName} (已故/失踪)");
    }

    private static void AppendFounderInfo(StringBuilder sb, Sect sect)
    {
        if (string.IsNullOrEmpty(sect.data.FounderActorName)) return;

        string founder = sect.data.FounderActorName;
        if (sect.data.FounderActorID > 0)
        {
            Actor founderActor = World.world.units.get(sect.data.FounderActorID);
            if (founderActor != null && !founderActor.isRekt())
            {
                founder = founderActor.getName();
            }
        }

        sb.AppendLine($"开山祖师: {founder}");
    }

    private static void AppendHomeCityInfo(StringBuilder sb, Sect sect)
    {
        if (sect.data.HomeCityID > 0)
        {
            City city = World.world.cities.get(sect.data.HomeCityID);
            if (city != null && !city.isRekt())
            {
                sb.AppendLine($"驻地: {city.name}");
                return;
            }
        }

        if (!string.IsNullOrEmpty(sect.data.HomeCityName))
        {
            sb.AppendLine($"驻地: {sect.data.HomeCityName} (已失去)");
        }
        else
        {
            sb.AppendLine("驻地: 未定");
        }
    }

    private static void AppendDoctrineInfo(StringBuilder sb, Sect sect)
    {
        sb.AppendLine();
        sb.AppendLine("★ 道统");

        CultibookAsset doctrine = sect.GetDoctrineCultibook();
        if (doctrine == null)
        {
            if (string.IsNullOrEmpty(sect.data.DoctrineCultibookName))
            {
                sb.AppendLine("\t主修功法: 无");
            }
            else
            {
                sb.AppendLine($"\t主修功法: {sect.data.DoctrineCultibookName} (已失传)");
            }

            return;
        }

        sb.AppendLine($"\t主修功法: {doctrine.Name}");
        string levelName = doctrine.Level.GetName();
        if (!string.IsNullOrEmpty(levelName))
        {
            sb.AppendLine($"\t品阶: {levelName}");
        }

        if (!string.IsNullOrEmpty(doctrine.CultivateMethodId))
        {
            var method = doctrine.GetCultivateMethod();
            if (method != null)
            {
                sb.AppendLine($"\t修炼方式: {method.id.Localize()}");
            }
        }
    }

    private static void AppendMemberInfo(StringBuilder sb, Sect sect)
    {
        List<Actor> members = sect.GetLivingMembers();
        int leader = 0;
        int elders = 0;
        int deacons = 0;
        int successors = 0;
        int direct = 0;
        int inner = 0;
        int outer = 0;

        foreach (Actor member in members)
        {
            if (member.HasSectRole(SectRoles.Leader)) leader++;
            if (member.HasSectRole(SectRoles.Elder)) elders++;
            if (member.HasSectRole(SectRoles.Deacon)) deacons++;
            if (member.HasSectRole(SectRoles.Successor)) successors++;
            if (member.HasSectRole(SectRoles.DirectDisciple)) direct++;
            if (member.HasSectRole(SectRoles.InnerDisciple)) inner++;
            if (member.HasSectRole(SectRoles.OuterDisciple)) outer++;
        }

        sb.AppendLine();
        sb.AppendLine("○ 门人");
        sb.AppendLine($"\t总人数: {members.Count}");
        sb.AppendLine($"\t掌门: {leader}  长老: {elders}  执事: {deacons}  衣钵: {successors}");
        sb.AppendLine($"\t亲传: {direct}  内门: {inner}  外门: {outer}");
    }

    private static void AppendArchiveInfo(StringBuilder sb, Sect sect)
    {
        List<Book> cultibooks = sect.GetScriptureBooks(BookTypes.Cultibook);

        sb.AppendLine();
        sb.AppendLine("□ 藏经阁");
        sb.AppendLine($"\t功法: {cultibooks.Count}");
        sb.AppendLine($"\t丹方: {sect.data.ElixirRecipeCount}");
        sb.AppendLine($"\t术法: {sect.data.SkillbookCount}");

        if (cultibooks.Count == 0) return;

        int shown = Mathf.Min(5, cultibooks.Count);
        for (int i = 0; i < shown; i++)
        {
            sb.AppendLine($"\t- {cultibooks[i].data.name}");
        }

        if (cultibooks.Count > 5)
        {
            sb.AppendLine($"\t... 另有 {cultibooks.Count - 5} 部");
        }
    }

    private static void AppendActorCultivation(StringBuilder sb, Actor actor, string prefix)
    {
        var ae = actor.GetExtend();
        if (!ae.HasCultisys<Xian>()) return;

        ref var xian = ref ae.GetCultisys<Xian>();
        sb.AppendLine($"{prefix}境界: {Cultisyses.Xian.GetLevelName(xian.CurrLevel)}");
    }

    private static string GetRoleName(SectRoleAsset role)
    {
        return role == null ? "未定" : role.GetName();
    }

    private static void SetText(CreatureInfoPage page, StringBuilder sb)
    {
        var this_page = page.GetComponent<SectPage>();
        if (this_page == null || this_page.Text == null) return;
        this_page.Text.text = sb.ToString();
    }
}
