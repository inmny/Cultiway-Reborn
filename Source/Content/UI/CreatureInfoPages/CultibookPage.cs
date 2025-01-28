using System.Text;
using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.UI.Prefab;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.Content.UI.CreatureInfoPages;

public class CultibookPage : MonoBehaviour
{
    public Text Text { get; private set; }
    public static void Setup(CreatureInfoPage page)
    {
        var this_page = page.gameObject.AddComponent<CultibookPage>();
        var text = page.gameObject.AddComponent<Text>();

        text.font = LocalizedTextManager.currentFont;
        text.fontSize = 8;

        this_page.Text = text;
    }
    [Hotfixable]
    public static void Show(CreatureInfoPage page, Actor actor)
    {
        ActorExtend ae = actor.GetExtend();
        var sb = new StringBuilder();

        var cultibook_master = ae.GetCultibookMasterRelation();
        var cultibook = cultibook_master.Cultibook.GetComponent<Cultibook>();
        var level = cultibook_master.Cultibook.GetComponent<ItemLevel>();
        sb.AppendLine($"功法: {cultibook}");
        sb.AppendLine($"\t{level.GetName()}");
        sb.AppendLine($"掌握程度: {cultibook_master.MasterValue}");

        var this_page = page.GetComponent<CultibookPage>();
        this_page.Text.text = sb.ToString();
    }
}