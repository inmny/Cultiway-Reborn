using System.Text;
using Cultiway.Core.Components;
using Cultiway.UI.Prefab;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI.CreatureInfoPages;

public class StatusEffectPage : MonoBehaviour
{
    public Text Text { get; private set; }
    public static void Setup(CreatureInfoPage page)
    {
        var this_page = page.gameObject.AddComponent<StatusEffectPage>();
        var text = page.gameObject.AddComponent<Text>();

        text.font = LocalizedTextManager.current_font;
        text.fontSize = 8;

        this_page.Text = text;
    }
    [Hotfixable]
    public static void Show(CreatureInfoPage page, Actor actor)
    {
        var ae = actor.GetExtend();
        var sb = new StringBuilder();

        foreach (var status_entity in ae.GetStatuses())
        {
            var status = status_entity.GetComponent<StatusComponent>().Type;
            var time = status_entity.GetComponent<AliveTimer>().value;
            string line = $"{status.GetName()} {time:F1}s";
            if (status_entity.HasComponent<AliveTimeLimit>())
            {
                line += $"/{status_entity.GetComponent<AliveTimeLimit>().value:F1}s";
            }
            else
            {
                line += "/∞";
            }
            sb.AppendLine(line);
        }

        var this_page = page.GetComponent<StatusEffectPage>();
        this_page.Text.text = sb.ToString();
    }
}