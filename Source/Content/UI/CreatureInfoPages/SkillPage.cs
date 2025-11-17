using System.Text;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.UI.Prefab;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.Content.UI.CreatureInfoPages;

public class SkillPage : MonoBehaviour
{
    public Text Text { get; private set; }
    public static void Setup(CreatureInfoPage page)
    {
        var this_page = page.gameObject.AddComponent<SkillPage>();
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

        foreach (var skill_container_entity in ae.all_skills)
        {
            var skill_container = skill_container_entity.GetComponent<SkillContainer>();
            if (skill_container_entity.HasName)
            {
                sb.AppendLine(skill_container_entity.Name.value);
            }
            else
            {
                sb.AppendLine(skill_container.Asset.id.Localize());
            }

            foreach (var modifier_asset in ModClass.I.SkillV3.ModifierLib.list)
            {
                var description = modifier_asset.GetDescription?.Invoke(skill_container_entity);
                if (string.IsNullOrEmpty(description)) continue;
                sb.AppendLine("\t" + description);
            }
        }

        var this_page = page.GetComponent<SkillPage>();
        this_page.Text.text = sb.ToString();
    }
}