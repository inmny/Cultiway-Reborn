using System.Text;
using Cultiway.Const;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.UI.Prefab;
using Cultiway.Utils.Extension;
using NeoModLoader.General;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI.CreatureInfoPages;

public class ElementRootPage : MonoBehaviour
{
    public Text Text { get; private set; }

    public static void Setup(CreatureInfoPage page)
    {
        var er_page = page.gameObject.AddComponent<ElementRootPage>();
        var text = page.gameObject.AddComponent<Text>();

        text.font = LocalizedTextManager.currentFont;
        text.fontSize = 8;

        er_page.Text = text;
    }

    public static void Show(CreatureInfoPage page, Actor actor)
    {
        ActorExtend ae = actor.GetExtend();
        var sb = new StringBuilder();

        ElementRoot er = ae.GetElementRoot();
        sb.AppendLine($"灵根类别: {er.Type.GetName()}");
        sb.AppendLine($"\t{er.Type.GetDescription()}");
        sb.AppendLine("各组分强度:");
        for (var i = 0; i < ElementIndex.ElementNames.Count; i++)
            sb.AppendLine($"\t{LM.Get(ElementIndex.ElementNames[i])}: {(int)(100 * Mathf.Exp(er[i]))}%");

        sb.AppendLine($"期望修炼倍率: {(int)(ae.GetElementRoot().GetStrength() * 100)}%");

        var er_page = page.GetComponent<ElementRootPage>();
        er_page.Text.text = sb.ToString();
    }
}