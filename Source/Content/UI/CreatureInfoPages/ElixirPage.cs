using System;
using System.Text;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.UI.Prefab;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;
using NeoModLoader.General;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.Content.UI.CreatureInfoPages;

public class ElixirPage : MonoBehaviour
{
    public Text Text { get; private set; }

    public static void Setup(CreatureInfoPage page)
    {
        var elixirPage = page.gameObject.AddComponent<ElixirPage>();
        var text = page.gameObject.AddComponent<Text>();
        text.font = Cultiway.UI.UiTheme.Current.Font;
        text.fontSize = 8;
        elixirPage.Text = text;
    }

    [Hotfixable]
    public static void Show(CreatureInfoPage page, Actor actor)
    {
        var builder = new StringBuilder();
        foreach (var mastery in actor.GetExtend().GetAllMaster<ElixirAsset>())
        {
            var asset = mastery.Item1;
            builder.AppendLine($"丹方: {asset.GetName()}");
            builder.AppendLine($"\t{Localize(asset.description_key)}");
            builder.AppendLine("\t配方:");
            foreach (var ingredient in asset.ingredients ?? Array.Empty<ElixirIngredientRequirement>())
                builder.AppendLine($"\t\t{ingredient.GetName()}");
            builder.AppendLine($"\t掌握程度: {mastery.Item2}");
        }

        page.GetComponent<ElixirPage>().Text.text = builder.ToString();
    }

    private static string Localize(string value)
    {
        return !string.IsNullOrEmpty(value) && LM.Has(value) ? LM.Get(value) : value;
    }
}
