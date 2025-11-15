using System.Runtime.Remoting.Metadata.W3cXsd2001;
using System.Text;
using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.UI.Prefab;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.Content.UI.CreatureInfoPages;

public class ElixirPage : MonoBehaviour
{
    public Text Text { get; private set; }
    public static void Setup(CreatureInfoPage page)
    {
        var this_page = page.gameObject.AddComponent<ElixirPage>();
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

        foreach (var elixir_master in ae.GetAllMaster<ElixirAsset>())
        {
            var elixir_asset = elixir_master.Item1;
            sb.AppendLine($"丹方: {elixir_asset.GetName()}");
            sb.AppendLine($"\t{elixir_asset.description_key}");
            sb.AppendLine($"\t配方:");
            foreach (var ingredient in elixir_asset.ingredients)
            {
                sb.AppendLine($"\t\t{ingredient.GetName()}");
            }
            sb.AppendLine($"\t掌握程度: {elixir_master.Item2}");
        }

        var this_page = page.GetComponent<ElixirPage>();
        this_page.Text.text = sb.ToString();
    }
}