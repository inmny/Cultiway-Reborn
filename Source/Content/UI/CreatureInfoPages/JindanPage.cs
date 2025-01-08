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

public class JindanPage : MonoBehaviour
{
    public Text Text { get; private set; }
    public static void Setup(CreatureInfoPage page)
    {
        var this_page = page.gameObject.AddComponent<JindanPage>();
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

        Jindan jindan = ae.GetJindan();
        sb.AppendLine($"金丹类别: {jindan.Type.GetName()}");
        sb.AppendLine($"\t{jindan.Type.GetDescription()}");
        sb.AppendLine($"金丹强度: {jindan.strength}");
        sb.AppendLine($"自带法术: \n\t{jindan.Type.wrapped_skill_id}");
        if (ae.HasComponent<JindanCultivation>())
        {
            var cultivation = ae.GetComponent<JindanCultivation>();
            sb.AppendLine($"金丹修为: {cultivation.stage}转金丹");
        }

        var this_page = page.GetComponent<JindanPage>();
        this_page.Text.text = sb.ToString();
    }
}