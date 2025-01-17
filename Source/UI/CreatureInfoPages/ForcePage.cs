using System.Text;
using Cultiway.Core.Components;
using Cultiway.UI.Prefab;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI.CreatureInfoPages;

public class ForcePage : MonoBehaviour
{
    public Text Text { get; private set; }
    public static void Setup(CreatureInfoPage page)
    {
        var this_page = page.gameObject.AddComponent<ForcePage>();
        var text = page.gameObject.AddComponent<Text>();

        text.font = LocalizedTextManager.currentFont;
        text.fontSize = 8;

        this_page.Text = text;
    }
    [Hotfixable]
    public static void Show(CreatureInfoPage page, Actor actor)
    {
        var ae = actor.GetExtend();
        var sb = new StringBuilder();

        foreach (var force_entity in ae.GetForces<ForceCityBelongRelation>())
        {
            sb.AppendLine($"{force_entity}");
        }

        var this_page = page.GetComponent<ForcePage>();
        this_page.Text.text = sb.ToString();
    }
}