using System.Text;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
using Cultiway.Core;
using Cultiway.UI.CreatureInfoPages;
using Cultiway.UI.Prefab;
using Cultiway.Utils.Extension;
using NeoModLoader.General;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.Content.UI.CreatureInfoPages;

public class XianBasePage : MonoBehaviour
{
    public Text Text { get; private set; }
    public static void Setup(CreatureInfoPage page)
    {
        var this_page = page.gameObject.AddComponent<XianBasePage>();
        var text = page.gameObject.AddComponent<Text>();

        text.font = LocalizedTextManager.current_font;
        text.fontSize = 8;

        this_page.Text = text;
    }

    public static void Show(CreatureInfoPage page, Actor actor)
    {
        ActorExtend ae = actor.GetExtend();
        var sb = new StringBuilder();

        XianBase xian_base = ae.GetXianBase();
        sb.AppendLine("精气神三花强度:");
        sb.AppendLine($"\t精: {xian_base.jing}");
        sb.AppendLine($"\t气: {xian_base.qi}");
        sb.AppendLine($"\t神: {xian_base.shen}");
        sb.AppendLine("五行五气强度:");
        sb.AppendLine($"\t火: {xian_base.fire}");
        sb.AppendLine($"\t木: {xian_base.wood}");
        sb.AppendLine($"\t土: {xian_base.earth}");
        sb.AppendLine($"\t金: {xian_base.iron}");
        sb.AppendLine($"\t水: {xian_base.water}");

        var this_page = page.GetComponent<XianBasePage>();
        this_page.Text.text = sb.ToString();
    }
}