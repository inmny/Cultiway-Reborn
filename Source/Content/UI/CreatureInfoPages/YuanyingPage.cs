using System.Text;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
using Cultiway.Core;
using Cultiway.UI.CreatureInfoPages;
using Cultiway.UI.Prefab;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;
using NeoModLoader.General;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.Content.UI.CreatureInfoPages;

public class YuanyingPage : MonoBehaviour
{
    public Text Text { get; private set; }
    public static void Setup(CreatureInfoPage page)
    {
        var this_page = page.gameObject.AddComponent<YuanyingPage>();
        var text = page.gameObject.AddComponent<Text>();

        text.font = LocalizedTextManager.current_font;
        text.fontSize = 8;

        this_page.Text = text;
    }
    [Hotfixable]
    public static void Show(CreatureInfoPage page, Actor actor)
    {
        ActorExtend ae = actor.GetExtend();
        var sb = new StringBuilder();

        Yuanying yuanying = ae.GetYuanying();
        sb.AppendLine($"元婴类别: {yuanying.Type.GetName()}");
        sb.AppendLine($"\t{yuanying.Type.GetDescription()}");
        sb.AppendLine($"元婴强度: {yuanying.strength}");

        var this_page = page.GetComponent<YuanyingPage>();
        this_page.Text.text = sb.ToString();
    }
}